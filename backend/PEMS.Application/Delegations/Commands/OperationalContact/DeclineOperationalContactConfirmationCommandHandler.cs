using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// The invited person declines ONE campus.
///
/// Same authentication bar as accepting: taking on a campus and refusing it are both answers to a
/// grant of authority, so only the invited account may give either. Declining on someone's behalf
/// would be as damaging as accepting on their behalf.
///
/// Nothing else moves:
///   • INITIAL_CONFIRMATION declined — the campus keeps no contact, so the global gate stays shut and
///     every Staff Leader stays blocked. That is intended: the request is genuinely not ready. The
///     registrant resends or names a different address.
///   • TRANSFER declined — the current contact keeps the campus, with its decision, host and schedule
///     exactly as they were.
///
/// The request is never cancelled by a decline, and sibling campuses never notice.
/// </summary>
public sealed class DeclineOperationalContactConfirmationCommandHandler
    : IRequestHandler<DeclineOperationalContactConfirmationCommand, OperationalContactActionResponse>
{
    private const int RetentionDays = 90;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _tokens;
    private readonly IOperationalContactInvitationService _invitations;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public DeclineOperationalContactConfirmationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IEmailActionTokenService tokens, IOperationalContactInvitationService invitations,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _tokens = tokens;
        _invitations = invitations;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactActionResponse> Handle(
        DeclineOperationalContactConfirmationCommand request, CancellationToken cancellationToken)
    {
        // Session actor normally; a token-proven actor when the public (no-login) handler delegates here.
        var actorId = OperationalContactGuards.ResolveActor(_writeFlag, _currentUser, request.ActingUserId);
        // From a link, or from the invitation id when a signed-in invitee declines inside the product.
        var changeId = request.Token is null
            ? request.IdentityChangeId
              ?? throw new ConflictException(
                  "Thiếu thông tin lời mời.", OperationalContactErrorCodes.ConfirmationNotFound)
            : await OperationalContactGuards.ResolveChangeIdAsync(
                  _db, _tokens, request.Token, cancellationToken)
              ?? throw new ConflictException(
                  "Liên kết không hợp lệ.", OperationalContactErrorCodes.ConfirmationNotFound);

        var now = _clock.VietnamNow;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var correlationId = Guid.NewGuid().ToString("N");

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var change = await _invitations.LockChangeAsync(changeId, cancellationToken)
            ?? throw new ConflictException(
                "Liên kết không hợp lệ.", OperationalContactErrorCodes.ConfirmationNotFound);

        var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
            _db, change.VisitRequestId, change.VisitInstanceId, cancellationToken);

        // Replaying a decline returns the same answer rather than an error — the person already said no.
        if (change.Status == IdentityChangeStatuses.Declined && change.NewUserId == actorId)
        {
            await tx.CommitAsync(cancellationToken);
            return Respond(visit, instance, change, IdentityChangeStatuses.Declined, idempotent: true);
        }

        OperationalContactGuards.EnsurePending(change, now);

        // The link must be a DECLINE link — see the mirror check in the accept handler. Null when a
        // signed-in invitee declines without one.
        var token = request.Token is null
            ? null
            : await OperationalContactGuards.LoadLiveTokenAsync(
                _db, _tokens, request.Token, changeId, now, cancellationToken,
                requiredIntendedAction: EmailIntendedActions.Decline);

        var actor = await _db.Users.FirstOrDefaultAsync(u => u.UserId == actorId, cancellationToken)
            ?? throw new ForbiddenException();

        // Only the invited address may answer. An inactive account is still allowed to DECLINE: refusing
        // a role you cannot perform is exactly what an inactive person should be able to do, and leaving
        // the invitation hanging helps nobody.
        var actorEmail = Services.VisitRequestFingerprintBuilder.NormalizeEmail(actor.Email);
        if (actorEmail != change.NewEmailNormalized)
            throw new ConflictException(
                "Tài khoản đang đăng nhập không trùng với email được mời.",
                OperationalContactErrorCodes.EmailMismatch);

        change.Status = IdentityChangeStatuses.Declined;
        change.DeclinedAt = now;
        change.NewUserId = actorId;
        change.Reason = reason;
        change.RetentionUntil = now.AddDays(RetentionDays);
        change.UpdatedAt = now;

        if (token is not null)
        {
            token.UsedAt = now;
            token.UsedAction = EmailIntendedActions.Decline;
            token.ResultStatus = EmailActionResultStatuses.Success;
            token.RecipientUserId = actorId;
        }

        await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitRequestIdentityChange, change.IdentityChangeId,
            "Lời mời đã bị từ chối.", now, cancellationToken);

        var eventType = change.ChangeKind == IdentityChangeKinds.Transfer
            ? "OPERATIONAL_CONTACT_TRANSFER_DECLINED"
            : "OPERATIONAL_CONTACT_CONFIRMATION_DECLINED";

        _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = change.IdentityChangeId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            EventType = eventType,
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Declined,
            ActorUserId = actorId,
            EmailMasked = change.NewEmailMasked,
            Reason = reason,
            CorrelationId = correlationId,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = eventType,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            VisitRequestId = visit.VisitRequestId,
            CorrelationId = correlationId,
            SourceType = "IDENTITY",
            Reason = reason,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Respond(visit, instance, change, IdentityChangeStatuses.Declined, idempotent: false);
    }

    private static OperationalContactActionResponse Respond(
        VisitRequest visit, VisitRequestCampus instance, VisitRequestIdentityChange change,
        string changeStatus, bool idempotent)
        => new(
            visit.VisitRequestId, instance.VisitInstanceId, visit.RequestCode ?? string.Empty,
            change.ChangeKind, changeStatus, instance.Status, visit.Status, idempotent,
            change.ChangeKind == IdentityChangeKinds.Transfer
                ? "Bạn đã từ chối nhận vai trò đầu mối vận hành. Đầu mối hiện tại của cơ sở này giữ nguyên quyền."
                : "Bạn đã từ chối làm đầu mối vận hành. Đơn đăng ký không bị hủy; người đăng ký có thể chỉ định người khác.");
}
