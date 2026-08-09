using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Commands.VisitAmendments;

/// <summary>
/// The before/after behind ONE timeline entry.
///
/// Everything here is derived from immutable rows that were written when the event happened —
/// revision snapshots, amendment change rows, audit change rows. Nothing is recomputed from current
/// state, because current state has moved on and a history that changes when you read it is not a
/// history.
///
/// Scope is resolved BEFORE anything is read, on the same terms as the timeline itself: a Staff
/// Leader sees only their own campus, a Host only the instances they host. An event belonging to a
/// campus outside that set is reported as not found rather than refused, so the response cannot be
/// used to discover that the campus exists.
/// </summary>
public sealed class GetVisitHistoryDetailQueryHandler
    : IRequestHandler<GetVisitHistoryDetailQuery, VisitHistoryDetailDto>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PerCampusFormV2Options _readFlag;

    public GetVisitHistoryDetailQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, PerCampusFormV2Options readFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _readFlag = readFlag;
    }

    public async Task<VisitHistoryDetailDto> Handle(
        GetVisitHistoryDetailQuery request, CancellationToken ct)
    {
        if (!_readFlag.Enabled) throw new NotFoundException("Không tìm thấy.");
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null) throw new ForbiddenException();
        var actorId = _currentUser.UserId.Value;

        if (!VisitHistoryEventSources.TryParse(request.EventId, out var source, out var key))
            throw new NotFoundException("Sự kiện lịch sử", 0);

        var visit = await _db.VisitRequests.AsNoTracking()
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, ct)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        // ── Scope FIRST (mirrors GetVisitRequestHistoryQueryHandler exactly) ──
        var isRegistrant = VisitRequestOwnership.IsRegistrant(visit, actorId);
        var operatedInstanceIds = VisitRequestOwnership.OperatedCampuses(visit, actorId)
            .Select(c => c.VisitInstanceId).ToList();
        var isHo = _currentUser.RoleCode == RoleCodes.Ho;
        List<ulong> visibleInstanceIds;
        var includeRequestLevel = false;
        // Identity events are readable by exactly the people the timeline shows them to — the requester
        // side, HO, and a campus's own operational contact. A Staff Leader or a Host reaching one by id
        // gets the same "not found" they would get for another campus's revision.
        var includeIdentity = false;
        if (isRegistrant || isHo)
        {
            visibleInstanceIds = visit.CampusInstances.Select(c => c.VisitInstanceId).ToList();
            includeRequestLevel = true;
            includeIdentity = true;
        }
        else if (operatedInstanceIds.Count > 0)
        {
            visibleInstanceIds = operatedInstanceIds;
            includeIdentity = true;
        }
        else if (_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader
                 && _currentUser.PrimaryCampusId is { } campusId)
        {
            visibleInstanceIds = visit.CampusInstances
                .Where(c => c.CampusId == campusId).Select(c => c.VisitInstanceId).ToList();
        }
        else
        {
            visibleInstanceIds = visit.CampusInstances
                .Where(c => c.CurrentHostUserId == actorId).Select(c => c.VisitInstanceId).ToList();
        }
        if (visibleInstanceIds.Count == 0 && !includeRequestLevel)
            throw new ForbiddenException("Bạn không có quyền xem lịch sử của đơn này.");

        var campusNameByInstance = await BuildCampusNamesAsync(visit, visibleInstanceIds, ct);
        string? CampusOf(ulong? id) =>
            id is { } i && campusNameByInstance.TryGetValue(i, out var n) ? n : null;
        long? CampusIdOf(ulong? id) => visit.CampusInstances
            .Where(c => c.VisitInstanceId == id).Select(c => (long?)c.CampusId).FirstOrDefault();

        return source switch
        {
            VisitHistoryEventSources.InstanceRevision =>
                await InstanceRevisionDetailAsync(visit.VisitRequestId, key, visibleInstanceIds, CampusOf, CampusIdOf, ct),
            VisitHistoryEventSources.RequestRevision =>
                await RequestRevisionDetailAsync(visit.VisitRequestId, key, includeRequestLevel, ct),
            VisitHistoryEventSources.AmendmentSubmitted or VisitHistoryEventSources.AmendmentDecided =>
                await AmendmentDetailAsync(visit.VisitRequestId, key, source, visibleInstanceIds, CampusOf, CampusIdOf, ct),
            VisitHistoryEventSources.Audit =>
                await AuditDetailAsync(visit.VisitRequestId, key, visibleInstanceIds, CampusOf, CampusIdOf, ct),
            VisitHistoryEventSources.IdentityChange =>
                await IdentityDetailAsync(visit.VisitRequestId, key, includeIdentity, visibleInstanceIds, CampusOf, CampusIdOf, ct),
            _ => throw new NotFoundException("Sự kiện lịch sử", 0),
        };
    }

    private async Task<Dictionary<ulong, string?>> BuildCampusNamesAsync(
        Domain.Entities.Delegations.VisitRequest visit, List<ulong> visibleInstanceIds, CancellationToken ct)
    {
        var campusIds = visit.CampusInstances
            .Where(c => visibleInstanceIds.Contains(c.VisitInstanceId))
            .Select(c => c.CampusId).Distinct().ToList();
        var byId = campusIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Campuses.AsNoTracking()
                .Where(c => campusIds.Contains(c.CampusId))
                .ToDictionaryAsync(c => c.CampusId, c => c.Name, ct);
        return visit.CampusInstances
            .Where(c => visibleInstanceIds.Contains(c.VisitInstanceId))
            .ToDictionary(
                c => c.VisitInstanceId,
                c => byId.TryGetValue(c.CampusId, out var n) ? n : (string?)null);
    }

    // ── Per-campus form revision: diff THIS snapshot against the previous one for the same campus ──

    private async Task<VisitHistoryDetailDto> InstanceRevisionDetailAsync(
        ulong visitRequestId, ulong revisionHistoryId, List<ulong> visibleInstanceIds,
        Func<ulong?, string?> campusOf, Func<ulong?, long?> campusIdOf, CancellationToken ct)
    {
        var row = await _db.VisitInstanceFormRevisionHistories.AsNoTracking()
            .Where(r => r.RevisionHistoryId == revisionHistoryId && r.VisitRequestId == visitRequestId)
            .Select(r => new
            {
                r.VisitInstanceId, r.FormRevision, r.ApprovalRevision,
                r.SourceType, r.SnapshotJson, r.AppliedBy, r.AppliedAt,
            })
            .FirstOrDefaultAsync(ct);
        // Out of scope is reported as absent: a 403 on an id the caller guessed would confirm the
        // campus exists, which is precisely what the scoping is there to prevent.
        if (row is null || !visibleInstanceIds.Contains(row.VisitInstanceId))
            throw new NotFoundException("Sự kiện lịch sử", (long)revisionHistoryId);

        var previous = await _db.VisitInstanceFormRevisionHistories.AsNoTracking()
            .Where(r => r.VisitInstanceId == row.VisitInstanceId && r.FormRevision < row.FormRevision)
            .OrderByDescending(r => r.FormRevision)
            .Select(r => new { r.FormRevision, r.SnapshotJson })
            .FirstOrDefaultAsync(ct);

        var (fields, collections) = DiffSnapshots(previous?.SnapshotJson, row.SnapshotJson);

        return new VisitHistoryDetailDto(
            VisitHistoryEventSources.Build(VisitHistoryEventSources.InstanceRevision, revisionHistoryId),
            InstanceRevisionCode(row.SourceType, row.FormRevision),
            row.AppliedAt,
            await NameOfAsync(row.AppliedBy, ct),
            campusIdOf(row.VisitInstanceId),
            campusOf(row.VisitInstanceId),
            // The correlation id stored in `reason` is plumbing, not a business reason — showing it
            // would put a 32-char hex string where the user expects a sentence.
            null,
            previous?.FormRevision,
            row.FormRevision,
            fields,
            collections);
    }

    // ── Request-level revision: the shared registrant/contact block ──

    private async Task<VisitHistoryDetailDto> RequestRevisionDetailAsync(
        ulong visitRequestId, ulong revisionId, bool includeRequestLevel, CancellationToken ct)
    {
        // Request-level content is only ever visible to the requester side and HO.
        if (!includeRequestLevel) throw new NotFoundException("Sự kiện lịch sử", (long)revisionId);

        var row = await _db.VisitRequestRevisionHistories.AsNoTracking()
            .Where(r => r.RequestRevisionHistoryId == revisionId && r.VisitRequestId == visitRequestId)
            .Select(r => new { r.RequestRevision, r.SourceType, r.SnapshotJson, r.AppliedBy, r.AppliedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Sự kiện lịch sử", (long)revisionId);

        var previous = await _db.VisitRequestRevisionHistories.AsNoTracking()
            .Where(r => r.VisitRequestId == visitRequestId && r.RequestRevision < row.RequestRevision)
            .OrderByDescending(r => r.RequestRevision)
            .Select(r => new { r.RequestRevision, r.SnapshotJson })
            .FirstOrDefaultAsync(ct);

        var (fields, _) = DiffSnapshots(previous?.SnapshotJson, row.SnapshotJson);

        return new VisitHistoryDetailDto(
            VisitHistoryEventSources.Build(VisitHistoryEventSources.RequestRevision, revisionId),
            RequestRevisionCode(row.SourceType),
            row.AppliedAt,
            await NameOfAsync(row.AppliedBy, ct),
            null, null, null,
            previous?.RequestRevision,
            row.RequestRevision,
            fields,
            Array.Empty<VisitHistoryCollectionChangeDto>());
    }

    // ── Amendment: the immutable change rows ARE the diff ──

    private async Task<VisitHistoryDetailDto> AmendmentDetailAsync(
        ulong visitRequestId, ulong amendmentId, string source, List<ulong> visibleInstanceIds,
        Func<ulong?, string?> campusOf, Func<ulong?, long?> campusIdOf, CancellationToken ct)
    {
        var amendment = await _db.VisitInstanceAmendments.AsNoTracking()
            .Where(a => a.AmendmentId == amendmentId && a.VisitRequestId == visitRequestId)
            .Select(a => new
            {
                a.VisitInstanceId, a.Status, a.Reason, a.DecisionNote,
                a.RequestedBy, a.RequestedAt, a.DecidedBy, a.DecidedAt,
                a.BaseFormRevision,
            })
            .FirstOrDefaultAsync(ct);
        if (amendment is null || !visibleInstanceIds.Contains(amendment.VisitInstanceId))
            throw new NotFoundException("Sự kiện lịch sử", (long)amendmentId);

        var changes = await _db.VisitInstanceAmendmentChanges.AsNoTracking()
            .Where(c => c.AmendmentId == amendmentId)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new { c.FieldPath, c.OldValueJson, c.NewValueJson })
            .ToListAsync(ct);

        var fields = new List<VisitHistoryFieldChangeDto>();
        var collections = new List<VisitHistoryCollectionChangeDto>();
        foreach (var c in changes)
        {
            if (c.FieldPath == VisitFieldClassifier.Visitors)
                collections.AddRange(DiffMemberJson(VisitHistoryCollectionCodes.Visitors, c.OldValueJson, c.NewValueJson));
            else if (c.FieldPath == VisitFieldClassifier.SupportMembers)
                collections.AddRange(DiffMemberJson(VisitHistoryCollectionCodes.SupportMembers, c.OldValueJson, c.NewValueJson));
            else
                fields.Add(new VisitHistoryFieldChangeDto(
                    c.FieldPath, LabelKeyFor(c.FieldPath), Unquote(c.OldValueJson), Unquote(c.NewValueJson)));
        }

        var decided = source == VisitHistoryEventSources.AmendmentDecided;
        var eventCode = decided
            ? amendment.Status switch
            {
                AmendmentStatuses.Approved => VisitHistoryEventCodes.AmendmentApproved,
                AmendmentStatuses.Rejected => VisitHistoryEventCodes.AmendmentRejected,
                AmendmentStatuses.Withdrawn => VisitHistoryEventCodes.AmendmentWithdrawn,
                _ => VisitHistoryEventCodes.AmendmentDecided,
            }
            : VisitHistoryEventCodes.AmendmentSubmitted;

        return new VisitHistoryDetailDto(
            VisitHistoryEventSources.Build(source, amendmentId),
            eventCode,
            decided ? amendment.DecidedAt ?? amendment.RequestedAt : amendment.RequestedAt,
            await NameOfAsync(decided ? amendment.DecidedBy : amendment.RequestedBy, ct),
            campusIdOf(amendment.VisitInstanceId),
            campusOf(amendment.VisitInstanceId),
            decided ? amendment.DecisionNote : amendment.Reason,
            amendment.BaseFormRevision,
            null,
            fields,
            collections);
    }

    // ── Audit-only events (Host handover) ──

    private async Task<VisitHistoryDetailDto> AuditDetailAsync(
        ulong visitRequestId, ulong auditLogId, List<ulong> visibleInstanceIds,
        Func<ulong?, string?> campusOf, Func<ulong?, long?> campusIdOf, CancellationToken ct)
    {
        // The change rows come through the navigation — audit_log_changes has no DbSet of its own,
        // and adding one just for this read would widen the context's surface for no other caller.
        var audit = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.AuditLogId == auditLogId && a.VisitRequestId == visitRequestId)
            .Select(a => new
            {
                a.Action, a.VisitInstanceId, a.ActorUserId, a.Reason, a.CreatedAt,
                Changes = a.Changes
                    .Select(c => new { c.FieldName, c.OldValueText, c.NewValueText })
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);
        // Only events the timeline itself surfaces are readable here — audit_logs holds a great deal
        // more than the business history, and this endpoint is not a window onto all of it.
        if (audit is null
            || audit.Action != VisitAuditActions.HostTransferred
            || audit.VisitInstanceId is null
            || !visibleInstanceIds.Contains(audit.VisitInstanceId.Value))
            throw new NotFoundException("Sự kiện lịch sử", (long)auditLogId);

        // The names are what a reader needs; the raw user ids beside them would add nothing they can
        // use and would expose internal identifiers for no benefit.
        var fields = audit.Changes
            .Where(c => c.FieldName == "currentHostName")
            .Select(c => new VisitHistoryFieldChangeDto(
                "host", "visitRequestV2:historyDetail.field.host", c.OldValueText, c.NewValueText))
            .ToList();

        return new VisitHistoryDetailDto(
            VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, auditLogId),
            VisitHistoryEventCodes.HostTransferred,
            audit.CreatedAt,
            await NameOfAsync(audit.ActorUserId, ct),
            campusIdOf(audit.VisitInstanceId),
            campusOf(audit.VisitInstanceId),
            audit.Reason,
            null, null,
            fields,
            Array.Empty<VisitHistoryCollectionChangeDto>());
    }

    // ── Contact-identity transitions ─────────────────────────────────────────

    /// <summary>
    /// One row of the append-only identity log: which campus, which address (MASKED), and which way the
    /// invitation moved.
    ///
    /// What is deliberately NOT here: the raw or hashed token, <c>pending_snapshot_json</c>, the
    /// unmasked address, and the row's own <c>reason</c> — which on these rows is usually plumbing
    /// ("EXPIRY_JOB", "token_version=2;resend_count=1") rather than a sentence anybody wrote.
    /// </summary>
    private async Task<VisitHistoryDetailDto> IdentityDetailAsync(
        ulong visitRequestId, ulong identityChangeEventId, bool includeIdentity,
        List<ulong> visibleInstanceIds, Func<ulong?, string?> campusOf, Func<ulong?, long?> campusIdOf,
        CancellationToken ct)
    {
        var row = await _db.VisitRequestIdentityChangeEvents.AsNoTracking()
            .Where(e => e.IdentityChangeEventId == identityChangeEventId
                        && e.VisitRequestId == visitRequestId)
            .Select(e => new
            {
                e.VisitInstanceId, e.EventType, e.FromStatus, e.ToStatus,
                e.ActorUserId, e.EmailMasked, e.CreatedAt,
            })
            .FirstOrDefaultAsync(ct);
        // Out of scope is reported as absent, on the same terms as every other branch here.
        if (row is null || !includeIdentity || !visibleInstanceIds.Contains(row.VisitInstanceId))
            throw new NotFoundException("Sự kiện lịch sử", (long)identityChangeEventId);

        var fields = new List<VisitHistoryFieldChangeDto>();
        if (!string.IsNullOrWhiteSpace(row.EmailMasked))
            fields.Add(new VisitHistoryFieldChangeDto(
                "contactEmailMasked", "visitRequestV2:historyDetail.field.contactEmailMasked",
                null, row.EmailMasked, BeforeUnknown: true));
        if (row.FromStatus is not null || row.ToStatus is not null)
            fields.Add(new VisitHistoryFieldChangeDto(
                "identityChangeStatus", "visitRequestV2:historyDetail.field.identityChangeStatus",
                row.FromStatus, row.ToStatus));

        return new VisitHistoryDetailDto(
            VisitHistoryEventSources.Build(VisitHistoryEventSources.IdentityChange, identityChangeEventId),
            VisitContactIdentityEventCodes.For(row.EventType),
            row.CreatedAt,
            await NameOfAsync(row.ActorUserId, ct),
            campusIdOf(row.VisitInstanceId),
            campusOf(row.VisitInstanceId),
            null, null, null,
            fields,
            Array.Empty<VisitHistoryCollectionChangeDto>());
    }

    // ── Snapshot diffing ─────────────────────────────────────────────────────

    /// <summary>
    /// Compares two stored snapshots and reports only what MOVED. A snapshot holds every field of the
    /// form whether or not it changed, so returning it whole would bury one edited note under twenty
    /// identical lines.
    ///
    /// <para>
    /// Both sides are NORMALIZED first (see <see cref="NormalizeSnapshot"/>). Snapshots written over
    /// the life of this feature do not all have the same shape, and comparing a nested
    /// <c>operationalContact.email</c> against a flat <c>operationalContactEmail</c> by property name
    /// found nothing on the left and reported "(trống) → the address it has always had".
    /// </para>
    /// <para>
    /// Three states are kept apart, because they are three different claims: the old value WAS this,
    /// the old value was empty, and nobody recorded an old value. The last one is what an absent or
    /// <c>{}</c> snapshot means, and it is reported as such rather than as an empty string.
    /// </para>
    /// </summary>
    private static (List<VisitHistoryFieldChangeDto> Fields, List<VisitHistoryCollectionChangeDto> Collections)
        DiffSnapshots(string? beforeJson, string? afterJson)
    {
        var fields = new List<VisitHistoryFieldChangeDto>();
        var collections = new List<VisitHistoryCollectionChangeDto>();

        var after = NormalizeSnapshot(afterJson);
        if (after is null) return (fields, collections);

        // No previous revision row at all: this is a creation. Listing every field as "→ value" would
        // drown the drawer in the request's whole content, so it reports nothing.
        var before = NormalizeSnapshot(beforeJson);
        if (before is null) return (fields, collections);

        foreach (var (code, afterValue) in after.Fields)
        {
            var known = before.Fields.TryGetValue(code, out var beforeValue);
            if (known && string.Equals(beforeValue ?? string.Empty, afterValue ?? string.Empty, StringComparison.Ordinal))
                continue;
            // The previous snapshot simply does not carry this field — an older shape, or an empty
            // blob. Saying "(trống) → X" would assert the field used to be blank; it says instead that
            // there is no history for it.
            if (!known && string.IsNullOrEmpty(afterValue))
                continue;

            fields.Add(new VisitHistoryFieldChangeDto(
                code, LabelKeyFor(code), known ? beforeValue : null, afterValue, BeforeUnknown: !known));
        }

        // Members are only diffed when BOTH sides recorded them. A snapshot that never had a member
        // list is not a snapshot of an empty delegation, and reporting everyone as newly ADDED because
        // the older shape stored them elsewhere would be a fabricated arrival.
        if (after.HasMembers && before.HasMembers)
            collections.AddRange(PairMembers(before.Members, after.Members));

        return (fields, collections);
    }

    /// <summary>
    /// A snapshot reduced to one canonical shape: flat camelCase field codes plus a member list.
    ///
    /// Returns null for "there is no snapshot" — distinct from a snapshot that parsed to nothing,
    /// which comes back as an empty instance and means "recorded, but recorded nothing".
    /// </summary>
    private sealed record NormalizedSnapshot(
        Dictionary<string, string?> Fields, List<MemberRow> Members, bool HasMembers);

    /// <summary>
    /// Historical key spellings mapped onto today's field codes. Everything on the left has been
    /// written into <c>snapshot_json</c> by some version of this feature; without the mapping each one
    /// reads as a field that appeared out of nowhere.
    /// </summary>
    private static readonly Dictionary<string, string> FieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["noteToFptu"] = "notes",
        ["contactPersonFullName"] = "operationalContactFullName",
        ["contactPersonOrganization"] = "operationalContactOrganization",
        ["contactPersonJobTitle"] = "operationalContactJobTitle",
        ["contactPersonPhone"] = "operationalContactPhone",
        ["contactPersonEmail"] = "operationalContactEmail",
    };

    /// <summary>Children of a nested <c>operationalContact</c> object, flattened onto the field codes.</summary>
    private static readonly Dictionary<string, string> ContactChildren = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fullName"] = "operationalContactFullName",
        ["organization"] = "operationalContactOrganization",
        ["jobTitle"] = "operationalContactJobTitle",
        ["phone"] = "operationalContactPhone",
        ["email"] = "operationalContactEmail",
    };

    private static NormalizedSnapshot? NormalizeSnapshot(string? json)
    {
        if (json is null) return null;
        var parsed = Parse(json);
        if (parsed is not { ValueKind: JsonValueKind.Object } root)
            // Present but unreadable: recorded nothing we can compare against, which is not the same
            // as never having been recorded.
            return new NormalizedSnapshot(new Dictionary<string, string?>(StringComparer.Ordinal), new List<MemberRow>(), false);

        var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
        var members = new List<MemberRow>();
        var hasMembers = false;

        foreach (var property in root.EnumerateObject())
        {
            var name = property.Name;

            // Members arrive as one mixed list (discriminated by MemberType) or as two separate ones,
            // and the key has been spelled both "Members" and "members" — a case-sensitive lookup for
            // one spelling is why member changes stopped appearing in revision diffs at all.
            if (name.Equals("members", StringComparison.OrdinalIgnoreCase))
            {
                members.AddRange(MemberRows(property.Value));
                hasMembers = true;
                continue;
            }
            if (name.Equals("visitors", StringComparison.OrdinalIgnoreCase))
            {
                members.AddRange(ReadMemberArray(property.Value, VisitHistoryCollectionCodes.Visitors));
                hasMembers = true;
                continue;
            }
            if (name.Equals("supportMembers", StringComparison.OrdinalIgnoreCase)
                || name.Equals("externalSupportMembers", StringComparison.OrdinalIgnoreCase))
            {
                members.AddRange(ReadMemberArray(property.Value, VisitHistoryCollectionCodes.SupportMembers));
                hasMembers = true;
                continue;
            }

            if (name.Equals("operationalContact", StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var child in property.Value.EnumerateObject())
                    if (ContactChildren.TryGetValue(child.Name, out var childCode))
                        fields[childCode] = Scalar(child.Value);
                continue;
            }

            // Objects and arrays that are not one of the shapes above have no scalar rendering, and a
            // drawer row reading "[object]" helps nobody.
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) continue;

            var code = FieldAliases.TryGetValue(name, out var alias) ? alias : Camel(name);
            fields[code] = Scalar(property.Value);
        }

        return new NormalizedSnapshot(fields, members, hasMembers);
    }

    private static IEnumerable<VisitHistoryCollectionChangeDto> DiffMemberJson(
        string collectionCode, string? beforeJson, string? afterJson)
    {
        var before = Parse(beforeJson);
        var after = Parse(afterJson);
        var beforeRows = before is null ? new List<MemberRow>() : ReadMemberArray(before.Value, collectionCode);
        var afterRows = after is null ? new List<MemberRow>() : ReadMemberArray(after.Value, collectionCode);
        return PairMembers(beforeRows, afterRows);
    }

    private sealed record MemberRow(string Collection, string Name, Dictionary<string, string> Values);

    private static List<MemberRow> MemberRows(JsonElement? element)
    {
        var rows = new List<MemberRow>();
        if (element is not { ValueKind: JsonValueKind.Array } array) return rows;
        foreach (var item in array.EnumerateArray())
        {
            var type = item.TryGetProperty("MemberType", out var mt) ? mt.GetString() : null;
            var collection = type == "EXTERNAL_SUPPORT"
                ? VisitHistoryCollectionCodes.SupportMembers
                : VisitHistoryCollectionCodes.Visitors;
            rows.Add(ToMemberRow(collection, item));
        }
        return rows;
    }

    private static List<MemberRow> ReadMemberArray(JsonElement element, string collection)
    {
        var rows = new List<MemberRow>();
        if (element.ValueKind != JsonValueKind.Array) return rows;
        foreach (var item in element.EnumerateArray()) rows.Add(ToMemberRow(collection, item));
        return rows;
    }

    private static MemberRow ToMemberRow(string collection, JsonElement item)
    {
        var values = new Dictionary<string, string>();
        foreach (var name in new[] { "FullName", "Organization", "JobTitle", "Nationality" })
        {
            // Snapshots and amendment payloads use different casing for the same fields; matching
            // case-insensitively keeps one differ working for both rather than two near-copies.
            var value = item.TryGetProperty(name, out var v) ? Scalar(v)
                : item.TryGetProperty(Camel(name), out var v2) ? Scalar(v2)
                : null;
            if (!string.IsNullOrWhiteSpace(value)) values[Camel(name)] = value!;
        }
        values.TryGetValue("fullName", out var fullName);
        return new MemberRow(collection, fullName ?? string.Empty, values);
    }

    /// <summary>
    /// Pairs people by name: same name on both sides with different details is an UPDATE, a name only
    /// on the right joined, a name only on the left left. Name is the only stable handle available —
    /// members are replaced wholesale on an edit, so their ids do not survive.
    /// </summary>
    private static IEnumerable<VisitHistoryCollectionChangeDto> PairMembers(
        List<MemberRow> before, List<MemberRow> after)
    {
        var result = new List<VisitHistoryCollectionChangeDto>();
        var collections = before.Select(r => r.Collection)
            .Concat(after.Select(r => r.Collection)).Distinct();

        foreach (var collection in collections)
        {
            var b = before.Where(r => r.Collection == collection).ToList();
            var a = after.Where(r => r.Collection == collection).ToList();
            var names = b.Select(r => r.Name).Concat(a.Select(r => r.Name)).Distinct(StringComparer.Ordinal);

            foreach (var name in names)
            {
                var beforeRow = b.FirstOrDefault(r => r.Name == name);
                var afterRow = a.FirstOrDefault(r => r.Name == name);

                if (beforeRow is null && afterRow is not null)
                    result.Add(new VisitHistoryCollectionChangeDto(
                        collection, VisitHistoryChangeTypes.Added, name, null, afterRow.Values));
                else if (beforeRow is not null && afterRow is null)
                    result.Add(new VisitHistoryCollectionChangeDto(
                        collection, VisitHistoryChangeTypes.Removed, name, beforeRow.Values, null));
                else if (beforeRow is not null && afterRow is not null
                         && !SameValues(beforeRow.Values, afterRow.Values))
                    result.Add(new VisitHistoryCollectionChangeDto(
                        collection, VisitHistoryChangeTypes.Updated, name, beforeRow.Values, afterRow.Values));
            }
        }
        return result;
    }

    private static bool SameValues(Dictionary<string, string> a, Dictionary<string, string> b)
        => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(json, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>A JSON value as display text; objects/arrays are not scalars and are skipped.</summary>
    private static string? Scalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => null,
    };

    /// <summary>Amendment rows store scalars as JSON, so a plain string arrives quoted.</summary>
    private static string? Unquote(string? json)
    {
        var parsed = Parse(json);
        return parsed is null ? null : Scalar(parsed.Value);
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>Translation key for a field, so the drawer never shows a raw column name.</summary>
    private static string LabelKeyFor(string fieldCode)
        => $"visitRequestV2:historyDetail.field.{Camel(fieldCode.Split('.').Last())}";

    private async Task<string?> NameOfAsync(ulong? userId, CancellationToken ct)
        => userId is null
            ? null
            : await _db.Users.AsNoTracking()
                .Where(u => u.UserId == userId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct);

    private static string InstanceRevisionCode(string? sourceType, uint formRevision) => sourceType switch
    {
        FormRevisionSourceTypes.Create => VisitHistoryEventCodes.InstanceContentCreated,
        FormRevisionSourceTypes.SafeEdit => VisitHistoryEventCodes.InstanceSafeEditApplied,
        FormRevisionSourceTypes.PendingEdit => VisitHistoryEventCodes.InstancePendingEditApplied,
        FormRevisionSourceTypes.Resubmit => VisitHistoryEventCodes.InstanceContentResubmitted,
        FormRevisionSourceTypes.AmendmentApplied => VisitHistoryEventCodes.InstanceAmendmentApplied,
        _ => formRevision <= 1
            ? VisitHistoryEventCodes.InstanceContentCreated
            : VisitHistoryEventCodes.InstanceContentRevised,
    };

    private static string RequestRevisionCode(string? sourceType) => sourceType switch
    {
        FormRevisionSourceTypes.Create => VisitHistoryEventCodes.RequestCreated,
        FormRevisionSourceTypes.SafeEdit => VisitHistoryEventCodes.RequestSafeEditApplied,
        FormRevisionSourceTypes.PendingEdit => VisitHistoryEventCodes.RequestPendingEditApplied,
        FormRevisionSourceTypes.Resubmit => VisitHistoryEventCodes.RequestResubmitted,
        _ => VisitHistoryEventCodes.RequestRevision,
    };
}

