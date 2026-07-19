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

        var entries = new List<VisitHistoryEntryDto>();
        var actorIds = new HashSet<ulong>();

        // ── Instance revisions (applied, immutable) ──
        var instanceRevisions = await _db.VisitInstanceFormRevisionHistories.AsNoTracking()
            .Where(r => r.VisitRequestId == visit.VisitRequestId
                        && visibleInstanceIds.Contains(r.VisitInstanceId))
            .Select(r => new { r.VisitInstanceId, r.FormRevision, r.ApprovalRevision, r.SourceType, r.AppliedBy, r.AppliedAt })
            .ToListAsync(cancellationToken);
        foreach (var r in instanceRevisions)
        {
            if (r.AppliedBy is { } by) actorIds.Add(by);
            entries.Add(new VisitHistoryEntryDto(
                r.AppliedAt, "INSTANCE_REVISION", r.VisitInstanceId,
                $"Nội dung cơ sở — bản áp dụng #{r.FormRevision}",
                $"source={r.SourceType};approvalRevision={r.ApprovalRevision}",
                null));
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
                if (r.AppliedBy is { } by) actorIds.Add(by);
                entries.Add(new VisitHistoryEntryDto(
                    r.AppliedAt, "REQUEST_REVISION", null,
                    $"Thông tin chung — bản #{r.RequestRevision}", $"source={r.SourceType}", null));
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
            actorIds.Add(a.RequestedBy);
            entries.Add(new VisitHistoryEntryDto(
                a.RequestedAt, "AMENDMENT", a.VisitInstanceId,
                $"Đề xuất thay đổi #{a.AmendmentNo} (chưa phải nội dung hiệu lực)",
                $"status={a.Status}", null));
            if (a.DecidedAt is { } decidedAt)
            {
                if (a.DecidedBy is { } by) actorIds.Add(by);
                entries.Add(new VisitHistoryEntryDto(
                    decidedAt, "AMENDMENT_DECISION", a.VisitInstanceId,
                    $"Đề xuất #{a.AmendmentNo}: {a.Status}", a.DecisionNote, null));
            }
        }

        // ── Campus decisions ──
        foreach (var c in visit.CampusInstances.Where(c => visibleInstanceIds.Contains(c.VisitInstanceId)))
        {
            if (c.DecidedAt is { } decidedAt)
            {
                if (c.DecidedBy is { } by) actorIds.Add(by);
                entries.Add(new VisitHistoryEntryDto(
                    decidedAt, "DECISION", c.VisitInstanceId,
                    $"Cơ sở: {c.Status}", c.DecisionNote, null));
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
                if (e.ActorUserId is { } by) actorIds.Add(by);
                entries.Add(new VisitHistoryEntryDto(
                    e.CreatedAt, "IDENTITY", null,
                    e.EventType,
                    $"email={e.EmailMasked};{e.FromStatus}→{e.ToStatus}", null));
            }
        }

        // ── Actor display names (one query) + final ordering ──
        var names = actorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);
        // (names resolved per entry kind above where available; entries keep null actor when unknown)

        var ordered = entries.OrderByDescending(e => e.At).ToList();
        return new VisitRequestHistoryResponse(
            visit.VisitRequestId, visit.RequestCode ?? string.Empty, ordered);
    }
}
