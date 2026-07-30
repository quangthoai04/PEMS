using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.VisitAmendments;

/// <summary>Shared guards for the amendment handlers.</summary>
internal static class AmendmentGuards
{
    public static ulong EnsureAuthenticated(
        PerCampusFormV2WriteOptions writeFlag, ICurrentUserService currentUser)
    {
        if (!writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new ForbiddenException();
        return currentUser.UserId.Value;
    }

    /// <summary>Requester side: registrant or ACTIVE primary contact of THIS request.</summary>
    public static void EnsureRequesterSide(VisitRequest visit, ulong actorId)
    {
        var isRegistrant = visit.RegistrantUserId == actorId;
        var isActiveContact = visit.VisitorUserId == actorId
                              && visit.PrimaryContactAccessStatus == PrimaryContactAccessStatuses.Active;
        if (!isRegistrant && !isActiveContact)
            throw new ForbiddenException("Bạn không có quyền thao tác đề xuất của đơn này.");
    }

    /// <summary>Decision side: ONLY the current Host.</summary>
    public static void EnsureCurrentHost(ICurrentUserService currentUser, ulong? currentHostUserId)
    {
        var isHost = currentUser.UserId.HasValue && currentUser.UserId.Value == currentHostUserId;

        if (!isHost)
            throw new ForbiddenException("Chỉ Host hiện tại của cơ sở này mới được quyết định đề xuất.")
            {
                // stable code surfaced to the client
            };
    }

    public static VisitAmendmentDto ToDto(
        VisitInstanceAmendment a, string? requestedByName, string? decidedByName)
        => new(
            a.AmendmentId, a.VisitRequestId, a.VisitInstanceId, a.AmendmentNo, a.Status,
            a.BaseFormRevision, a.BaseApprovalRevision,
            a.RequestedBy, requestedByName, a.RequestedAt, a.Reason,
            a.DecidedBy, decidedByName, a.DecidedAt, a.DecisionNote, a.ExpiresAt,
            a.Changes.OrderBy(c => c.DisplayOrder)
                .Select(c => new VisitAmendmentChangeDto(c.FieldPath, c.ChangeClass, c.OldValueJson, c.NewValueJson))
                .ToList());
}

/// <summary>Requester submits a per-campus amendment; nothing active mutates (plan §16.6).</summary>
public sealed class SubmitVisitAmendmentCommandHandler
    : IRequestHandler<SubmitVisitAmendmentCommand, VisitAmendmentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitAmendmentService _amendments;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly ILogger<SubmitVisitAmendmentCommandHandler> _logger;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public SubmitVisitAmendmentCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitAmendmentService amendments,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        ILogger<SubmitVisitAmendmentCommandHandler> logger,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _amendments = amendments;
        _notificationService = notificationService;
        _logger = logger;
        _writeFlag = writeFlag;
    }

    public async Task<VisitAmendmentDto> Handle(
        SubmitVisitAmendmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .Include(v => v.CampusInstances).ThenInclude(c => c.GuestMemberLinks)
            .Include(v => v.GuestMembers)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);
        AmendmentGuards.EnsureRequesterSide(visit, actorId);

        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("Lịch thăm tại cơ sở", request.VisitInstanceId);

        VisitInstanceAmendment amendment;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            amendment = await _amendments.SubmitAsync(
                visit, instance, request.Proposal, actorId, now, cancellationToken);
            
            if (_currentUser.RoleCode == RoleCodes.Staff)
            {
                await _amendments.ApproveAsync(amendment, actorId, "Tự động duyệt do người đề xuất là Staff.", now, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }

        // Post-commit: notify the CURRENT campus Staff Leader (decision) + the current Host (visibility).
        try
        {
            var recipients = await _db.Users.AsNoTracking()
                .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader
                            && u.Status == UserStatuses.Active && u.PrimaryCampusId == instance.CampusId)
                .Select(u => u.UserId).ToListAsync(cancellationToken);
            if (instance.CurrentHostUserId is { } host) recipients.Add(host);
            var notifications = recipients.Distinct().Select(id =>
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Có đề xuất thay đổi nội dung chuyến thăm",
                    Message: $"Đơn {visit.RequestCode}: khách đề xuất thay đổi nội dung tại cơ sở của bạn (chờ Staff Leader duyệt).",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: instance.VisitInstanceId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: visit.VisitRequestId,
                    VisitInstanceId: instance.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}")).ToList();
            if (notifications.Count > 0)
                await _notificationService.CreateManyAsync(notifications, cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "amendment submit notification failed for {AmendmentId}", amendment.AmendmentId);
        }

        var requesterName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == actorId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
        return AmendmentGuards.ToDto(amendment, requesterName, null);
    }
}

/// <summary>Scoped read of an instance's ACTIVE (pending) amendment.</summary>
public sealed class GetActiveVisitAmendmentQueryHandler
    : IRequestHandler<GetActiveVisitAmendmentQuery, VisitAmendmentDto?>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public GetActiveVisitAmendmentQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _writeFlag = writeFlag;
    }

    public async Task<VisitAmendmentDto?> Handle(
        GetActiveVisitAmendmentQuery request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);

        var row = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == request.VisitInstanceId
                        && c.VisitRequestId == request.VisitRequestId)
            .Select(c => new
            {
                c.CampusId,
                c.CurrentHostUserId,
                c.VisitRequest!.RegistrantUserId,
                c.VisitRequest.VisitorUserId,
                c.VisitRequest.PrimaryContactAccessStatus,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Lịch thăm tại cơ sở", request.VisitInstanceId);

        // Scope: requester side, the current Host, or HO (read-only).
        var allowed = row.RegistrantUserId == actorId
            || (row.VisitorUserId == actorId && row.PrimaryContactAccessStatus == PrimaryContactAccessStatuses.Active)
            || row.CurrentHostUserId == actorId
            || _currentUser.RoleCode == RoleCodes.Ho;
        if (!allowed)
            throw new ForbiddenException("Bạn không có quyền xem đề xuất của cơ sở này.");

        var amendment = await _db.VisitInstanceAmendments.AsNoTracking()
            .Include(a => a.Changes)
            .Where(a => a.VisitInstanceId == request.VisitInstanceId
                        && a.Status == AmendmentStatuses.PendingApproval)
            .FirstOrDefaultAsync(cancellationToken);
        if (amendment is null) return null;

        var names = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == amendment.RequestedBy)
            .Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
        return AmendmentGuards.ToDto(amendment, names, null);
    }
}

/// <summary>Approve / reject (current campus Staff Leader) and withdraw (requester side).</summary>
public sealed class DecideVisitAmendmentCommandHandlers :
    IRequestHandler<ApproveVisitAmendmentCommand, VisitAmendmentDecisionResponse>,
    IRequestHandler<RejectVisitAmendmentCommand, VisitAmendmentDecisionResponse>,
    IRequestHandler<WithdrawVisitAmendmentCommand, VisitAmendmentDecisionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitAmendmentService _amendments;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly ILogger<DecideVisitAmendmentCommandHandlers> _logger;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public DecideVisitAmendmentCommandHandlers(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitAmendmentService amendments,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        ILogger<DecideVisitAmendmentCommandHandlers> logger,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _amendments = amendments;
        _notificationService = notificationService;
        _logger = logger;
        _writeFlag = writeFlag;
    }

    public async Task<VisitAmendmentDecisionResponse> Handle(
        ApproveVisitAmendmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);
        var campusId = await CampusOfInstanceAsync(request.VisitInstanceId, cancellationToken);
        var instance = await _db.VisitRequestCampuses.AsNoTracking().FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken);
        try
        {
            AmendmentGuards.EnsureCurrentHost(_currentUser, instance?.CurrentHostUserId);
        }
        catch (ForbiddenException)
        {
            throw new ForbiddenException("Chỉ Host hiện tại của cơ sở này mới được duyệt đề xuất.");
        }

        VisitAmendmentDecisionResponse result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var amendment = await LockOwnAsync(request.AmendmentId, request.VisitInstanceId, cancellationToken);
            result = await _amendments.ApproveAsync(amendment, actorId, request.Note, _clock.VietnamNow, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        await NotifyDecisionAsync(request.VisitInstanceId, result, "được DUYỆT và áp dụng", actorId, cancellationToken);
        return result;
    }

    public async Task<VisitAmendmentDecisionResponse> Handle(
        RejectVisitAmendmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);
        var campusId = await CampusOfInstanceAsync(request.VisitInstanceId, cancellationToken);
        var instance = await _db.VisitRequestCampuses.AsNoTracking().FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken);
        AmendmentGuards.EnsureCurrentHost(_currentUser, instance?.CurrentHostUserId);

        VisitAmendmentDecisionResponse result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var amendment = await LockOwnAsync(request.AmendmentId, request.VisitInstanceId, cancellationToken);
            result = await _amendments.RejectAsync(amendment, actorId, request.Note, _clock.VietnamNow, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        await NotifyDecisionAsync(request.VisitInstanceId, result, "bị TỪ CHỐI (nội dung hiện tại giữ nguyên)", actorId, cancellationToken);
        return result;
    }

    public async Task<VisitAmendmentDecisionResponse> Handle(
        WithdrawVisitAmendmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);
        var head = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == request.VisitRequestId)
            .Select(v => new { v.RegistrantUserId, v.VisitorUserId, v.PrimaryContactAccessStatus })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);
        var isRequesterSide = head.RegistrantUserId == actorId
            || (head.VisitorUserId == actorId
                && head.PrimaryContactAccessStatus == PrimaryContactAccessStatuses.Active);
        if (!isRequesterSide)
            throw new ForbiddenException("Bạn không có quyền thao tác đề xuất của đơn này.");

        VisitAmendmentDecisionResponse result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var amendment = await LockOwnAsync(request.AmendmentId, request.VisitInstanceId, cancellationToken);
            if (amendment.VisitRequestId != request.VisitRequestId)
                throw new NotFoundException("Đề xuất thay đổi", request.AmendmentId);
            result = await _amendments.WithdrawAsync(amendment, actorId, _clock.VietnamNow, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        return result;
    }

    private async Task<ulong> CampusOfInstanceAsync(ulong visitInstanceId, CancellationToken ct)
        => await _db.VisitRequestCampuses.AsNoTracking()
               .Where(c => c.VisitInstanceId == visitInstanceId)
               .Select(c => (ulong?)c.CampusId)
               .FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Lịch thăm tại cơ sở", visitInstanceId);

    private async Task<VisitInstanceAmendment> LockOwnAsync(
        ulong amendmentId, ulong visitInstanceId, CancellationToken ct)
    {
        var amendment = await _amendments.LockAmendmentAsync(amendmentId, ct)
            ?? throw new NotFoundException("Đề xuất thay đổi", amendmentId);
        if (amendment.VisitInstanceId != visitInstanceId)
            throw new NotFoundException("Đề xuất thay đổi", amendmentId);
        return amendment;
    }

    private async Task NotifyDecisionAsync(
        ulong visitInstanceId, VisitAmendmentDecisionResponse result, string outcome,
        ulong actorId, CancellationToken ct)
    {
        try
        {
            var row = await _db.VisitInstanceAmendments.AsNoTracking()
                .Where(a => a.AmendmentId == result.AmendmentId)
                .Select(a => new { a.RequestedBy, a.VisitRequestId })
                .FirstAsync(ct);
            var host = await _db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == visitInstanceId)
                .Select(c => c.CurrentHostUserId)
                .FirstOrDefaultAsync(ct);
            var recipients = new System.Collections.Generic.HashSet<ulong> { row.RequestedBy };
            if (host is { } h) recipients.Add(h);
            recipients.Remove(actorId);
            if (recipients.Count == 0) return;
            var notifications = recipients.Select(id =>
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Đề xuất thay đổi đã được xử lý",
                    Message: $"Đề xuất thay đổi của cơ sở đã {outcome}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: visitInstanceId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: false,
                    VisitRequestId: row.VisitRequestId,
                    VisitInstanceId: visitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={row.VisitRequestId}")).ToList();
            await _notificationService.CreateManyAsync(notifications, ct);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "amendment decision notification failed for {AmendmentId}", result.AmendmentId);
        }
    }
}
