using System;
using System.Linq;
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
/// Owner-side view of ONE campus's contact state. Masked addresses only — even the registrant never
/// reads an invited address back out of the API, because the API is not where that address is needed.
/// </summary>
public sealed class GetOperationalContactStateQueryHandler
    : IRequestHandler<GetOperationalContactStateQuery, OperationalContactStateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public GetOperationalContactStateQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactStateResponse> Handle(
        GetOperationalContactStateQuery request, CancellationToken cancellationToken)
    {
        var actorId = OperationalContactGuards.RequireAuthenticated(_writeFlag, _currentUser);

        var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
            _db, request.VisitRequestId, request.VisitInstanceId, cancellationToken);

        // Reading is allowed to the registrant and to whoever currently holds this campus.
        OperationalContactGuards.EnsureMayManageContact(visit, instance, actorId, allowCurrentContact: true);

        var confirmedMasked = instance.FormDetail is null
            ? null
            : Services.VisitRequestFingerprintBuilder.MaskEmail(
                Services.VisitRequestFingerprintBuilder.NormalizeEmail(
                    instance.FormDetail.OperationalContactEmail));

        var pending = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.VisitInstanceId == instance.VisitInstanceId
                        && c.Status == IdentityChangeStatuses.Pending)
            .Select(c => new
            {
                c.ChangeKind, c.NewEmailMasked, c.ExpiresAt, c.ResendCount, c.TokenVersion,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (pending is null)
            return new OperationalContactStateResponse(
                visit.VisitRequestId, instance.VisitInstanceId, instance.Status,
                instance.OperationalContactUserId is not null,
                instance.OperationalContactUserId is null ? null : confirmedMasked,
                instance.OperationalContactConfirmedAt,
                instance.OperationalContactConfirmationSource,
                null, null, null, null, 0, 0);

        // An overdue-but-not-yet-swept invitation reads EXPIRED; the sweep settles the row later.
        var effective = pending.ExpiresAt <= _clock.VietnamNow
            ? IdentityChangeStatuses.Expired
            : IdentityChangeStatuses.Pending;

        return new OperationalContactStateResponse(
            visit.VisitRequestId, instance.VisitInstanceId, instance.Status,
            instance.OperationalContactUserId is not null,
            instance.OperationalContactUserId is null ? null : confirmedMasked,
            instance.OperationalContactConfirmedAt,
            instance.OperationalContactConfirmationSource,
            pending.ChangeKind, effective, pending.NewEmailMasked,
            pending.ExpiresAt, pending.ResendCount, pending.TokenVersion);
    }
}

/// <summary>
/// Closes the in-flight invitation of ONE campus without changing who holds it.
///
/// For a TRANSFER that means the current contact simply keeps the campus. For an INITIAL_CONFIRMATION
/// it means the campus goes back to having no invitation at all — the gate stays shut, and the
/// registrant is expected to name someone. Cancelling is not a way to skip confirmation.
/// </summary>
public sealed class CancelOperationalContactChangeCommandHandler
    : IRequestHandler<CancelOperationalContactChangeCommand, OperationalContactManageResponse>
{
    private const int RetentionDays = 90;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IOperationalContactInvitationService _invitations;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public CancelOperationalContactChangeCommandHandler(
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
        CancelOperationalContactChangeCommand request, CancellationToken cancellationToken)
    {
        var actorId = OperationalContactGuards.RequireAuthenticated(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var correlationId = Guid.NewGuid().ToString("N");

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
            _db, request.VisitRequestId, request.VisitInstanceId, cancellationToken);

        var change = await _invitations.LockPendingChangeForInstanceAsync(
                instance.VisitInstanceId, cancellationToken)
            ?? throw new ConflictException(
                "Cơ sở này không có thay đổi đầu mối nào đang chờ xác nhận để hủy.",
                OperationalContactErrorCodes.ConfirmationNotFound);

        OperationalContactGuards.EnsureMayManageContact(visit, instance, actorId,
            allowCurrentContact: change.ChangeKind == IdentityChangeKinds.Transfer);

        await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitRequestIdentityChange, change.IdentityChangeId,
            "Lời mời đã bị hủy.", now, cancellationToken);

        change.Status = IdentityChangeStatuses.Cancelled;
        change.CancelledAt = now;
        change.Reason = reason ?? change.Reason;
        change.RetentionUntil = now.AddDays(RetentionDays);
        change.UpdatedAt = now;

        _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = change.IdentityChangeId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            EventType = "OPERATIONAL_CONTACT_INVITATION_CANCELLED",
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Cancelled,
            ActorUserId = actorId,
            EmailMasked = change.NewEmailMasked,
            Reason = reason,
            CorrelationId = correlationId,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "OPERATIONAL_CONTACT_INVITATION_CANCELLED",
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

        return new OperationalContactManageResponse(
            visit.VisitRequestId, instance.VisitInstanceId, instance.Status, visit.Status,
            ContactConfirmed: instance.OperationalContactUserId is not null,
            change.ChangeKind, IdentityChangeStatuses.Cancelled, change.NewEmailMasked,
            null, change.ResendCount, change.TokenVersion,
            change.ChangeKind == IdentityChangeKinds.Transfer
                ? "Đã hủy lời mời chuyển giao. Đầu mối vận hành hiện tại của cơ sở giữ nguyên quyền."
                : "Đã hủy lời mời xác nhận. Cơ sở này vẫn chưa có đầu mối vận hành.");
    }
}
