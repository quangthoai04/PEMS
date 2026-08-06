using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// Proposes handing ONE decided campus to a new address (plan §3.3).
///
/// NOTHING changes here. The current contact keeps every right, the campus keeps its decision, its
/// host and its schedule, the request keeps its aggregate status, and the sibling campuses are not
/// even read. The only thing this writes is an invitation — the swap happens on accept, or never.
///
/// That is the difference from replacing a contact: a campus nobody has committed resources to can be
/// re-pointed outright, but a campus a Staff Leader has already approved must not lose its contact on
/// the say-so of someone who has not answered yet.
/// </summary>
public sealed class InitiateOperationalContactTransferCommandHandler
    : IRequestHandler<InitiateOperationalContactTransferCommand, OperationalContactManageResponse>
{
    private static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IOperationalContactInvitationService _invitations;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public InitiateOperationalContactTransferCommandHandler(
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
        InitiateOperationalContactTransferCommand request, CancellationToken cancellationToken)
    {
        var actorId = OperationalContactGuards.RequireAuthenticated(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        var newEmail = VisitRequestFingerprintBuilder.NormalizeEmail(request.Email);
        var newEmailMasked = VisitRequestFingerprintBuilder.MaskEmail(newEmail);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var expiresAt = OperationalContactGuards.ExpiryFor(IdentityChangeKinds.Transfer, now);

        ulong invitationId;
        OperationalContactManageResponse response;

        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
                _db, request.VisitRequestId, request.VisitInstanceId, cancellationToken);

            // The registrant, or the person currently holding THIS campus and handing it on.
            OperationalContactGuards.EnsureMayManageContact(visit, instance, actorId, allowCurrentContact: true);
            OperationalContactGuards.EnsureTransferWindowOpen(visit, instance, now);

            var currentContactId = instance.OperationalContactUserId
                ?? throw new ConflictException(
                    "Cơ sở này chưa có đầu mối vận hành nào để chuyển giao.",
                    OperationalContactErrorCodes.ConfirmationNotFound);

            var detail = instance.FormDetail
                ?? throw new NotFoundException("Chi tiết đơn của cơ sở này", instance.VisitInstanceId);

            var currentEmail = VisitRequestFingerprintBuilder.NormalizeEmail(detail.OperationalContactEmail);
            if (newEmail == currentEmail)
                throw new BusinessRuleException(
                    "Email mới trùng với email đầu mối vận hành hiện tại của cơ sở này.",
                    OperationalContactErrorCodes.ChangeConflict);

            // If the address already has an account it must be usable. An address with NO account is
            // fine — SSO provisions one when they accept. Nothing here reveals which case it was.
            var target = await _db.Users.AsNoTracking()
                .Where(u => u.Email == newEmail)
                .Select(u => new { u.UserId, u.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (target is not null && target.Status != UserStatuses.Active)
                throw new ConflictException(
                    "Tài khoản của email này đang không hoạt động nên không thể nhận vai trò đầu mối vận hành.",
                    OperationalContactErrorCodes.AccountInactive);
            if (target is not null && target.UserId == currentContactId)
                throw new BusinessRuleException(
                    "Người này đã là đầu mối vận hành của cơ sở này.",
                    OperationalContactErrorCodes.AlreadyConfirmed);

            // One in-flight invitation per campus. The DB has a unique pending-guard as the last line;
            // this pre-check exists so the common case gets a sentence instead of a constraint error.
            var pending = await _invitations.LockPendingChangeForInstanceAsync(
                instance.VisitInstanceId, cancellationToken);
            if (pending is not null)
                throw new ConflictException(
                    "Cơ sở này đang có một thay đổi đầu mối chờ xác nhận. Hãy hủy hoặc hoàn tất thay đổi đó trước.",
                    OperationalContactErrorCodes.ChangeConflict);

            var transfer = new VisitRequestIdentityChange
            {
                VisitRequestId = visit.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                ChangeKind = IdentityChangeKinds.Transfer,
                TokenVersion = 1,
                ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
                // Captured so the accept can prove the campus is still held by the person it was
                // proposed from — the invariant that actually protects the handover.
                OldUserId = currentContactId,
                OldEmailNormalized = currentEmail,
                NewUserId = null,
                NewEmailNormalized = newEmail,
                NewEmailMasked = newEmailMasked,
                PendingSnapshotJson = JsonSerializer.Serialize(new
                {
                    fullName = request.FullName.Trim(),
                    organization = string.IsNullOrWhiteSpace(request.Organization) ? null : request.Organization.Trim(),
                    phone = request.Phone.Trim(),
                    email = newEmail,
                }, Json),
                Status = IdentityChangeStatuses.Pending,
                ExpectedRequestRowVersion = (uint)instance.RowVersion,
                RequestedBy = actorId,
                RequestedAt = now,
                ExpiresAt = expiresAt,
                Reason = reason,
                ResendCount = 0,
                CreatedAt = now,
            };
            _db.VisitRequestIdentityChanges.Add(transfer);

            try
            {
                await _db.SaveChangesAsync(cancellationToken); // also fires the DB pending-guard
            }
            catch (DbUpdateException)
            {
                // A concurrent initiate lost the unique pending-guard race — same answer as the
                // pre-check above, so the caller cannot tell which path refused them.
                await tx.RollbackAsync(cancellationToken);
                throw new ConflictException(
                    "Cơ sở này đang có một thay đổi đầu mối chờ xác nhận. Hãy hủy hoặc hoàn tất thay đổi đó trước.",
                    OperationalContactErrorCodes.ChangeConflict);
            }

            _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = transfer.IdentityChangeId,
                VisitRequestId = visit.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                EventType = "OPERATIONAL_CONTACT_TRANSFER_REQUESTED",
                FromStatus = null,
                ToStatus = IdentityChangeStatuses.Pending,
                ActorUserId = actorId,
                EmailMasked = newEmailMasked,
                Reason = reason,
                CorrelationId = correlationId,
                CreatedAt = now,
            });
            var audit = new AuditLog
            {
                ActorUserId = actorId,
                Action = "OPERATIONAL_CONTACT_TRANSFER_REQUESTED",
                EntityType = "VisitRequestCampus",
                EntityId = instance.VisitInstanceId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                Reason = reason,
                CreatedAt = now,
            };
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "operational_contact_transfer_target",
                OldValueText = VisitRequestFingerprintBuilder.MaskEmail(currentEmail), // masked only
                NewValueText = newEmailMasked,
                CreatedAt = now,
            });
            _db.AuditLogs.Add(audit);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            invitationId = transfer.IdentityChangeId;
            response = new OperationalContactManageResponse(
                visit.VisitRequestId, instance.VisitInstanceId, instance.Status, visit.Status,
                ContactConfirmed: true,     // the CURRENT contact still holds the campus
                IdentityChangeKinds.Transfer, IdentityChangeStatuses.Pending, newEmailMasked,
                expiresAt, ResendCount: 0, TokenVersion: 1,
                "Đã gửi lời mời chuyển giao đầu mối vận hành cho cơ sở này. Đầu mối hiện tại giữ nguyên quyền cho tới khi người mới xác nhận.");
        }

        await _invitations.SendInvitationAsync(invitationId, cancellationToken);

        return response;
    }
}
