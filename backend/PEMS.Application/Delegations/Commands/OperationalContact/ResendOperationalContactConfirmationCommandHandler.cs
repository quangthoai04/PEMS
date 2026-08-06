using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// Re-sends the pending invitation of ONE campus.
///
/// The old link dies FIRST, inside the transaction, and <c>token_version</c> is bumped before the new
/// one is minted. That ordering is the whole point: two live links for the same campus would let a
/// stale email win a race against the current one, and the version is what makes the new invitation
/// distinguishable in the dispatcher's dedupe key.
///
/// Rate limited on two axes — a hard cap per invitation and a cooldown since the last link was
/// actually minted — because an invitation the registrant can fire at will is a spam channel pointed
/// at somebody else's inbox.
/// </summary>
public sealed class ResendOperationalContactConfirmationCommandHandler
    : IRequestHandler<ResendOperationalContactConfirmationCommand, OperationalContactManageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IOperationalContactInvitationService _invitations;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public ResendOperationalContactConfirmationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IOperationalContactInvitationService invitations, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _invitations = invitations;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactManageResponse> Handle(
        ResendOperationalContactConfirmationCommand request, CancellationToken cancellationToken)
    {
        var actorId = OperationalContactGuards.RequireAuthenticated(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        ulong changeId;
        OperationalContactManageResponse response;

        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
                _db, request.VisitRequestId, request.VisitInstanceId, cancellationToken);

            var change = await _invitations.LockPendingChangeForInstanceAsync(
                    instance.VisitInstanceId, cancellationToken)
                ?? throw new ConflictException(
                    "Cơ sở này không có lời mời nào đang chờ xác nhận để gửi lại.",
                    OperationalContactErrorCodes.ConfirmationNotFound);

            // A TRANSFER may also be resent by the contact who proposed it; an initial confirmation is
            // the registrant's to manage.
            OperationalContactGuards.EnsureMayManageContact(visit, instance, actorId,
                allowCurrentContact: change.ChangeKind == IdentityChangeKinds.Transfer);

            if (change.ExpiresAt <= now)
                throw new ConflictException(
                    "Lời mời đã hết hạn. Vui lòng tạo lời mời mới thay vì gửi lại.",
                    OperationalContactErrorCodes.ConfirmationExpired);

            await OperationalContactGuards.EnsureResendAllowedAsync(_db, change, now, cancellationToken);

            // Kill every outstanding link BEFORE the replacement exists.
            await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _db, EmailActionTargetTypes.VisitRequestIdentityChange, change.IdentityChangeId,
                "Lời mời đã được gửi lại bằng liên kết mới.", now, cancellationToken);

            change.TokenVersion += 1;
            change.ResendCount += 1;
            change.ExpiresAt = OperationalContactGuards.ExpiryFor(change.ChangeKind, now);
            change.UpdatedAt = now;

            _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = change.IdentityChangeId,
                VisitRequestId = visit.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                EventType = "OPERATIONAL_CONTACT_INVITATION_RESENT",
                FromStatus = IdentityChangeStatuses.Pending,
                ToStatus = IdentityChangeStatuses.Pending,
                ActorUserId = actorId,
                EmailMasked = change.NewEmailMasked,
                Reason = $"token_version={change.TokenVersion};resend_count={change.ResendCount};new_expiry={change.ExpiresAt:yyyy-MM-dd HH:mm}",
                CorrelationId = correlationId,
                CreatedAt = now,
            });
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                Action = "OPERATIONAL_CONTACT_INVITATION_RESENT",
                EntityType = "VisitRequestCampus",
                EntityId = instance.VisitInstanceId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            changeId = change.IdentityChangeId;
            response = new OperationalContactManageResponse(
                visit.VisitRequestId, instance.VisitInstanceId, instance.Status, visit.Status,
                ContactConfirmed: instance.OperationalContactUserId is not null,
                change.ChangeKind, IdentityChangeStatuses.Pending, change.NewEmailMasked,
                change.ExpiresAt, change.ResendCount, change.TokenVersion,
                "Đã gửi lại lời mời xác nhận cho cơ sở này. Liên kết cũ không còn hiệu lực.");
        }

        // AFTER commit: mint the fresh token and send. A failure here leaves the invitation valid and
        // resendable rather than half-applied.
        await _invitations.SendInvitationAsync(changeId, cancellationToken);

        return response;
    }
}
