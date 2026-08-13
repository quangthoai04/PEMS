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
/// The old link dies FIRST, <c>token_version</c> is bumped, and the new link is minted — all three in
/// ONE transaction, with only the email left outside it. The ordering is what keeps two live links
/// from racing each other for the same campus (and the version is what makes the new invitation
/// distinguishable in the dispatcher's dedupe key); the shared transaction is what keeps a failed mint
/// from committing the kill on its own, which would strand the invitee with dead links and the
/// registrant with a pending change they cannot replace.
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
        // Minted inside the transaction below, dispatched after it commits.
        OperationalContactInvitationTokens invitationTokens;
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

            // ── A resend is not a neutral re-delivery of a TRANSFER: it renews the invitation's expiry
            //    and mints a fresh link, which is what keeps the handover applicable. So it answers the
            //    same lifecycle question as initiating one, and it answers it NOW — a campus that has
            //    started must not have its stale handover kept alive for another day. Cleanup still
            //    works: cancel and decline settle the invitation without touching who holds the campus.
            //
            //    Checked BEFORE anything is written, so a refusal leaves token_version, resend_count,
            //    expires_at and every outstanding link exactly as they were.
            //
            //    An initial confirmation is untouched by this: appointing a first contact is not a
            //    handover, and its own window is the campus's confirmation gate. ──
            if (change.ChangeKind == IdentityChangeKinds.Transfer)
                OperationalContactGuards.EnsureTransferWindowOpen(visit, instance);

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

            // Flush the kill + the bump so the mint below reads the version it is minting FOR, and so
            // the new token rows cannot be mistaken for the ones just invalidated.
            await _db.SaveChangesAsync(cancellationToken);

            // ── The replacement link is part of THIS transaction, and for resend that is not a
            //    refinement — it is the whole correctness of the operation. Every outstanding link for
            //    this campus has just been marked invalid a few lines up. Minting the new one after the
            //    commit means a mint failure lands on a campus whose invitation is still PENDING, whose
            //    old links are dead, and whose new link never existed: the invitee's email is now a
            //    row of broken URLs, and the registrant cannot re-invite either, because a pending
            //    change already occupies the campus. Minted here, that failure rolls the invalidation
            //    and the version bump back with it, and the previous links keep working.
            invitationTokens = OperationalContactGuards.RequireMintedLinks(
                await _invitations.MintInvitationTokensAsync(
                    change.IdentityChangeId, cancellationToken),
                change.IdentityChangeId);
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

        // AFTER commit: only the delivery. The new link is durable by now, so a mail-provider failure
        // leaves a resend that WORKED — the invitee can still be given the link, and another resend
        // (subject to the same cooldown and cap) is a retry rather than a repair.
        await _invitations.DispatchInvitationEmailAsync(changeId, invitationTokens, cancellationToken);

        return response;
    }
}
