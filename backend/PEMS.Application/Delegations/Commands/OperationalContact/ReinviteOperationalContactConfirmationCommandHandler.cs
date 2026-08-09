using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// Re-opens the confirmation of ONE campus for the address it already names, after the previous
/// invitation ended without an answer (cancelled, declined, expired).
///
/// <para>
/// It is deliberately NOT a variant of replace. Replace exists to change WHO the contact is, and pays
/// the price for that: it rewrites the campus's contact snapshot, supersedes whatever was in flight,
/// clears <c>operational_contact_user_id</c> and writes a "the registrant changed the contact" entry
/// into the campus's identity history. None of that is true here — nobody is being changed, the same
/// person is being asked again — so none of it happens. One new invitation, one honest event.
/// </para>
/// <para>
/// The three refusals below are the whole rule. A campus that already has a confirmed contact has
/// nothing to confirm; a campus with a live invitation should RESEND rather than open a second one
/// (the DB allows at most one PENDING change per campus anyway); and a campus past the undecided
/// stages is transfer territory, where the handshake protects a decision somebody has already taken.
/// </para>
/// </summary>
public sealed class ReinviteOperationalContactConfirmationCommandHandler
    : IRequestHandler<ReinviteOperationalContactConfirmationCommand, OperationalContactManageResponse>
{
    private static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IOperationalContactInvitationService _invitations;
    private readonly IVisitRequestAggregateStatusService _aggregate;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public ReinviteOperationalContactConfirmationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IOperationalContactInvitationService invitations, IVisitRequestAggregateStatusService aggregate,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _invitations = invitations;
        _aggregate = aggregate;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactManageResponse> Handle(
        ReinviteOperationalContactConfirmationCommand request, CancellationToken cancellationToken)
    {
        var actorId = OperationalContactGuards.RequireAuthenticated(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        ulong invitationToSend;
        // Minted inside the transaction below, dispatched after it commits.
        OperationalContactInvitationTokens invitationTokens;
        DateTime expiresAt;
        string emailMasked;
        OperationalContactManageResponse response;

        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
                _db, request.VisitRequestId, request.VisitInstanceId, cancellationToken);

            // Re-inviting is managing the contact, and the campus has no confirmed holder to speak for
            // itself — so this is the registrant's alone, exactly like replace.
            OperationalContactGuards.EnsureMayManageContact(visit, instance, actorId, allowCurrentContact: false);

            // Same lifecycle window as replace: the two undecided stages. Reuses the guard so the two
            // commands cannot drift on what "still undecided" means.
            OperationalContactGuards.EnsureReplaceWindowOpen(visit, instance);

            if (instance.OperationalContactUserId is not null)
                throw new ConflictException(
                    "Cơ sở này đã có đầu mối vận hành xác nhận nên không cần mời lại.",
                    OperationalContactErrorCodes.ChangeConflict);

            // Locked, not merely read: two clicks on "Gửi lời mời mới" must not both find "no pending
            // invitation" and each open one.
            var existing = await _invitations.LockPendingChangeForInstanceAsync(
                instance.VisitInstanceId, cancellationToken);
            if (existing is not null)
                throw new ConflictException(
                    "Cơ sở này đang có lời mời xác nhận chờ trả lời. Hãy dùng chức năng gửi lại lời mời.",
                    OperationalContactErrorCodes.ChangeConflict);

            var detail = instance.FormDetail
                ?? throw new NotFoundException("Chi tiết đơn của cơ sở này", instance.VisitInstanceId);

            var email = VisitRequestFingerprintBuilder.NormalizeEmail(detail.OperationalContactEmail);
            if (string.IsNullOrWhiteSpace(email))
                throw new BusinessRuleException(
                    "Cơ sở này chưa có email đầu mối vận hành để gửi lời mời.",
                    VisitRequestErrorCodes.OperationalContactEmailRequired);
            emailMasked = VisitRequestFingerprintBuilder.MaskEmail(email);

            // A cancelled invitation may have left the campus at WAITING_REQUEST_APPROVAL if it was a
            // correction of an already-confirmed contact. Nobody holds it now, so it belongs back at
            // the contact gate — and the aggregate below re-derives the request status from that.
            instance.Status = VisitInstanceStatuses.WaitingContactConfirmation;

            expiresAt = OperationalContactGuards.ExpiryFor(IdentityChangeKinds.InitialConfirmation, now);
            var invitation = new VisitRequestIdentityChange
            {
                VisitRequestId = visit.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                ChangeKind = IdentityChangeKinds.InitialConfirmation,
                TokenVersion = 1,
                ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
                OldUserId = null,
                NewUserId = null,
                NewEmailNormalized = email,
                NewEmailMasked = emailMasked,
                PendingSnapshotJson = JsonSerializer.Serialize(new
                {
                    fullName = detail.OperationalContactFullName,
                    organization = detail.OperationalContactOrganization,
                    jobTitle = detail.OperationalContactJobTitle,
                    phone = detail.OperationalContactPhone,
                    email,
                }, Json),
                Status = IdentityChangeStatuses.Pending,
                ExpectedRequestRowVersion = (uint)instance.RowVersion,
                RequestedBy = actorId,
                RequestedAt = now,
                ExpiresAt = expiresAt,
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
                EmailMasked = emailMasked,
                Reason = "Gửi lại lời mời xác nhận cho đầu mối hiện có của cơ sở.",
                CorrelationId = correlationId,
                CreatedAt = now,
            });

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                Action = "OPERATIONAL_CONTACT_REINVITED",
                EntityType = "VisitRequestCampus",
                EntityId = instance.VisitInstanceId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                CreatedAt = now,
            });

            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;

            // ── The campus was just put back behind the contact gate, and the REQUEST's status is a
            //    function of its campuses — so it has to be re-derived from the state above, by the one
            //    service that mirrors the DB trigger. Skipping it left the tracked request holding
            //    whatever it said before (PENDING_APPROVAL, say) while a campus underneath it was
            //    waiting for a contact again: the response answered with the old status, and — worse —
            //    the gate revision never moved, so the approval-ready mail for the NEXT opening would
            //    be deduped away as already sent. Apply also owns the ContactGateRevision bump; nothing
            //    here touches it by hand. ──
            _aggregate.Apply(visit);

            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;

            invitationToSend = invitation.IdentityChangeId;

            // ── The link is part of THIS transaction. A re-invite exists only to be answered, and the
            //    row it writes is the one PENDING change this campus is allowed — so minting after the
            //    commit could leave the campus back at the contact gate with a pending invitation that
            //    has no link, which refuses the next re-invite ("already has one waiting") and cannot
            //    itself be answered. Minted here, a token failure rolls the re-invite back with it. ──
            invitationTokens = OperationalContactGuards.RequireMintedLinks(
                await _invitations.MintInvitationTokensAsync(
                    invitation.IdentityChangeId, cancellationToken),
                invitation.IdentityChangeId);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            response = new OperationalContactManageResponse(
                visit.VisitRequestId, instance.VisitInstanceId, instance.Status, visit.Status,
                ContactConfirmed: false,
                IdentityChangeKinds.InitialConfirmation, IdentityChangeStatuses.Pending, emailMasked,
                expiresAt, ResendCount: 0, TokenVersion: 1u,
                "Đã gửi lời mời xác nhận mới tới đầu mối của cơ sở này.");
        }

        // After commit, for the same reason replace sends after commit: a rolled-back invitation must
        // never leave somebody holding a link to a campus that did not change. Delivery alone is
        // best-effort — the invitation and its link are already durable, and resend recovers a bounce.
        await _invitations.DispatchInvitationEmailAsync(
            invitationToSend, invitationTokens, cancellationToken);

        return response;
    }
}
