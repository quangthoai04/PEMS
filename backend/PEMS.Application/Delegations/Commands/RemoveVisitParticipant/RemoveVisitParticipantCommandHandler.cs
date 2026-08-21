using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.RemoveVisitParticipant;

public sealed class RemoveVisitParticipantCommandHandler
    : IRequestHandler<RemoveVisitParticipantCommand, RemoveVisitParticipantResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IUserMutationLockService _lockService;

    public RemoveVisitParticipantCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IUserMutationLockService lockService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _lockService = lockService;
    }

    public async Task<RemoveVisitParticipantResponse> Handle(
        RemoveVisitParticipantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        // Structural FK, safe to peek unlocked (see VisitInvitationResponse.ApplyCoreAsync for the
        // same pattern) — needed to lock tier 2 before anything that reads request-level status.
        var visitRequestId = await _db.VisitRequestCampuses
            .Where(c => c.VisitInstanceId == request.VisitInstanceId)
            .Select(c => (ulong?)c.VisitRequestId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Removing a participant joins the same lock hierarchy every writer of this aggregate follows
        // (VisitRequest → VisitRequestCampus → VisitParticipant), so it cannot race a concurrent
        // response/cancellation on the same rows — previously this handler took no lock and no
        // transaction at all.
        await using var transaction = await _db.BeginSerializedTransactionAsync(cancellationToken);
        await _lockService.LockVisitRequestsAsync(new[] { visitRequestId }, cancellationToken);
        await _lockService.LockVisitRequestCampusesAsync(new[] { request.VisitInstanceId }, cancellationToken);
        await _lockService.LockVisitParticipantsAsync(new[] { request.ParticipantId }, cancellationToken);

        var participant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId, cancellationToken)
            ?? throw new NotFoundException("VisitParticipant", request.ParticipantId);

        if (participant.VisitInstanceId != request.VisitInstanceId)
            throw new NotFoundException("VisitParticipant", request.ParticipantId);

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == participant.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", participant.VisitInstanceId);

        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được gỡ thành phần tham gia.");

        if (participant.IsHost || participant.ParticipantRole == ParticipantRoles.IcHost)
            throw new ConflictException("Không thể gỡ Host chính khỏi danh sách.");

        if (participant.Status != ParticipantStatuses.Invited)
            throw new ConflictException(
                "Chỉ có thể gỡ lời mời khi người được mời chưa phản hồi. Người đã chấp nhận/được phân công cần quy trình xác nhận riêng.");

        var now = _clock.VietnamNow;
        participant.Status = ParticipantStatuses.Removed;
        participant.UpdatedAt = now;
        participant.UpdatedBy = actorId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = instance.CampusId,
            Action = "REMOVE_VISIT_PARTICIPANT",
            EntityType = "VisitParticipant",
            EntityId = participant.ParticipantId,
            CreatedAt = now,
        });

        await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitParticipant, participant.ParticipantId, "Lời mời này đã bị thu hồi.", now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RemoveVisitParticipantResponse(
            participant.ParticipantId, participant.Status, "Đã gỡ khỏi danh sách mời.");
    }
}
