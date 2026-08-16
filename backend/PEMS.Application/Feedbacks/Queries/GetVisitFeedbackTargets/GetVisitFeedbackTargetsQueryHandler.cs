using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Feedbacks.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Feedbacks;

namespace PEMS.Application.Feedbacks.Queries.GetVisitFeedbackTargets;

public sealed class GetVisitFeedbackTargetsQueryHandler
    : IRequestHandler<GetVisitFeedbackTargetsQuery, GetVisitFeedbackTargetsResponse>
{
    // Logistics items shown in "Detail setup": the department actually handled them.
    private static readonly string[] RateableLogisticsStatuses = { "ACCEPTED", "IN_PROGRESS", "DONE" };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitFeedbackTargetsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetVisitFeedbackTargetsResponse> Handle(
        GetVisitFeedbackTargetsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);
        var visitRequest = await _db.VisitRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.VisitRequestId == instance.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", instance.VisitRequestId);

        var actorType = FeedbackEligibility.ActorTypeOf(userId, visitRequest, instance)
            ?? throw new ForbiddenException("Bạn không có quyền đánh giá chuyến thăm này.");

        var statusEligible = FeedbackEligibility.IsInstanceStatusEligible(instance.Status);

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        // Mixed per-campus v2: this feedback screen is INSTANCE-scoped → THIS instance's detail name.
        var effectiveDelegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { instance.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(instance.VisitInstanceId);

        var myFeedbacks = await _db.Feedbacks.AsNoTracking()
            .Where(f => f.VisitInstanceId == instance.VisitInstanceId && f.SubmittedByUserId == userId)
            .ToListAsync(cancellationToken);

        var response = new GetVisitFeedbackTargetsResponse
        {
            ActorType = actorType,
            VisitRequestId = visitRequest.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = effectiveDelegationName,
            CampusName = campusName,
            InstanceStatus = instance.Status,
            PlannedStartAt = instance.PlannedStartAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            PlannedEndAt = instance.PlannedEndAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            ExistingFeedbacks = myFeedbacks
                .OrderBy(f => f.SubmittedAt)
                .Select(f => new ExistingFeedbackDto
                {
                    FeedbackId = f.FeedbackId,
                    FeedbackType = f.FeedbackType,
                    TargetType = f.TargetType,
                    TargetName = f.TargetNameSnapshot,
                    Rating = f.Rating,
                    Comment = f.Comment,
                    SubmittedAt = f.SubmittedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                }).ToList(),
        };

        response.Groups = actorType == FeedbackSubmitterRoles.Visitor
            ? BuildVisitorGroups(visitRequest, campusName, myFeedbacks, effectiveDelegationName)
            : await BuildHostGroupsAsync(visitRequest, instance, myFeedbacks, effectiveDelegationName, cancellationToken);

        var allTargets = response.Groups.SelectMany(g => g.Targets).ToList();
        response.AlreadySubmittedAllRequired = allTargets.Count > 0 && allTargets.All(t => t.AlreadySubmitted);
        response.CanSubmit = statusEligible && allTargets.Any(t => !t.AlreadySubmitted);
        response.SubmitHintMessage = !statusEligible
            ? "Chỉ đánh giá được khi chuyến thăm đang diễn ra hoặc đã hoàn tất."
            : response.AlreadySubmittedAllRequired
                ? "Bạn đã đánh giá chuyến thăm này."
                : actorType == FeedbackSubmitterRoles.Visitor
                    ? "Chấm sao bắt buộc; nhận xét không bắt buộc."
                    : "Chấm sao cho từng mục (bắt buộc với mục muốn gửi); nhận xét không bắt buộc.";
        response.SubmitHintKey = !statusEligible
            ? "NOT_ELIGIBLE"
            : response.AlreadySubmittedAllRequired
                ? "ALL_SUBMITTED"
                : actorType == FeedbackSubmitterRoles.Visitor
                    ? "VISITOR_HINT"
                    : "HOST_HINT";
        return response;
    }

    // ── Visitor: one overall target on the campus instance ──────────────────
    private static List<FeedbackGroupDto> BuildVisitorGroups(
        Domain.Entities.Delegations.VisitRequest visitRequest, string? campusName, List<Feedback> myFeedbacks,
        string? effectiveDelegationName)
    {
        var existing = myFeedbacks.FirstOrDefault(f => f.FeedbackType == FeedbackTypes.VisitorOverall);
        return new List<FeedbackGroupDto>
        {
            new()
            {
                GroupCode = "OVERALL",
                Title = "Đánh giá chung chuyến thăm",
                Targets = new List<FeedbackTargetDto>
                {
                    new()
                    {
                        TargetKey = "INSTANCE:OVERALL",
                        FeedbackType = FeedbackTypes.VisitorOverall,
                        TargetType = FeedbackTargetTypes.VisitInstance,
                        Name = effectiveDelegationName,
                        Subtitle = campusName is null ? "Đoàn tiếp đón" : $"Đoàn tiếp đón • {campusName}",
                        TargetContext = "Đoàn tiếp đón",
                        AlreadySubmitted = existing != null,
                        ExistingRating = existing?.Rating,
                        ExistingComment = existing?.Comment,
                    },
                },
            },
        };
    }

    // ── Host: 3 compact groups mapped to REAL rows only ──────────────────────
    // Rule mới (v10.1): Bỏ group "Người tạo đoàn khách";
    //   Group 1 "Đoàn khách" = 1 target đánh giá CHUNG cả đoàn (VISIT_INSTANCE target),
    //   không từng guest member.
    //   Group 2 "Người tham gia hỗ trợ" = từng participant.
    //   Group 3 "Hậu cần / đồ mượn" = từng logistics item.
    private async Task<List<FeedbackGroupDto>> BuildHostGroupsAsync(
        Domain.Entities.Delegations.VisitRequest visitRequest,
        Domain.Entities.Delegations.VisitRequestCampus instance,
        List<Feedback> myFeedbacks,
        string? effectiveDelegationName,
        CancellationToken ct)
    {
        var groups = new List<FeedbackGroupDto>();

        void Attach(FeedbackTargetDto t)
        {
            var match = myFeedbacks.FirstOrDefault(f => FeedbackRules.SameTarget(
                f, t.TargetType, t.TargetUserId, t.TargetParticipantId, t.TargetGuestMemberId,
                t.TargetLogisticsItemId, t.TargetHandoverId, t.TargetDepartmentId));
            if (match != null)
            {
                t.AlreadySubmitted = true;
                t.ExistingRating = match.Rating;
                t.ExistingComment = match.Comment;
            }
        }

        // 1. Đoàn khách — 1 target đánh giá chung cả đoàn (VISIT_INSTANCE).
        //    Rule: Host không đánh giá từng thành viên riêng lẻ; chỉ đánh giá cả đoàn.
        var guestCount = await _db.VisitGuestMembers.AsNoTracking()
            .CountAsync(m => m.VisitRequestId == visitRequest.VisitRequestId, ct);
        var delegationGroup = new FeedbackGroupDto
        {
            GroupCode = "DELEGATION",
            Title = "Đoàn khách",
        };
        var delegationTarget = new FeedbackTargetDto
        {
            TargetKey = $"INSTANCE:DELEGATION:{instance.VisitInstanceId}",
            FeedbackType = FeedbackTypes.HostDelegationOverall,
            TargetType = FeedbackTargetTypes.VisitInstance,
            Name = effectiveDelegationName,
            Subtitle = string.Join(" • ", new[]
            {
                visitRequest.RegistrantOrganization,
                guestCount > 0 ? $"{guestCount} khách" : null,
            }.Where(s => !string.IsNullOrWhiteSpace(s))),
            TargetRole = "Đoàn khách",
            TargetContext = "Đoàn khách",
        };
        Attach(delegationTarget);
        delegationGroup.Targets.Add(delegationTarget);
        groups.Add(delegationGroup);

        // 2. Người tham gia hỗ trợ — internal participants (host/IC support/dept support/students)
        //    who accepted/were assigned. Host không tự đánh giá bản thân.
        var participantRows = await (
            from p in _db.VisitParticipants.AsNoTracking()
            join u in _db.Users.AsNoTracking() on p.UserId equals u.UserId
            where p.VisitInstanceId == instance.VisitInstanceId
                  && (p.Status == ParticipantStatuses.Accepted || p.Status == ParticipantStatuses.Assigned)
            select new { p.ParticipantId, p.UserId, p.ParticipantRole, u.FullName, u.DepartmentId }
        ).ToListAsync(ct);
        var participantDeptIds = participantRows
            .Where(p => p.DepartmentId.HasValue).Select(p => p.DepartmentId!.Value).Distinct().ToList();
        var deptNamesById = participantDeptIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Departments.AsNoTracking()
                .Where(d => participantDeptIds.Contains(d.DepartmentId))
                .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, ct);

        var setupGroup = new FeedbackGroupDto { GroupCode = "SETUP", Title = "Người tham gia hỗ trợ" };
        foreach (var p in participantRows.OrderBy(p => p.ParticipantRole).ThenBy(p => p.FullName))
        {
            // Host không tự đánh giá bản thân.
            if (p.UserId == instance.CurrentHostUserId) continue;
            var roleLabel = p.ParticipantRole switch
            {
                ParticipantRoles.IcHost => "Host",
                ParticipantRoles.IcSupport => "Staff hỗ trợ IC",
                ParticipantRoles.DeptSupport => "Phòng ban hỗ trợ",
                ParticipantRoles.Student => "Sinh viên hỗ trợ",
                _ => p.ParticipantRole,
            };
            var deptName = p.DepartmentId.HasValue && deptNamesById.TryGetValue(p.DepartmentId.Value, out var dn) ? dn : null;
            var target = new FeedbackTargetDto
            {
                TargetKey = $"PARTICIPANT:{p.ParticipantId}",
                FeedbackType = FeedbackTypes.HostParticipant,
                TargetType = FeedbackTargetTypes.VisitParticipant,
                TargetParticipantId = p.ParticipantId,
                Name = p.FullName,
                Subtitle = string.Join(" • ", new[] { roleLabel, deptName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                TargetRole = roleLabel,
                TargetContext = "Người tham gia hỗ trợ",
            };
            Attach(target);
            setupGroup.Targets.Add(target);
        }
        if (setupGroup.Targets.Count == 0)
            setupGroup.InfoNote = "Chưa có bên tham gia hỗ trợ nào xác nhận cho chuyến này.";
        groups.Add(setupGroup);

        // 3. Hậu cần / đồ mượn — logistics/resource items the departments actually handled.
        var logisticsRows = await _db.VisitLogisticsItems.AsNoTracking()
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId
                        && RateableLogisticsStatuses.Contains(l.Status))
            .Select(l => new
            {
                l.LogisticsItemId, l.Title, l.ItemType, l.Quantity, l.Status,
                l.RequestedToDepartmentId, l.AssignedToUserId,
            })
            .ToListAsync(ct);
        var logisticsDeptIds = logisticsRows
            .Where(l => l.RequestedToDepartmentId.HasValue).Select(l => l.RequestedToDepartmentId!.Value).Distinct().ToList();
        var logisticsDeptNames = logisticsDeptIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Departments.AsNoTracking()
                .Where(d => logisticsDeptIds.Contains(d.DepartmentId))
                .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, ct);
        var assigneeIds = logisticsRows
            .Where(l => l.AssignedToUserId.HasValue).Select(l => l.AssignedToUserId!.Value).Distinct().ToList();
        var assigneeNames = assigneeIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => assigneeIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
        var logisticsItemIds = logisticsRows.Select(l => l.LogisticsItemId).ToList();
        var handovers = logisticsItemIds.Count == 0
            ? new List<Domain.Entities.Delegations.VisitLogisticsItemHandover>()
            : await _db.VisitLogisticsItemHandovers.AsNoTracking()
                .Where(h => logisticsItemIds.Contains(h.LogisticsItemId))
                .ToListAsync(ct);

        var detailGroup = new FeedbackGroupDto { GroupCode = "DETAIL_SETUP", Title = "Hậu cần / đồ mượn" };
        foreach (var l in logisticsRows.OrderBy(l => l.RequestedToDepartmentId).ThenBy(l => l.LogisticsItemId))
        {
            var deptName = l.RequestedToDepartmentId.HasValue
                && logisticsDeptNames.TryGetValue(l.RequestedToDepartmentId.Value, out var dn) ? dn : null;
            var assigneeName = l.AssignedToUserId.HasValue
                && assigneeNames.TryGetValue(l.AssignedToUserId.Value, out var an) ? an : null;
            var borrow = handovers.FirstOrDefault(h => h.LogisticsItemId == l.LogisticsItemId && h.HandoverType == LogisticsHandoverTypes.Borrow);
            var ret = handovers.FirstOrDefault(h => h.LogisticsItemId == l.LogisticsItemId && h.HandoverType == LogisticsHandoverTypes.Return);
            string SignState(Domain.Entities.Delegations.VisitLogisticsItemHandover? h) =>
                h is null ? "chưa ký"
                : h.BorrowerSignedAt != null && h.ProviderSignedAt != null ? "đủ chữ ký"
                : "đã ký 1 bên";
            var subtitleParts = new[]
            {
                l.ItemType,
                l.Quantity.HasValue ? $"SL: {l.Quantity}" : null,
                deptName,
                assigneeName,
                $"Mượn: {SignState(borrow)}",
                $"Trả: {SignState(ret)}",
            }.Where(s => !string.IsNullOrWhiteSpace(s));
            var target = new FeedbackTargetDto
            {
                TargetKey = $"LOGISTICS:{l.LogisticsItemId}",
                FeedbackType = FeedbackTypes.HostLogistics,
                TargetType = FeedbackTargetTypes.LogisticsItem,
                TargetLogisticsItemId = l.LogisticsItemId,
                Name = l.Title,
                Subtitle = string.Join(" • ", subtitleParts),
                TargetRole = deptName,
                TargetContext = "Hậu cần / đồ mượn",
            };
            Attach(target);
            detailGroup.Targets.Add(target);
        }
        if (detailGroup.Targets.Count == 0)
            detailGroup.InfoNote = "Không có hạng mục hậu cần nào được phòng ban xử lý cho chuyến này.";
        groups.Add(detailGroup);

        return groups;
    }
}
