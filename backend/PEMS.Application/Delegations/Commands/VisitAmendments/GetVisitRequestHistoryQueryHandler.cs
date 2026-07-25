using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.VisitAmendments;

/// <summary>
/// Scoped business-history timeline (plan §16.6 / handoff §7.7). METADATA ONLY — no snapshot JSON, no
/// tokens/IP/UA, no full emails (identity entries surface the MASKED email). Scope:
///   • registrant / ACTIVE primary contact → whole own request (incl. identity timeline);
///   • HO → whole request read-only (incl. masked identity timeline);
///   • Staff Leader → only instances of their primary campus (no identity timeline);
///   • current Host → only their instance(s) (no identity timeline);
///   • anyone else → 403.
/// Proposed amendments are clearly distinct from applied revisions — a proposal is never presented as
/// active content.
/// </summary>
public sealed class GetVisitRequestHistoryQueryHandler
    : IRequestHandler<GetVisitRequestHistoryQuery, VisitRequestHistoryResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PerCampusFormV2Options _readFlag;

    public GetVisitRequestHistoryQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, PerCampusFormV2Options readFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _readFlag = readFlag;
    }

    public async Task<VisitRequestHistoryResponse> Handle(
        GetVisitRequestHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!_readFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var actorId = _currentUser.UserId.Value;

        var visit = await _db.VisitRequests.AsNoTracking()
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        // ── Scope resolution BEFORE any projection ──
        var isManager = visit.RegistrantUserId == actorId
            || (visit.VisitorUserId == actorId
                && visit.PrimaryContactAccessStatus == PrimaryContactAccessStatuses.Active);
        var isHo = _currentUser.RoleCode == RoleCodes.Ho;
        List<ulong> visibleInstanceIds;
        var includeIdentity = false;
        var includeRequestLevel = false;
        if (isManager || isHo)
        {
            visibleInstanceIds = visit.CampusInstances.Select(c => c.VisitInstanceId).ToList();
            includeIdentity = true;
            includeRequestLevel = true;
        }
        else if (_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader
                 && _currentUser.PrimaryCampusId is { } campusId)
        {
            visibleInstanceIds = visit.CampusInstances
                .Where(c => c.CampusId == campusId)
                .Select(c => c.VisitInstanceId).ToList();
        }
        else
        {
            visibleInstanceIds = visit.CampusInstances
                .Where(c => c.CurrentHostUserId == actorId)
                .Select(c => c.VisitInstanceId).ToList();
        }
        if (visibleInstanceIds.Count == 0 && !includeRequestLevel)
            throw new ForbiddenException("Bạn không có quyền xem lịch sử của đơn này.");

        // ── Campus names for the visible instances ──
        // Without these, a three-campus request emits three identical "content created" rows and the
        // reader cannot tell which campus each one belongs to.
        var visibleCampusIds = visit.CampusInstances
            .Where(c => visibleInstanceIds.Contains(c.VisitInstanceId))
            .Select(c => c.CampusId).Distinct().ToList();
        var campusNameById = visibleCampusIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Campuses.AsNoTracking()
                .Where(c => visibleCampusIds.Contains(c.CampusId))
                .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);
        var campusNameByInstance = visit.CampusInstances
            .Where(c => visibleInstanceIds.Contains(c.VisitInstanceId))
            .ToDictionary(
                c => c.VisitInstanceId,
                c => campusNameById.TryGetValue(c.CampusId, out var n) ? n : null);
        string? CampusOf(ulong? instanceId) =>
            instanceId is { } id && campusNameByInstance.TryGetValue(id, out var n) ? n : null;

        // Raw facts are collected first, then names are attached in one pass — the previous version
        // built the name dictionary and then never used it, so every entry went out with a null actor.
        var raw = new List<(VisitHistoryEntryDto Entry, ulong? ActorId)>();
        var actorIds = new HashSet<ulong>();

        void Add(VisitHistoryEntryDto entry, ulong? actorId)
        {
            if (actorId is { } id) actorIds.Add(id);
            raw.Add((entry, actorId));
        }

        // ── Instance revisions (applied, immutable) ──
        var instanceRevisions = await _db.VisitInstanceFormRevisionHistories.AsNoTracking()
            .Where(r => r.VisitRequestId == visit.VisitRequestId
                        && visibleInstanceIds.Contains(r.VisitInstanceId))
            .Select(r => new { r.VisitInstanceId, r.FormRevision, r.ApprovalRevision, r.SourceType, r.AppliedBy, r.AppliedAt })
            .ToListAsync(cancellationToken);
        foreach (var r in instanceRevisions)
        {
            Add(new VisitHistoryEntryDto(
                r.AppliedAt, InstanceRevisionCode(r.SourceType, r.FormRevision),
                r.VisitInstanceId, CampusOf(r.VisitInstanceId), null,
                r.FormRevision, r.ApprovalRevision, null, null, r.SourceType, null, null, null, null),
                r.AppliedBy);
        }

        // ── Request-level revisions ──
        if (includeRequestLevel)
        {
            var requestRevisions = await _db.VisitRequestRevisionHistories.AsNoTracking()
                .Where(r => r.VisitRequestId == visit.VisitRequestId)
                .Select(r => new { r.RequestRevision, r.SourceType, r.AppliedBy, r.AppliedAt })
                .ToListAsync(cancellationToken);
            foreach (var r in requestRevisions)
            {
                Add(new VisitHistoryEntryDto(
                    r.AppliedAt, RequestRevisionCode(r.SourceType), null, null, null,
                    r.RequestRevision, null, null, null, r.SourceType, null, null, null, null),
                    r.AppliedBy);
            }

            // Cancelling the whole request is recorded on the request row itself, not in a revision
            // table — so without this the timeline simply stopped at the last edit and never said the
            // request had been called off, by whom, or why.
            if (visit.CancelledAt is { } requestCancelledAt)
            {
                Add(new VisitHistoryEntryDto(
                    requestCancelledAt, VisitHistoryEventCodes.RequestCancelled, null, null, null,
                    null, null, null, VisitRequestStatuses.Cancelled, null,
                    visit.CancellationReason, null, null, null),
                    visit.CancelledBy);
            }
        }

        // ── Amendments (proposals + decisions — NEVER presented as active content) ──
        var amendments = await _db.VisitInstanceAmendments.AsNoTracking()
            .Where(a => a.VisitRequestId == visit.VisitRequestId
                        && visibleInstanceIds.Contains(a.VisitInstanceId))
            .Select(a => new { a.VisitInstanceId, a.AmendmentNo, a.Status, a.RequestedBy, a.RequestedAt, a.DecidedBy, a.DecidedAt, a.DecisionNote })
            .ToListAsync(cancellationToken);
        foreach (var a in amendments)
        {
            Add(new VisitHistoryEntryDto(
                a.RequestedAt, VisitHistoryEventCodes.AmendmentSubmitted, a.VisitInstanceId,
                CampusOf(a.VisitInstanceId), null, null, null, a.AmendmentNo,
                null, null, null, null, null, null),
                a.RequestedBy);

            if (a.DecidedAt is { } decidedAt)
            {
                var code = a.Status switch
                {
                    AmendmentStatuses.Approved => VisitHistoryEventCodes.AmendmentApproved,
                    AmendmentStatuses.Rejected => VisitHistoryEventCodes.AmendmentRejected,
                    AmendmentStatuses.Withdrawn => VisitHistoryEventCodes.AmendmentWithdrawn,
                    _ => VisitHistoryEventCodes.AmendmentDecided,
                };
                Add(new VisitHistoryEntryDto(
                    decidedAt, code, a.VisitInstanceId, CampusOf(a.VisitInstanceId), null,
                    null, null, a.AmendmentNo, a.Status, null, a.DecisionNote, null, null, null),
                    a.DecidedBy);
            }
        }

        // ── Campus decisions + cancellations ──
        foreach (var c in visit.CampusInstances.Where(c => visibleInstanceIds.Contains(c.VisitInstanceId)))
        {
            if (c.DecidedAt is { } decidedAt)
            {
                var code = c.Status switch
                {
                    VisitInstanceStatuses.Rejected => VisitHistoryEventCodes.InstanceRejected,
                    VisitInstanceStatuses.Assigned => VisitHistoryEventCodes.InstanceApproved,
                    // A campus that has moved on (BEFORE/DURING/AFTER/CLOSED) was approved to get there;
                    // the decision event still describes that approval.
                    VisitInstanceStatuses.BeforeVisit or VisitInstanceStatuses.DuringVisit
                        or VisitInstanceStatuses.AfterVisit or VisitInstanceStatuses.Closed
                        => VisitHistoryEventCodes.InstanceApproved,
                    _ => VisitHistoryEventCodes.InstanceDecided,
                };
                Add(new VisitHistoryEntryDto(
                    decidedAt, code, c.VisitInstanceId, CampusOf(c.VisitInstanceId), null,
                    null, null, null, c.Status, null, c.DecisionNote, null, null, null),
                    c.DecidedBy);
            }

            // Cancelling a campus is a separate event from deciding it, and used to be invisible here.
            if (c.CancelledAt is { } cancelledAt)
            {
                Add(new VisitHistoryEntryDto(
                    cancelledAt, VisitHistoryEventCodes.InstanceCancelled, c.VisitInstanceId,
                    CampusOf(c.VisitInstanceId), null, null, null, null,
                    VisitInstanceStatuses.Cancelled, c.CancellationSource, c.CancellationReason, null, null, null),
                    c.CancelledBy);
            }
        }

        // ── Identity timeline (managers + HO only; masked emails only) ──
        if (includeIdentity)
        {
            var identityEvents = await _db.VisitRequestIdentityChangeEvents.AsNoTracking()
                .Where(e => e.VisitRequestId == visit.VisitRequestId)
                .Select(e => new { e.EventType, e.FromStatus, e.ToStatus, e.ActorUserId, e.EmailMasked, e.CreatedAt })
                .ToListAsync(cancellationToken);
            foreach (var e in identityEvents)
            {
                Add(new VisitHistoryEntryDto(
                    e.CreatedAt, VisitHistoryEventCodes.ContactIdentityChanged, null, null, null,
                    null, null, null, e.EventType, null, null, e.EmailMasked, e.FromStatus, e.ToStatus),
                    e.ActorUserId);
            }
        }

        // ── Actor display names (one query), then attach them ──
        var names = actorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var ordered = raw
            .Select(x => x.ActorId is { } id && names.TryGetValue(id, out var name)
                ? x.Entry with { ActorName = name }
                : x.Entry)
            .OrderByDescending(e => e.At)
            .ToList();

        return new VisitRequestHistoryResponse(
            visit.VisitRequestId, visit.RequestCode ?? string.Empty, ordered);
    }

    /// <summary>
    /// What a per-campus revision row MEANS, read from the reason it was written rather than from its
    /// number. Both "sửa nhanh" and "gửi lại đơn" already write these rows — the timeline just collapsed
    /// every one of them into "content created / revised", so a safe edit and a resubmit were
    /// indistinguishable from an ordinary edit. Revision 1 is still a creation for rows written before
    /// <c>source_type</c> was populated.
    /// </summary>
    private static string InstanceRevisionCode(string? sourceType, uint formRevision) => sourceType switch
    {
        FormRevisionSourceTypes.Create => VisitHistoryEventCodes.InstanceContentCreated,
        FormRevisionSourceTypes.SafeEdit => VisitHistoryEventCodes.InstanceSafeEditApplied,
        FormRevisionSourceTypes.Resubmit => VisitHistoryEventCodes.InstanceContentResubmitted,
        FormRevisionSourceTypes.AmendmentApplied => VisitHistoryEventCodes.InstanceAmendmentApplied,
        _ => formRevision <= 1
            ? VisitHistoryEventCodes.InstanceContentCreated
            : VisitHistoryEventCodes.InstanceContentRevised,
    };

    /// <summary>Same reading for the request-level (registrant + contact block) revisions.</summary>
    private static string RequestRevisionCode(string? sourceType) => sourceType switch
    {
        FormRevisionSourceTypes.Create => VisitHistoryEventCodes.RequestCreated,
        FormRevisionSourceTypes.SafeEdit => VisitHistoryEventCodes.RequestSafeEditApplied,
        FormRevisionSourceTypes.Resubmit => VisitHistoryEventCodes.RequestResubmitted,
        _ => VisitHistoryEventCodes.RequestRevision,
    };
}
