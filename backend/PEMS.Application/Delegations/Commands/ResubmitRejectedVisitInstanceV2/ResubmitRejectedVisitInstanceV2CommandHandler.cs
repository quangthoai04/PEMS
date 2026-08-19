using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitInstanceV2;

/// <summary>
/// One rejected campus goes back for review; every sibling stays exactly as it was.
///
/// <para>
/// Who may do it is the point of this handler. The registrant owns the request, and the confirmed
/// operational contact runs THIS campus — both are on the guest side of it, and either may ask for it to
/// be looked at again. The contact of a SIBLING campus may not, and neither may a VISITOR who simply
/// holds an account: authority comes from
/// <c>visit_request_campuses.operational_contact_user_id</c>, never from a role name (plan v9 §2).
/// </para>
/// </summary>
public sealed class ResubmitRejectedVisitInstanceV2CommandHandler
    : IRequestHandler<ResubmitRejectedVisitInstanceV2Command, ResubmitRejectedVisitInstanceV2Response>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestV2EditService _editService;
    private readonly INotificationService _notifications;
    private readonly ILogger<ResubmitRejectedVisitInstanceV2CommandHandler> _logger;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public ResubmitRejectedVisitInstanceV2CommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestV2EditService editService, INotificationService notifications,
        ILogger<ResubmitRejectedVisitInstanceV2CommandHandler> logger,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _editService = editService;
        _notifications = notifications;
        _logger = logger;
        _writeFlag = writeFlag;
    }

    public async Task<ResubmitRejectedVisitInstanceV2Response> Handle(
        ResubmitRejectedVisitInstanceV2Command request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;
        var now = _clock.VietnamNow;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .Include(v => v.CampusInstances).ThenInclude(c => c.GuestMemberLinks)
            .Include(v => v.GuestMembers)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        // Naming a campus of somebody else's request fails here rather than being silently accepted.
        var instance = visit.CampusInstances
            .FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // IsGuestSide is the shared guard: registrant of the request, OR confirmed contact of THIS
        // campus. Asking it per-campus is what stops the holder of HN resubmitting DN.
        if (!VisitRequestOwnership.IsGuestSide(visit, instance, actorId))
            throw new ForbiddenException(
                "Chỉ người đăng ký hoặc đầu mối vận hành của cơ sở này mới được gửi lại cơ sở này.");

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var result = await _editService.ApplyInstanceResubmitAsync(
            visit, instance, request.Content, actorId, now, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        // Post-commit, best-effort: the campus IS back in review whatever happens to the notification.
        await NotifyLeaderAsync(visit, instance, actorId, cancellationToken);

        return new ResubmitRejectedVisitInstanceV2Response(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            instance.RowVersion,
            "Đã gửi lại yêu cầu tại cơ sở này. Cơ sở sẽ xem xét lại.");
    }

    /// <summary>
    /// Tells the Staff Leader of THIS campus, and nobody else's. A sibling campus's leader has nothing
    /// to reprocess and no reason to hear about it.
    /// </summary>
    private async Task NotifyLeaderAsync(
        Domain.Entities.Delegations.VisitRequest visit,
        Domain.Entities.Delegations.VisitRequestCampus instance,
        ulong actorId, CancellationToken cancellationToken)
    {
        try
        {
            if (instance.CoordinatorUserId is not { } leaderId) return;

            var campusName = await _db.Campuses.AsNoTracking()
                .Where(c => c.CampusId == instance.CampusId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{instance.CampusId}";

            await _notifications.CreateAsync(new CreateNotificationRequest(
                RecipientUserId: leaderId,
                Title: "Yêu cầu tại cơ sở đã được gửi lại",
                Message: $"Đơn {visit.RequestCode} đã được chỉnh sửa và gửi lại tại cơ sở {campusName}. Vui lòng xem xét lại.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: actorId,
                Category: NotificationCategories.Visit,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}",
                MetadataJson: PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                    PEMS.Application.Notifications.Common.NotificationEventKeys.VisitRequestResubmitted,
                    new { requestCode = visit.RequestCode, campusName })
            ), cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex,
                "instance resubmit {VisitInstanceId} committed but its notification failed",
                instance.VisitInstanceId);
        }
    }
}
