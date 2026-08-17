using System;
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
using PEMS.Application.Delegations.Services;
using PEMS.Application.EmailActions;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// The invited person takes on ONE campus (plan §3.2).
///
/// Both kinds land here, because a link is a link — the invitation itself knows which it is:
///   • INITIAL_CONFIRMATION — the campus has nobody yet. Accepting links the account, moves the campus
///     to WAITING_REQUEST_APPROVAL, and — if this was the last outstanding campus of the request —
///     opens the global gate so every campus's Staff Leader can finally see it.
///   • TRANSFER — the campus already has a contact. Accepting is the ONLY moment the relation moves:
///     until this commit the previous contact keeps every right. The campus decision, its host, its
///     schedule and every sibling campus are untouched.
///
/// Authority comes from the account, never from the link: possession of a token proves nothing on its
/// own, so the caller must be signed in as the invited address and be ACTIVE. Internal accounts are
/// allowed on purpose (plan §1.7) — an FPTU staff member can run a campus.
///
/// Concurrency: the invitation row is locked FOR UPDATE, so a concurrent accept/decline/resend
/// serializes and the loser sees a settled state. A replay by the same accepted account returns the
/// applied result instead of swapping anything a second time.
/// </summary>
public sealed class AcceptOperationalContactConfirmationCommandHandler
    : IRequestHandler<AcceptOperationalContactConfirmationCommand, OperationalContactActionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _tokens;
    private readonly IOperationalContactInvitationService _invitations;
    private readonly IVisitRequestAggregateStatusService _aggregate;
    private readonly IProposedHostActivationService _activation;
    private readonly INotificationService _notifications;
    private readonly ILogger<AcceptOperationalContactConfirmationCommandHandler> _logger;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public AcceptOperationalContactConfirmationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IEmailActionTokenService tokens, IOperationalContactInvitationService invitations,
        IVisitRequestAggregateStatusService aggregate,
        IProposedHostActivationService activation, INotificationService notifications,
        ILogger<AcceptOperationalContactConfirmationCommandHandler> logger,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _activation = activation;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _tokens = tokens;
        _invitations = invitations;
        _aggregate = aggregate;
        _notifications = notifications;
        _logger = logger;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactActionResponse> Handle(
        AcceptOperationalContactConfirmationCommand request, CancellationToken cancellationToken)
    {
        // Session actor normally; a token-proven actor when the public (no-login) handler delegates here.
        var actorId = OperationalContactGuards.ResolveActor(_writeFlag, _currentUser, request.ActingUserId);
        // From a link, or — for a signed-in invitee answering inside the product — from the
        // invitation id, with the session standing in for the token. Either way the identity check
        // below (EnsureActorMayTakeContactRole) is what actually authorizes the answer.
        var changeId = request.Token is null
            ? request.IdentityChangeId
              ?? throw new ConflictException(
                  "Thiếu thông tin lời mời.", OperationalContactErrorCodes.ConfirmationNotFound)
            : await OperationalContactGuards.ResolveChangeIdAsync(
                  _db, _tokens, request.Token, cancellationToken)
              ?? throw new ConflictException(
                  "Liên kết không hợp lệ.", OperationalContactErrorCodes.ConfirmationNotFound);

        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        OperationalContactActionResponse response;
        ContactGateTransition gate;
        ulong? previousContactId;
        string campusLabel;
        IReadOnlyList<ProposedHostActivation> activations = System.Array.Empty<ProposedHostActivation>();
        VisitRequest confirmedVisit;

        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var change = await _invitations.LockChangeAsync(changeId, cancellationToken)
                ?? throw new ConflictException(
                    "Liên kết không hợp lệ.", OperationalContactErrorCodes.ConfirmationNotFound);

            var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
                _db, change.VisitRequestId, change.VisitInstanceId, cancellationToken);
            confirmedVisit = visit;

            // ── Idempotent replay: the SAME accepted account retries the same link. Return what already
            //    happened rather than swapping a second time — a double-submitted form must not look
            //    like a failure to the person who succeeded. ──
            if (change.Status == IdentityChangeStatuses.Applied && change.NewUserId == actorId)
            {
                await tx.CommitAsync(cancellationToken);
                return new OperationalContactActionResponse(
                    visit.VisitRequestId, instance.VisitInstanceId, visit.RequestCode ?? string.Empty,
                    change.ChangeKind, IdentityChangeStatuses.Applied, instance.Status, visit.Status,
                    Idempotent: true,
                    "Bạn đã là đầu mối vận hành của cơ sở này.");
            }

            OperationalContactGuards.EnsurePending(change, now);

            // The link must be an ACCEPT link. An invitation now carries one per answer, so the
            // decline link cannot be turned into an acceptance by posting it here. A signed-in
            // invitee answering without a link has no token to consume — every outstanding link of
            // this invitation is invalidated below regardless of how the answer arrived.
            var token = request.Token is null
                ? null
                : await OperationalContactGuards.LoadLiveTokenAsync(
                    _db, _tokens, request.Token, changeId, now, cancellationToken,
                    requiredIntendedAction: EmailIntendedActions.Accept);

            var actor = await _db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == actorId, cancellationToken)
                ?? throw new ForbiddenException();
            OperationalContactGuards.EnsureActorMayTakeContactRole(actor, change);

            EnsureCampusStillAcceptsContact(visit, instance);

            // ── A pending TRANSFER is re-tested against the campus AS IT IS NOW, not as it was when
            //    the invitation was written. The invitation is answerable for a day, and a campus can
            //    start inside that day: without this, a handover proposed at 08:55 for a 09:00 visit
            //    would still swap the contact at 09:02, mid-visit, on the strength of a link that was
            //    legal when it was sent. The lifecycle answer belongs to the moment of the answer.
            //
            //    An initial confirmation is NOT re-tested: it appoints a contact to a campus that has
            //    none, which is what EnsureCampusStillAcceptsContact above already answers. ──
            if (change.ChangeKind == IdentityChangeKinds.Transfer)
                OperationalContactGuards.EnsureTransferWindowOpen(visit, instance);

            previousContactId = instance.OperationalContactUserId;
            campusLabel = await CampusLabelAsync(instance, cancellationToken);

            if (change.ChangeKind == IdentityChangeKinds.Transfer)
                ApplyTransfer(change, instance, actor, actorId, now);
            else
                ApplyInitialConfirmation(change, instance, actorId, now);

            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;

            change.Status = IdentityChangeStatuses.Applied;
            change.AppliedAt = now;
            change.NewUserId = actorId;
            change.UpdatedAt = now;

            if (token is not null)
            {
                token.UsedAt = now;
                token.UsedAction = EmailIntendedActions.Accept;
                token.ResultStatus = EmailActionResultStatuses.Success;
                token.RecipientUserId = actorId;
            }

            // Every other outstanding link of this invitation (from a resend) dies with the answer.
            await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _db, EmailActionTargetTypes.VisitRequestIdentityChange, change.IdentityChangeId,
                "Lời mời đã được chấp nhận.", now, cancellationToken);

            // ── Recompute the request from its campuses. This is where the last confirmation of a
            //    multi-campus request opens the gate and bumps its revision. ──
            gate = _aggregate.Apply(visit);
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;

            WriteAudit(change, visit, instance, actorId, previousContactId, correlationId, now);

            await _db.SaveChangesAsync(cancellationToken);

            // ── The gate just opened: every campus that had a reception host preauthorized gets it
            //    revalidated and activated now (plan §6.1 steps 6-8). This is a SEPARATE flush on
            //    purpose. The DB refuses a campus reaching ASSIGNED while its request row still reads
            //    PENDING_CONTACT_CONFIRMATION, and EF writes visit_request_campuses before
            //    visit_requests — so folding the activation into the flush above would send the
            //    ASSIGNED update while the request still said the gate was shut.
            //
            //    A proposal that no longer holds does NOT fail this confirmation (§2.1 branch E): the
            //    campus falls back to WAITING_REQUEST_APPROVAL and its Staff Leader picks somebody. ──
            if (gate.Opened)
            {
                activations = await _activation.ActivateAsync(visit, now, cancellationToken);
                if (activations.Count > 0)
                {
                    _aggregate.Apply(visit);
                    WriteActivationAudits(visit, activations, correlationId, now);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            await tx.CommitAsync(cancellationToken);

            response = new OperationalContactActionResponse(
                visit.VisitRequestId, instance.VisitInstanceId, visit.RequestCode ?? string.Empty,
                change.ChangeKind, IdentityChangeStatuses.Applied, instance.Status, visit.Status,
                Idempotent: false,
                change.ChangeKind == IdentityChangeKinds.Transfer
                    ? $"Bạn đã trở thành đầu mối vận hành tại {campusLabel}. Đầu mối trước không còn quyền quản lý cơ sở này."
                    : $"Bạn đã xác nhận làm đầu mối vận hành tại {campusLabel}.");

            // Captured for the post-commit block below.
            previousContactId = change.ChangeKind == IdentityChangeKinds.Transfer ? previousContactId : null;
        }

        // ── After commit. Every dispatch is best-effort: the relation is already durable. ──
        if (gate.Opened)
            await OperationalContactNotifier.AnnounceApprovalReadyAsync(
                _db, _notifications, _logger, response.VisitRequestId, gate.GateRevision, cancellationToken);

        if (activations.Count > 0)
            await ProposedHostNotifier.AnnounceOutcomeAsync(
                _db, _notifications, _logger, confirmedVisit, hadProposals: true, activations,
                actorId, cancellationToken);

        if (previousContactId is not null)
        {
            var registrantId = await _db.VisitRequests.AsNoTracking()
                .Where(v => v.VisitRequestId == response.VisitRequestId)
                .Select(v => v.RegistrantUserId)
                .FirstOrDefaultAsync(cancellationToken);

            await OperationalContactNotifier.AnnounceTransferAppliedAsync(
                _notifications, _logger, response.VisitRequestId, response.VisitInstanceId,
                response.RequestCode, campusLabel, previousContactId, registrantId, actorId,
                cancellationToken);
        }

        return response;
    }

    /// <summary>
    /// The campus must still be somewhere an invitation can land. A cancelled or rejected campus has
    /// no operational contact to appoint, and a cancelled request has no campuses worth appointing to.
    /// </summary>
    private static void EnsureCampusStillAcceptsContact(VisitRequest visit, VisitRequestCampus instance)
    {
        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new ConflictException(
                "Đơn đăng ký đã bị hủy nên lời mời không còn hiệu lực.",
                OperationalContactErrorCodes.ChangeConflict);

        if (instance.Status is VisitInstanceStatuses.Cancelled or VisitInstanceStatuses.Rejected)
            throw new ConflictException(
                "Cơ sở này đã bị hủy hoặc từ chối nên lời mời không còn hiệu lực.",
                OperationalContactErrorCodes.ChangeConflict);
    }

    /// <summary>
    /// First contact for a campus that has none. Someone else having confirmed in the meantime is a
    /// conflict, not a silent overwrite — two people must never both believe they run one campus.
    /// </summary>
    private static void ApplyInitialConfirmation(
        VisitRequestIdentityChange change, VisitRequestCampus instance, ulong actorId, DateTime now)
    {
        if (instance.OperationalContactUserId is not null)
            throw new ConflictException(
                "Cơ sở này đã có đầu mối vận hành xác nhận.",
                OperationalContactErrorCodes.AlreadyConfirmed);

        instance.OperationalContactUserId = actorId;
        instance.OperationalContactConfirmedAt = now;
        instance.OperationalContactConfirmationSource = OperationalContactSources.EmailConfirmation;

        // Only this campus advances. A sibling still waiting for its own contact stays where it is,
        // and the request-level gate stays shut until every one of them has answered.
        if (instance.Status == VisitInstanceStatuses.WaitingContactConfirmation)
            instance.Status = VisitInstanceStatuses.WaitingRequestApproval;

        change.OldUserId = null;
    }

    /// <summary>
    /// Hand-over of a campus that already has a contact. The holder must still be exactly the person
    /// the transfer was proposed from: if the campus changed hands since the invitation, this
    /// invitation is answering a question nobody is asking any more.
    ///
    /// Deliberately NOT gated on the campus row version. That number moves for agenda edits, prep
    /// notes and reminder settings — unrelated work that would brick every transfer proposed before
    /// it. The invariant that actually protects the handover is the holder check right below.
    /// </summary>
    private static void ApplyTransfer(
        VisitRequestIdentityChange change, VisitRequestCampus instance, User actor, ulong actorId, DateTime now)
    {
        if (instance.OperationalContactUserId != change.OldUserId)
            throw new ConflictException(
                "Đầu mối vận hành của cơ sở này đã thay đổi ngoài lời mời này nên lời mời không còn hiệu lực.",
                OperationalContactErrorCodes.ChangeConflict);

        if (instance.OperationalContactUserId == actorId)
            throw new ConflictException(
                "Bạn đã là đầu mối vận hành của cơ sở này.",
                OperationalContactErrorCodes.AlreadyConfirmed);

        // ── Organization is required on every path that can raise a TRANSFER today (Save/Replace/
        //    Transfer's own validators), but this invitation may predate that rule. Checked BEFORE any
        //    mutation below — including instance.OperationalContactUserId — so a legacy snapshot with a
        //    blank or unreadable Organization is refused outright rather than partially applied: no
        //    relation moves, no campus/request status changes, and (since this refusal happens inside
        //    the caller's transaction, before its SaveChangesAsync) nothing reaches the database. ──
        var snapshot = PendingContactSnapshot.Read(change.PendingSnapshotJson);
        if (string.IsNullOrWhiteSpace(snapshot?.ResolvedOrganization))
            throw new BusinessRuleException(
                "Đơn vị công tác của đầu mối vận hành đang thiếu. Vui lòng cập nhật thông tin đầu mối và " +
                "gửi lời mời xác nhận lại.",
                OperationalContactErrorCodes.OrganizationRequired);

        instance.OperationalContactUserId = actorId;
        instance.OperationalContactConfirmedAt = now;
        instance.OperationalContactConfirmationSource = OperationalContactSources.Transfer;

        // The proposed person's details replace the campus snapshot. The campus lifecycle does NOT
        // move: a decided campus stays decided, with its host and schedule intact.
        var detail = instance.FormDetail;
        if (detail is not null)
        {
            detail.OperationalContactFullName = snapshot.ResolvedFullName ?? actor.FullName;
            detail.OperationalContactOrganization = snapshot.ResolvedOrganization;
            // Keep whatever the campus already had when a field OTHER than Organization is unreadable:
            // the columns are NOT NULL, and an accepted transfer must not fail on a field nobody
            // disputed. Organization itself can no longer be unreadable here — the guard above refused
            // the transfer before this point if it were.
            detail.OperationalContactJobTitle = snapshot.ResolvedJobTitle ?? detail.OperationalContactJobTitle;
            detail.OperationalContactPhone = snapshot.ResolvedPhone ?? detail.OperationalContactPhone;
            detail.OperationalContactEmail = change.NewEmailNormalized!;

            // The role has moved to somebody else, so the link that said WHICH delegation member held
            // it describes the previous person and nothing more. Left behind, it outlived them: the
            // biên bản reads this column to decide who wears "· Đầu mối", so the campus went on
            // naming the person who handed the role over — and the one who took it never appeared in
            // the record at all, because the auto-fill saw the old member already listed and stopped.
            //
            // Cleared rather than re-pointed: a transfer names an EMAIL ADDRESS, a delegation member
            // row has none, and there is no other evidence that could tie the two together. "Không
            // phải thành viên nào của đoàn" is the honest answer, and the biên bản knows what to do
            // with it — it adds the contact from this snapshot instead.
            detail.OperationalContactGuestMemberId = null;
        }
    }

    private async Task<string> CampusLabelAsync(VisitRequestCampus instance, CancellationToken ct)
        => await _db.Campuses.AsNoTracking()
               .Where(c => c.CampusId == instance.CampusId)
               .Select(c => c.Name)
               .FirstOrDefaultAsync(ct)
           ?? "cơ sở đã chọn";

    private void WriteAudit(
        VisitRequestIdentityChange change, VisitRequest visit, VisitRequestCampus instance,
        ulong actorId, ulong? previousContactId, string correlationId, DateTime now)
    {
        var isTransfer = change.ChangeKind == IdentityChangeKinds.Transfer;
        var action = isTransfer
            ? "OPERATIONAL_CONTACT_TRANSFER_APPLIED"
            : "OPERATIONAL_CONTACT_CONFIRMED";

        _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = change.IdentityChangeId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            EventType = action,
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Applied,
            ActorUserId = actorId,
            EmailMasked = change.NewEmailMasked,
            CorrelationId = correlationId,
            CreatedAt = now,
        });

        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = action,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            VisitRequestId = visit.VisitRequestId,
            CorrelationId = correlationId,
            SourceType = "IDENTITY",
            CreatedAt = now,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "operational_contact_user_id",
            OldValueText = previousContactId?.ToString(),
            NewValueText = actorId.ToString(),
            CreatedAt = now,
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "visit_request_campuses.status",
            OldValueText = isTransfer ? instance.Status : VisitInstanceStatuses.WaitingContactConfirmation,
            NewValueText = instance.Status,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(audit);
    }

    /// <summary>
    /// One audit row per campus the gate opening decided. Filed under the PROPOSER, not the contact
    /// who clicked accept: the contact chose nothing here, and an audit trail that says otherwise
    /// would attribute a staffing decision to somebody outside FPTU.
    /// </summary>
    private void WriteActivationAudits(
        VisitRequest visit, IReadOnlyList<ProposedHostActivation> activations,
        string correlationId, DateTime now)
    {
        foreach (var activation in activations)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = activation.ProposerUserId,
                Action = activation.Activated
                    ? "PROPOSED_HOST_ACTIVATED"
                    : "PROPOSED_HOST_NEEDS_RESELECTION",
                EntityType = "VisitRequestCampus",
                EntityId = activation.VisitInstanceId,
                CampusId = activation.CampusId,
                VisitRequestId = visit.VisitRequestId,
                VisitInstanceId = activation.VisitInstanceId,
                CorrelationId = correlationId,
                SourceType = CampusDecisionAudit.SourceType,
                Reason = activation.Activated
                    ? $"mode={activation.SelectionMode};host={activation.ProposedHostUserId}"
                    : $"mode={activation.SelectionMode};reason={activation.RejectionReason}",
                CreatedAt = now,
            });
        }
    }

}
