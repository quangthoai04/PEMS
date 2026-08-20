using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Validation;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Policies;
using PEMS.Shared;

namespace PEMS.Infrastructure.Services;

/// <summary>See <see cref="IVisitSafeEditService"/>. The classifier (<see cref="VisitFieldClassifier"/>)
/// is the single gate: anything outside SAFE/PRIVACY_URGENT fails closed here regardless of what the
/// endpoint accepted structurally.</summary>
public sealed class VisitSafeEditService : IVisitSafeEditService
{
    private readonly IApplicationDbContext _db;

    public VisitSafeEditService(IApplicationDbContext db) => _db = db;

    private sealed record Change(
        string FieldPath, ulong? InstanceId, string? OldValue, string? NewValue, string Class,
        Action Apply);

    public async Task<VisitRequestSafeEditResponse> ApplySafeEditAsync(
        VisitRequest request, VisitRequestSafeEditDto patch, ulong actorId, DateTime now,
        CancellationToken ct)
    {
        // ── 0. FOR-UPDATE row-version guard (same discipline as the v2 edit flows) ──
        var currentVersion = (await _db.VisitRequests
                .FromSqlRaw("SELECT * FROM visit_requests WHERE visit_request_id = {0} FOR UPDATE",
                    request.VisitRequestId)
                .AsNoTracking()
                .Select(v => (int?)v.RowVersion)
                .ToListAsync(ct))
            .Single();
        if (currentVersion != patch.ExpectedRequestRowVersion || request.RowVersion != currentVersion)
            throw new ConflictException(
                "Đơn đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                VisitFormV2ErrorCodes.VisitFormConcurrencyConflict);

        var changes = new List<Change>();

        // Campus names for refusal messages — a multi-campus user needs to know WHICH campus closed the
        // window, and "Không thể sửa" without a name sends them to look at all of them.
        var campusIds = request.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        var campusNames = await _db.Campuses.AsNoTracking()
            .Where(c => campusIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, ct);

        // ── 1. Request-level safe subset (registrant display fields + contact snapshot, never emails) ──
        if (patch.Registrant is { } reg)
        {
            if (string.IsNullOrWhiteSpace(reg.FullName))
                throw new BusinessRuleException("Họ tên người đăng ký không được để trống.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
            if (string.IsNullOrWhiteSpace(reg.Nationality))
                throw new BusinessRuleException("Quốc tịch người đăng ký không được để trống.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

            // Partner identity travels ATOMICALLY with the organization text: the client sends only an
            // id, and the canonical display name is ALWAYS resolved here, server-side — never trusted
            // from client-supplied text sitting next to a client-supplied id (plan §3.3/§7.4).
            var registrantOrg = Clean(reg.Organization);
            if (reg.PartnerId.HasValue)
            {
                await GuestOrganizationPartnerPolicy.EnsureRequestFormSelectableAsync(
                    _db, new[] { reg.PartnerId.Value }, ct);
                var partner = await _db.Partners.AsNoTracking()
                    .FirstAsync(p => p.PartnerId == reg.PartnerId.Value, ct);
                registrantOrg = string.IsNullOrWhiteSpace(partner.ShortName)
                    ? partner.Name : $"{partner.Name} ({partner.ShortName})";
            }

            Diff(changes, VisitFieldClassifier.RegistrantFullName, null,
                request.RegistrantFullName, reg.FullName.Trim(), v => request.RegistrantFullName = v!);

            // Nationality (Patch 4): compared in CANONICAL space, not raw text — mirrors
            // VisitRequestV2EditService.ApplyCommonFields. A safe edit that only touches, say, the
            // organization must not be blocked — or silently re-cased/rewritten — just because the
            // registrant's legacy nationality spelling does not match the canonical form byte for
            // byte. Nothing is written or diffed unless the two sides resolve to DIFFERENT countries;
            // only then must the new one resolve at all.
            var trimmedNationality = reg.Nationality.Trim();
            var nationalityResolved = CountryName.TryResolve(trimmedNationality, out var nationalityCanonical);
            var currentNationalityResolved = CountryName.TryResolve(request.RegistrantNationality, out var currentNationalityCanonical);
            var effectiveCurrentNationality = currentNationalityResolved ? currentNationalityCanonical! : request.RegistrantNationality;
            var effectiveIncomingNationality = nationalityResolved ? nationalityCanonical! : trimmedNationality;
            if (!string.Equals(effectiveCurrentNationality, effectiveIncomingNationality, StringComparison.Ordinal))
            {
                if (!nationalityResolved)
                    throw new BusinessRuleException(
                        $"Quốc tịch người đăng ký không hợp lệ: '{trimmedNationality}'. {CountryName.FormatHint}",
                        VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
                Diff(changes, VisitFieldClassifier.RegistrantNationality, null,
                    request.RegistrantNationality, nationalityCanonical, v => request.RegistrantNationality = v!);
            }
            Diff(changes, VisitFieldClassifier.RegistrantOrganization, null,
                request.RegistrantOrganization, registrantOrg, v => request.RegistrantOrganization = v);
            Diff(changes, VisitFieldClassifier.RegistrantPartnerId, null,
                request.PartnerId?.ToString(), reg.PartnerId?.ToString(),
                v => request.PartnerId = v is null ? (ulong?)null : ulong.Parse(v));
            Diff(changes, VisitFieldClassifier.RegistrantJobTitle, null,
                request.RegistrantJobTitle, Clean(reg.JobTitle), v => request.RegistrantJobTitle = v);

            // Validated AND normalized here, not just trusted from the (already-checked) validator: the
            // classifier/Diff pipeline below has no format opinion of its own, so an invalid value that
            // somehow reached this far (a caller bypassing the MediatR pipeline in a test, a future
            // refactor) must still be refused rather than silently persisted as typed — the exact
            // regression this guards against was a Safe Edit call storing "+821012340001123213sd"
            // verbatim because nothing between the wire and the column ever checked its shape.
            var cleanedPhone = Clean(reg.Phone);
            string? newPhone = null;
            if (cleanedPhone is not null && !PhoneNumber.TryNormalize(cleanedPhone, out newPhone))
                throw new BusinessRuleException(
                    $"Số điện thoại người đăng ký không hợp lệ. {PhoneNumberRules.FormatHint}",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
            Diff(changes, VisitFieldClassifier.RegistrantPhone, null,
                request.RegistrantPhone, newPhone, v => request.RegistrantPhone = v);
        }

        // ── 2. Per-instance safe subset ──
        var touchedInstances = new List<VisitRequestCampus>();
        foreach (var ip in patch.Instances ?? new List<SafeInstancePatchDto>())
        {
            var instance = request.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == ip.VisitInstanceId)
                ?? throw new BusinessRuleException("Cơ sở được sửa không thuộc đơn này.",
                    VisitFormV2ErrorCodes.VisitInstanceScopeForbidden);
            if (ip.ExpectedRowVersion != instance.RowVersion)
                throw new ConflictException(
                    "Lịch thăm tại một cơ sở đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                    VisitFormV2ErrorCodes.VisitFormConcurrencyConflict);

            // LIFECYCLE + CUTOFF for THIS campus only — a sibling that is under way says nothing about a
            // campus still days out, and coupling them is what let one campus's timing freeze another's
            // notes. Both halves are checked here, in one call: the cutoff used to be deferred until the
            // changes were known so a privacy-urgent media withdrawal could skip it, and that exception
            // is gone (§38) — every safe edit answers to the same six hours.
            //
            // WAITING_REQUEST_APPROVAL is deliberately NOT accepted here: a still-pending campus belongs
            // to per-campus pending-edit, which can change anything. Offering the narrow tool as well
            // only made it unclear which one to reach for.
            VisitMutationGuard.EnsureAllowed(
                VisitMutationAction.SubmitSafeEdit, request.Status, instance, now,
                VisitViewerRelations.Requester, VisitRequestErrorCodes.VisitRequestNotEditable,
                campusNames);

            var detail = instance.FormDetail
                ?? throw new ConflictException("Thiếu dữ liệu biểu mẫu theo cơ sở.",
                    VisitFormV2ErrorCodes.VisitFormDetailMissing);

            if (ip.MediaConsentStatus is not (null or "AGREED" or "DECLINED"))
                throw new BusinessRuleException("Trạng thái truyền thông không hợp lệ.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

            // Every instance field is OPTIONAL: null means "not part of this edit", which is how the
            // client sends only what changed. Previously all four were mandatory, so a one-word note
            // correction re-submitted the media-consent decision of every campus in the request — and a
            // stale value in the form would silently overwrite a newer one.
            var before = changes.Count;
            if (ip.TransportationNote is not null)
                Diff(changes, VisitFieldClassifier.TransportationNote, instance.VisitInstanceId,
                    detail.TransportationNote, Clean(ip.TransportationNote), v => detail.TransportationNote = v);
            // RETIRED (plan PEMS_CONTACT_ONE_DOOR): the contact profile has exactly one door now —
            // "Manage the contact role". The current client never sends this; an old/handcrafted
            // client that still does is refused outright rather than applied or silently dropped, so a
            // stale caller finds out immediately instead of believing a correction went through.
            if (ip.OperationalContact is not null)
                throw new BusinessRuleException(
                    "Thông tin đầu mối vận hành (họ tên/tổ chức/chức danh/điện thoại) không thể sửa qua Sửa nhanh. " +
                    "Hãy dùng chức năng \"Chỉnh sửa đầu mối\" của cơ sở.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
            if (ip.Notes is not null)
                Diff(changes, VisitFieldClassifier.Notes, instance.VisitInstanceId,
                    detail.Notes, Clean(ip.Notes), v => detail.Notes = v);
            if (ip.MediaConsentStatus is not null)
                Diff(changes, VisitFieldClassifier.MediaConsentStatus, instance.VisitInstanceId,
                    detail.MediaConsentStatus, ip.MediaConsentStatus, v => detail.MediaConsentStatus = v!);
            if (changes.Count > before)
                touchedInstances.Add(instance);
        }

        if (changes.Count == 0)
            throw new BusinessRuleException("Không có thay đổi nào để áp dụng.",
                VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

        // ── 3. Classifier gate — fail closed on anything that is not SAFE/PRIVACY_URGENT ──
        foreach (var c in changes)
            if (c.Class is not (AmendmentChangeClasses.Safe or AmendmentChangeClasses.PrivacyUrgent))
                throw new BusinessRuleException(
                    $"Trường '{c.FieldPath}' không thuộc nhóm sửa nhanh. Vui lòng gửi đề xuất thay đổi (amendment).",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

        // ── 4. Request-level scope. The registrant/contact block is SHARED by every campus, so it is
        //        all-or-nothing: refused outright while any campus has passed the point of no return
        //        (its delegation is already on site reading that name), and the deadline comes from the
        //        earliest campus still ahead.
        //
        //        The old check asked only "is the earliest ACTIVE campus < 24h away", and skipped
        //        entirely when that set was EMPTY — so once every campus had moved to DURING_VISIT the
        //        guard evaluated `earliest is null` and let the edit straight through. An empty set now
        //        falls back to the earliest campus overall, never to "no campus, allow".
        if (changes.Any(c => c.InstanceId is null))
        {
            VisitMutationGuard.EnsureRequestLevelAllowed(
                VisitMutationAction.SubmitSafeEdit, request, now,
                c => c.Status is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit,
                VisitRequestErrorCodes.VisitRequestNotEditable,
                campusNames);
        }

        // ── 5. Apply + revisions + audit, one commit ──
        var correlationId = Guid.NewGuid().ToString("N");

        // Baselines BEFORE `Apply()` writes anything. Every touched campus is about to reach revision
        // N+1, and the request block is about to be rewritten — so this is the last point at which the
        // "before" values still exist to be recorded. A safe edit is exactly the case that used to
        // produce an empty drawer: it changes a note or a phone number, and with no revision N to diff
        // against, the history reported "no recorded changes" for a change the user had just made.
        var requestBaselineJson = VisitRevisionBaselineGuard.CaptureRequestSnapshot(request);
        foreach (var instance in touchedInstances)
            if (instance.FormDetail is { } beforeDetail)
                await VisitRevisionBaselineGuard.EnsureInstanceBaselineAsync(
                    _db, request, instance, beforeDetail, actorId, now, ct);

        foreach (var c in changes) c.Apply();

        foreach (var instance in touchedInstances)
        {
            var detail = instance.FormDetail!;
            detail.FormRevision += 1;
            detail.RowVersion += 1;
            detail.UpdatedAt = now;
            detail.UpdatedBy = actorId;
            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;

            var members = MembersOf(request, instance);
            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = detail.FormRevision,
                ApprovalRevision = detail.ApprovalRevision,
                SourceType = FormRevisionSourceTypes.SafeEdit,
                SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, detail, members),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        if (changes.Any(c => c.InstanceId is null))
        {
            await VisitRevisionBaselineGuard.EnsureRequestBaselineAsync(
                _db, request, requestBaselineJson, actorId, now, ct);

            // NOT a raw MaxAsync: EnsureRequestBaselineAsync just staged (unflushed) a possible
            // RECOVERED_BASELINE row two lines above, which a database MAX cannot see yet — the
            // shared helper unions the DB max with EF's own .Local staged rows so the two can never
            // collide on the same revision number (see VisitRevisionBaselineGuard for detail).
            var nextRevision = await VisitRevisionBaselineGuard.NextRequestRevisionAsync(
                _db, request.VisitRequestId, ct);
            _db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                RequestRevision = nextRevision,
                SourceType = FormRevisionSourceTypes.SafeEdit,
                SnapshotJson = VisitFormRevisionSnapshotBuilder.Request(request),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        // ── 6. Canonical recompute (instance copyable content may flip has_mixed / the projection) ──
        if (touchedInstances.Count > 0)
            await V2CanonicalRefresh.RecomputeAsync(_db, request, ct);

        request.RowVersion += 1;
        request.UpdatedAt = now;
        request.UpdatedBy = actorId;

        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "VISIT_SAFE_FIELDS_UPDATED",
            EntityType = "VisitRequest",
            EntityId = request.VisitRequestId,
            VisitRequestId = request.VisitRequestId,
            CorrelationId = correlationId,
            SourceType = FormRevisionSourceTypes.SafeEdit,
            CreatedAt = now,
        };
        foreach (var c in changes)
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = c.InstanceId is null ? c.FieldPath : $"{c.FieldPath}#{c.InstanceId}",
                OldValueText = c.OldValue,
                NewValueText = c.NewValue,
                CreatedAt = now,
            });
        _db.AuditLogs.Add(audit);

        await _db.SaveChangesAsync(ct);

        return new VisitRequestSafeEditResponse(
            request.VisitRequestId,
            changes.Select(c => new SafeEditAppliedChange(c.FieldPath, c.InstanceId, c.Class)).ToList(),
            request.RowVersion,
            request.CampusInstances.ToDictionary(c => c.VisitInstanceId, c => c.RowVersion),
            "Đã cập nhật các thông tin cho phép sửa nhanh.");
    }

    /// <summary>True when the safe edit contains a PRIVACY_URGENT media withdrawal (for HIGH-priority notify).</summary>
    public static bool HasPrivacyUrgent(VisitRequestSafeEditResponse response)
        => response.AppliedChanges.Any(c => c.ChangeClass == AmendmentChangeClasses.PrivacyUrgent);

    private static void Diff(
        List<Change> changes, string fieldPath, ulong? instanceId,
        string? oldValue, string? newValue, Action<string?> apply)
    {
        if (string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
            return;
        var cls = VisitFieldClassifier.ClassifyChange(fieldPath, oldValue, newValue)
            ?? throw new BusinessRuleException(
                $"Trường '{fieldPath}' không được hỗ trợ.", VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
        changes.Add(new Change(fieldPath, instanceId, oldValue, newValue, cls, () => apply(newValue)));
    }

    private static List<VisitGuestMember> MembersOf(VisitRequest request, VisitRequestCampus instance)
        => V2CanonicalRefresh.MembersOf(request, instance);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// Rebuilds a request's canonical scope/mixed/fingerprint/compatibility-projection from the CURRENT
/// persisted per-campus state (details + linked members). Shared by the safe-edit and amendment services —
/// unlike the pending-edit flow there is no payload covering all campuses, so the source is the entities.
/// </summary>
internal static class V2CanonicalRefresh
{
    public static async Task RecomputeAsync(IApplicationDbContext db, VisitRequest request, CancellationToken ct)
    {
        var campusIds = request.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        var codesById = await db.Campuses.AsNoTracking()
            .Where(c => campusIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.CampusCode, ct);

        var contents = request.CampusInstances
            .OrderBy(c => c.CampusId)
            .Select(c => ToFormDto(request, c, codesById.TryGetValue(c.CampusId, out var code) ? code : c.CampusId.ToString()))
            .ToList();

        var scope = VisitRequestV2Canonical.ScopeOf(contents.Count);
        var hasMixed = VisitRequestV2Canonical.ComputeHasMixed(contents);
        var fingerprint = VisitRequestV2Canonical.BuildFingerprint(
            VisitRequestFingerprintBuilder.NormalizeEmail(request.RegistrantEmail),
            scope, contents);

        // Pure V2: form content lives ONLY in each campus's visit_instance_form_details. The request row
        // keeps identity, scope and lifecycle — it no longer mirrors one campus's content, so a mixed
        // request can never leak the smallest campus's values as if they were request-wide.
        request.VisitScope = scope;
        request.HasMixedCampusDetails = hasMixed;
        request.BusinessFingerprint = fingerprint;
    }

    public static List<VisitGuestMember> MembersOf(VisitRequest request, VisitRequestCampus instance)
    {
        var ids = instance.GuestMemberLinks.Select(l => l.GuestMemberId).ToHashSet();
        return request.GuestMembers.Where(m => ids.Contains(m.GuestMemberId))
            .OrderBy(m => m.DisplayOrder).ToList();
    }

    public static CampusVisitFormDto ToFormDto(VisitRequest request, VisitRequestCampus instance, string campusCode)
    {
        var d = instance.FormDetail!;
        var members = MembersOf(request, instance);
        return new CampusVisitFormDto(
            campusCode, instance.PlannedStartAt, instance.PlannedEndAt,
            d.DelegationName, d.VisitType ?? string.Empty, d.VisitTypeOther, d.Purpose ?? string.Empty, d.WorkingContent,
            members.Where(m => m.MemberType == "GUEST")
                .Select(m => new VisitorDto(m.FullName, m.Nationality ?? string.Empty, m.JobTitle ?? string.Empty,
                    m.Organization ?? string.Empty, m.OrganizationPartnerId)).ToList(),
            members.Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .Select(m => new SupportTeamMemberDto(m.FullName, m.JobTitle ?? string.Empty, m.Organization ?? string.Empty,
                    m.Nationality ?? string.Empty, m.OrganizationPartnerId)).ToList(),
            new ContactPointDto(
                d.OperationalContactFullName, d.OperationalContactOrganization ?? string.Empty,
                d.OperationalContactJobTitle, d.OperationalContactPhone, d.OperationalContactEmail),
            d.WorkingLanguage ?? "EN", d.TransportationNote, d.MediaConsentStatus ?? "DECLINED", d.Notes,
            null);
    }
}
