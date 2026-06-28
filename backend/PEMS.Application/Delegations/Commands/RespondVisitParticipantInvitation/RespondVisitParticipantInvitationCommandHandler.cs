using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;

public sealed class RespondVisitParticipantInvitationCommandHandler
    : IRequestHandler<RespondVisitParticipantInvitationCommand, RespondVisitParticipantInvitationResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public RespondVisitParticipantInvitationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<RespondVisitParticipantInvitationResponse> Handle(
        RespondVisitParticipantInvitationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var participant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId, cancellationToken)
            ?? throw new NotFoundException("VisitParticipant", request.ParticipantId);

        // Ownership: a user may only respond to their OWN invitation.
        if (participant.UserId != userId)
            throw new ForbiddenException("Bạn chỉ có thể phản hồi lời mời của chính mình.");

        // The host slot is not an invitation; only the 3 non-host invitee roles can respond.
        if (participant.IsHost || participant.ParticipantRole == ParticipantRoles.IcHost)
            throw new ForbiddenException("Lời mời này không thể phản hồi.");

        if (participant.ParticipantRole != ParticipantRoles.IcSupport
            && participant.ParticipantRole != ParticipantRoles.DeptSupport
            && participant.ParticipantRole != ParticipantRoles.Student)
            throw new ForbiddenException("Loại lời mời không hợp lệ.");

        // Must still be pending — already-responded invitations are immutable here.
        if (participant.Status != ParticipantStatuses.Invited && participant.Status != ParticipantStatuses.Assigned)
            throw new ConflictException("Lời mời đã được phản hồi hoặc không còn hiệu lực.");

        var now = _clock.UtcNow;
        string newStatus;

        if (request.Accept)
        {
            participant.Status = ParticipantStatuses.Accepted;
            newStatus = ParticipantStatuses.Accepted;
        }
        else
        {
            participant.Status = ParticipantStatuses.Declined;
            // No decline_reason column on visit_participants — record it on note (per schema).
            // Validator guarantees a non-empty 5–1000 char reason here; store it trimmed.
            participant.Note = request.DeclineReason!.Trim();
            newStatus = ParticipantStatuses.Declined;
        }

        participant.RespondedAt = now;
        participant.UpdatedAt = now;
        participant.UpdatedBy = userId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = request.Accept ? "ACCEPT_VISIT_INVITATION" : "DECLINE_VISIT_INVITATION",
            EntityType = "VisitParticipant",
            EntityId = participant.ParticipantId,
            CreatedAt = now
        });

        await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitParticipant, participant.ParticipantId, "Lời mời này đã được phản hồi.", now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var message = request.Accept
            ? "Đã xác nhận tham gia. Đơn sẽ xuất hiện trong tab Đơn mời tham dự."
            : "Đã từ chối lời mời tham gia.";

        return new RespondVisitParticipantInvitationResponse(participant.ParticipantId, newStatus, message);
    }
}
