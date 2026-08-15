using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Application.EmailActions;
using PEMS.Application.Notifications.Common;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// The registrant changes the operational contact of ONE campus, before that campus has been decided
/// (plan §3.3).
///
/// Two outcomes, decided by the address alone:
///   • the registrant's own verified address → linked immediately, no invitation, no email. The
///     registrant already proved that address at submit time, so asking them to confirm it again by
///     email would be theatre.
///   • anything else → the campus relation is CLEARED and a fresh invitation goes out. Clearing it is
///     the important half: a campus with an unconfirmed contact re-closes the global gate, and every
///     Staff Leader on every campus of the request stops seeing it until the new person answers. That
///     is the intended cost of changing your mind, and it is why this command is refused once the
///     campus has a decision — at that point the handover is a transfer, which moves nothing until
///     the new person accepts.
///
/// Identity is never taken from the form. The address written here only says where an invitation may
/// be sent; who may act comes from <c>operational_contact_user_id</c>, and nothing else.
/// </summary>
public sealed class ReplaceOperationalContactCommandHandler
    : IRequestHandler<ReplaceOperationalContactCommand, OperationalContactManageResponse>
{
    private static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IOperationalContactInvitationService _invitations;
    private readonly IVisitRequestAggregateStatusService _aggregate;
    private readonly INotificationService _notifications;
    private readonly ILogger<ReplaceOperationalContactCommandHandler> _logger;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public ReplaceOperationalContactCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IOperationalContactInvitationService invitations, IVisitRequestAggregateStatusService aggregate,
        INotificationService notifications, ILogger<ReplaceOperationalContactCommandHandler> logger,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _invitations = invitations;
        _aggregate = aggregate;
        _notifications = notifications;
        _logger = logger;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactManageResponse> Handle(
        ReplaceOperationalContactCommand request, CancellationToken cancellationToken)
    {
        var actorId = OperationalContactGuards.RequireAuthenticated(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        var newEmail = VisitRequestFingerprintBuilder.NormalizeEmail(request.Email);
        var newEmailMasked = VisitRequestFingerprintBuilder.MaskEmail(newEmail);

        ulong? invitationToSend = null;
        // Minted inside the transaction below, dispatched after it commits.
        OperationalContactInvitationTokens? invitationTokens = null;
        ContactGateTransition gate;
        OperationalContactManageResponse response;

        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
                _db, request.VisitRequestId, request.VisitInstanceId, cancellationToken);

            OperationalContactGuards.EnsureMayManageContact(visit, instance, actorId, allowCurrentContact: false);
            OperationalContactGuards.EnsureReplaceWindowOpen(visit, instance);

            // The address has to be an external one. Checked BEFORE anything is written, so a refused
            // replacement leaves the campus exactly as it was — with its current contact, its current
            // invitation and its current status. It covers the self-match branch below too: a registrant
            // naming their own address gets linked with nobody confirming anything, which is precisely
            // the shortcut an internal account must not have.
            await OperationalContactEligibility.EnsureEmailMayHoldContactRoleAsync(
                _db, newEmail, cancellationToken);

            var detail = instance.FormDetail
                ?? throw new NotFoundException("Chi tiết đơn của cơ sở này", instance.VisitInstanceId);

            var oldMasked = VisitRequestFingerprintBuilder.MaskEmail(
                VisitRequestFingerprintBuilder.NormalizeEmail(detail.OperationalContactEmail));
            var previousContactId = instance.OperationalContactUserId;

            // ── Any in-flight invitation for THIS campus is superseded. A sibling campus's invitation
            //    is not touched — it is answering a different question. ──
            var pending = await _invitations.LockPendingChangeForInstanceAsync(
                instance.VisitInstanceId, cancellationToken);
            if (pending is not null)
            {
                await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                    _db, EmailActionTargetTypes.VisitRequestIdentityChange, pending.IdentityChangeId,
                    "Đầu mối vận hành của cơ sở đã được thay đổi.", now, cancellationToken);

                pending.Status = IdentityChangeStatuses.Superseded;
                pending.SupersededAt = now;
                pending.RetentionUntil = now.AddDays(90);
                pending.UpdatedAt = now;

                _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
                {
                    IdentityChangeId = pending.IdentityChangeId,
                    VisitRequestId = visit.VisitRequestId,
                    VisitInstanceId = instance.VisitInstanceId,
                    EventType = "OPERATIONAL_CONTACT_INVITATION_SUPERSEDED",
                    FromStatus = IdentityChangeStatuses.Pending,
                    ToStatus = IdentityChangeStatuses.Superseded,
                    ActorUserId = actorId,
                    EmailMasked = pending.NewEmailMasked,
                    Reason = "Người đăng ký đổi đầu mối vận hành của cơ sở.",
                    CorrelationId = correlationId,
                    CreatedAt = now,
                });
            }

            // ── The campus's contact snapshot. Content only; it grants nothing. ──
            detail.OperationalContactFullName = request.FullName.Trim();
            detail.OperationalContactOrganization =
                string.IsNullOrWhiteSpace(request.Organization) ? null : request.Organization.Trim();
            detail.OperationalContactJobTitle = request.JobTitle.Trim();
            detail.OperationalContactPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            detail.OperationalContactEmail = newEmail;
            // A different person now holds the role, so the link naming which delegation member the
            // PREVIOUS contact was is stale — and a stale link is not harmless here: the biên bản
            // decides the "· Đầu mối" badge from it, so it would go on naming the person who was
            // replaced and would never list the replacement. Cleared, not re-pointed: a replacement
            // is given as an email address and a delegation member row has none, so nothing could
            // honestly tie this contact to one. See AcceptOperationalContactConfirmationCommandHandler
            // .ApplyTransfer, which clears it for the same reason on the post-approval path.
            detail.OperationalContactGuestMemberId = null;

            var registrantEmail = VisitRequestFingerprintBuilder.NormalizeEmail(visit.RegistrantEmail);
            // Self-match needs BOTH a matching address and a verified registrant account to link to.
            // Without an account there is nobody to make the contact, so it falls through to an
            // invitation rather than silently leaving the campus ownerless.
            var linksRegistrant = newEmail == registrantEmail
                                  && visit.RegistrantUserId is not null
                                  && visit.EmailVerifiedAt is not null;

            string message;
            if (linksRegistrant)
            {
                instance.OperationalContactUserId = visit.RegistrantUserId;
                instance.OperationalContactConfirmedAt = now;
                instance.OperationalContactConfirmationSource = OperationalContactSources.RegistrantSelfMatch;
                if (instance.Status == VisitInstanceStatuses.WaitingContactConfirmation)
                    instance.Status = VisitInstanceStatuses.WaitingRequestApproval;

                message = "Bạn đã trở thành đầu mối vận hành của cơ sở này (email trùng email người đăng ký đã xác thực).";
            }
            else
            {
                // The campus loses its contact until the new address answers — this is what shuts the
                // global gate again.
                instance.OperationalContactUserId = null;
                instance.OperationalContactConfirmedAt = null;
                instance.OperationalContactConfirmationSource = null;
                instance.Status = VisitInstanceStatuses.WaitingContactConfirmation;

                var invitation = new VisitRequestIdentityChange
                {
                    VisitRequestId = visit.VisitRequestId,
                    VisitInstanceId = instance.VisitInstanceId,
                    ChangeKind = IdentityChangeKinds.InitialConfirmation,
                    TokenVersion = 1,
                    ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
                    OldUserId = null,
                    NewUserId = null,
                    NewEmailNormalized = newEmail,
                    NewEmailMasked = newEmailMasked,
                    PendingSnapshotJson = JsonSerializer.Serialize(new
                    {
                        fullName = detail.OperationalContactFullName,
                        organization = detail.OperationalContactOrganization,
                        jobTitle = detail.OperationalContactJobTitle,
                        phone = detail.OperationalContactPhone,
                        email = newEmail,
                    }, Json),
                    Status = IdentityChangeStatuses.Pending,
                    ExpectedRequestRowVersion = (uint)instance.RowVersion,
                    RequestedBy = actorId,
                    RequestedAt = now,
                    ExpiresAt = OperationalContactGuards.ExpiryFor(IdentityChangeKinds.InitialConfirmation, now),
                    ResendCount = 0,
                    CreatedAt = now,
                };
                _db.VisitRequestIdentityChanges.Add(invitation);
                await _db.SaveChangesAsync(cancellationToken); // resolve the id its event points at

                _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
                {
                    IdentityChangeId = invitation.IdentityChangeId,
                    VisitRequestId = visit.VisitRequestId,
                    VisitInstanceId = instance.VisitInstanceId,
                    EventType = "OPERATIONAL_CONTACT_INVITATION_CREATED",
                    FromStatus = null,
                    ToStatus = IdentityChangeStatuses.Pending,
                    ActorUserId = actorId,
                    EmailMasked = newEmailMasked,
                    CorrelationId = correlationId,
                    CreatedAt = now,
                });

                // ── The link is part of THIS transaction, not a follow-up step. ─────────────────
                // The campus's contact snapshot has just been rewritten and its
                // operational_contact_user_id cleared, so the ONLY way anyone regains the campus is
                // through this invitation's link. Minting it after the commit could leave the campus
                // re-pointed at an address that was never written to, holding a PENDING change that
                // blocks the next replace — and the browser would be told the whole thing failed.
                // Minted here, a token failure rolls the replace back and the refusal is the truth.
                invitationToSend = invitation.IdentityChangeId;
                invitationTokens = OperationalContactGuards.RequireMintedLinks(
                    await _invitations.MintInvitationTokensAsync(
                        invitation.IdentityChangeId, cancellationToken),
                    invitation.IdentityChangeId);

                message = previousContactId is null
                    ? "Đã cập nhật đầu mối vận hành và gửi lời mời xác nhận tới email mới."
                    : "Đã đổi đầu mối vận hành của cơ sở. Cơ sở này chờ người mới xác nhận, và toàn bộ đơn tạm dừng ở cổng xác nhận cho tới khi đủ đầu mối.";
            }

            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;

            gate = _aggregate.Apply(visit);
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;

            var audit = new AuditLog
            {
                ActorUserId = actorId,
                Action = "OPERATIONAL_CONTACT_REPLACED",
                EntityType = "VisitRequestCampus",
                EntityId = instance.VisitInstanceId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                CreatedAt = now,
            };
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "operational_contact_email",
                OldValueText = oldMasked,   // masked only — audit never keeps a full address
                NewValueText = newEmailMasked,
                CreatedAt = now,
            });
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "operational_contact_user_id",
                OldValueText = previousContactId?.ToString(),
                NewValueText = instance.OperationalContactUserId?.ToString(),
                CreatedAt = now,
            });
            _db.AuditLogs.Add(audit);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            response = new OperationalContactManageResponse(
                visit.VisitRequestId, instance.VisitInstanceId, instance.Status, visit.Status,
                ContactConfirmed: instance.OperationalContactUserId is not null,
                invitationToSend is null ? null : IdentityChangeKinds.InitialConfirmation,
                invitationToSend is null ? null : IdentityChangeStatuses.Pending,
                invitationToSend is null ? null : newEmailMasked,
                invitationToSend is null ? null : OperationalContactGuards.ExpiryFor(IdentityChangeKinds.InitialConfirmation, now),
                ResendCount: 0,
                TokenVersion: invitationToSend is null ? 0u : 1u,
                message);
        }

        // ── Only the DELIVERY is best-effort, and only after the commit. Sending inside the
        //    transaction would let a rollback leave somebody holding an invitation to a campus that
        //    never changed; failing here leaves a durable, answerable invitation that resend recovers. ──
        if (invitationToSend is not null && invitationTokens is not null)
            await _invitations.DispatchInvitationEmailAsync(
                invitationToSend.Value, invitationTokens, cancellationToken);

        // Replacing the last outstanding contact with the registrant's own address can complete the
        // request, which opens the gate here rather than on an accept.
        if (gate.Opened)
            await OperationalContactNotifier.AnnounceApprovalReadyAsync(
                _db, _notifications, _logger, response.VisitRequestId, gate.GateRevision, cancellationToken);

        return response;
    }
}
