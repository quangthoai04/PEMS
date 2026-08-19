using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// Shared post-commit notification dispatch for both v2 create paths (authenticated + public OTP).
/// Routes an "incoming visit request" in-app notification to each involved campus Staff Leader (the
/// per-instance coordinator) plus, for a multi-campus request, a visibility notification to HO — mirroring
/// the v1 create. Best-effort: dispatched AFTER the request is committed, so a rollback never notifies and a
/// dispatch failure is logged but never rolls the committed request back (there is no outbox → not exactly-once).
/// The INITIAL_CLAIM invitation email to a different primary contact (contact ≠ registrant) is issued by the
/// identity-confirmation workflow (Phase D), not here.
/// </summary>
internal static class V2CreateNotifier
{
    public static async Task NotifyStaffLeadersAfterCommitAsync(
        IApplicationDbContext db,
        INotificationService notificationService,
        ILogger logger,
        VisitRequest created,
        CancellationToken cancellationToken)
    {
        try
        {
            // ONLY campuses still awaiting a decision get the actionable "please review" notification.
            // A campus the authenticated creator already processed directly (self-host / leader-assign) is
            // not pending for anyone, so raising an action there would be a fake task.
            var pendingInstances = created.CampusInstances
                .Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval && c.CoordinatorUserId.HasValue)
                .ToList();

            // Per-campus names: each Staff Leader is told about THEIR OWN campus's delegation, so a mixed
            // request never shows one campus's content to another campus's reviewer.
            var pendingInstanceIds = pendingInstances.Select(c => c.VisitInstanceId).ToList();
            var nameByInstance = pendingInstanceIds.Count == 0
                ? new Dictionary<ulong, string>()
                : await db.VisitInstanceFormDetails.AsNoTracking()
                    .Where(d => pendingInstanceIds.Contains(d.VisitInstanceId))
                    .ToDictionaryAsync(d => d.VisitInstanceId, d => d.DelegationName, cancellationToken);

            // Campus display name for the "waiting approval" notification message — the reviewer needs
            // to see WHICH campus without opening the request first.
            var pendingCampusIds = pendingInstances.Select(c => c.CampusId).Distinct().ToList();
            var campusNameById = pendingCampusIds.Count == 0
                ? new Dictionary<ulong, string>()
                : await db.Campuses.AsNoTracking()
                    .Where(c => pendingCampusIds.Contains(c.CampusId))
                    .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

            var notifications = pendingInstances.Select(c => new CreateNotificationRequest(
                RecipientUserId: c.CoordinatorUserId!.Value,
                Title: "Có yêu cầu tiếp khách mới",
                Message: $"{(nameByInstance.TryGetValue(c.VisitInstanceId, out var nm) ? nm : created.RequestCode)} đang chờ xử lý tại cơ sở của bạn. Vui lòng xem chi tiết, duyệt/từ chối và chọn host nếu duyệt.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                RelatedId: created.VisitRequestId,
                Category: NotificationCategories.Visit,
                IsActionRequired: true,
                VisitRequestId: created.VisitRequestId,
                // Exact campus context — the frontend deep-link resolver needs this to route the
                // coordinator straight to THEIR campus's review, not merely the request (plan
                // §13.1: multi-campus without an instance id cannot say which campus needs this
                // reviewer).
                VisitInstanceId: c.VisitInstanceId,
                CampusId: c.CampusId,
                // OpenCampusReview (not the generic OpenVisitDetail): the notification IS the review
                // task itself — the frontend's navigation-intent resolver may open the live
                // approve/reject control for it, unlike a same-request "just updated" notification
                // (plan §14/§34).
                ActionType: NotificationActionTypes.OpenCampusReview,
                ActionUrl: $"/dashboard/visit?visitRequestId={created.VisitRequestId}",
                MetadataJson: NotificationEventKeys.BuildMetadata(
                    NotificationEventKeys.VisitRequestWaitingApproval,
                    new
                    {
                        delegationName = nameByInstance.TryGetValue(c.VisitInstanceId, out var dn) ? dn : created.RequestCode,
                        requestCode = created.RequestCode,
                        campusName = campusNameById.TryGetValue(c.CampusId, out var cn) ? cn : created.RequestCode,
                    }))).ToList();

            if (created.VisitScope == VisitScopes.MultiCampus)
            {
                var hoUsers = await db.Users
                    .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == UserStatuses.Active)
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);
                notifications.AddRange(hoUsers.Select(id => new CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Có đơn liên cơ sở mới",
                    // Request-level message: a mixed request has no single business name.
                    Message: $"{(created.HasMixedCampusDetails ? "Khác nhau theo cơ sở" : nameByInstance.Values.FirstOrDefault() ?? created.RequestCode)} vừa gửi đơn liên cơ sở, đang chờ các cơ sở xử lý.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: created.VisitRequestId,
                    Category: NotificationCategories.Visit,
                    IsActionRequired: false,
                    VisitRequestId: created.VisitRequestId,
                    ActionType: NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={created.VisitRequestId}",
                    MetadataJson: NotificationEventKeys.BuildMetadata(
                        NotificationEventKeys.MultiCampusRequestSubmittedHoVisibility,
                        new { requestCode = created.RequestCode }))));
            }

            if (notifications.Count > 0)
                await notificationService.CreateManyAsync(notifications, cancellationToken);
        }
        catch (System.Exception ex)
        {
            // The request is already committed; a notification failure must not fail the create.
            logger.LogError(ex,
                "create-v2 post-commit notification dispatch failed for visit request {VisitRequestId}",
                created.VisitRequestId);
        }
    }

    /// <summary>One campus's INITIAL_CLAIM invitation and the links that answer it.</summary>
    public readonly record struct MintedInvitation(
        ulong IdentityChangeId, OperationalContactInvitationTokens Tokens);

    /// <summary>
    /// Mints the links for every INITIAL_CLAIM the create transaction just wrote (plan §16.4) — a campus
    /// whose contact is NOT the registrant. A campus that self-matched was linked inside the create and
    /// has no invitation, so nothing is minted for it, by design.
    ///
    /// <para>
    /// Called INSIDE the create transaction, and deliberately NOT wrapped in a try/catch. An invitation
    /// exists to be answered, and it can only be answered through a link, so the two are one fact: if
    /// the links cannot be written, the create that depends on them must not commit either. Minting
    /// afterwards produced the opposite — a committed request whose campuses sat at the contact gate
    /// with PENDING claims and no links, which no accept could open and no re-invite could replace,
    /// while the registrant was shown a request that had been created successfully.
    /// </para>
    /// <para>
    /// The identity changes already have their ids (the create service flushes them to resolve its own
    /// event FKs), and this only ADDS token rows — the caller's commit is what makes both durable.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyList<MintedInvitation>> MintOperationalContactInvitationsAsync(
        IApplicationDbContext db,
        IOperationalContactInvitationService invitations,
        VisitRequest created,
        CancellationToken cancellationToken)
    {
        var pending = await db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.VisitRequestId == created.VisitRequestId
                        && c.ChangeKind == IdentityChangeKinds.InitialConfirmation
                        && c.Status == IdentityChangeStatuses.Pending)
            .OrderBy(c => c.VisitInstanceId)
            .Select(c => c.IdentityChangeId)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return System.Array.Empty<MintedInvitation>();

        var minted = new List<MintedInvitation>(pending.Count);
        foreach (var id in pending)
            minted.Add(new MintedInvitation(
                id,
                OperationalContactGuards.RequireMintedLinks(
                    await invitations.MintInvitationTokensAsync(id, cancellationToken), id)));
        return minted;
    }

    /// <summary>
    /// Sends the invitations whose links are already committed. Best-effort PER CAMPUS: one address
    /// failing must not deny the others theirs, and any that fails leaves a PENDING invitation with a
    /// LIVE link — the registrant resends it, and the link in the failed email would have worked too.
    /// Only the first successful create reaches here (idempotent replays return earlier), so a retry
    /// never re-invites.
    /// </summary>
    public static async Task DispatchOperationalContactInvitationsAfterCommitAsync(
        IOperationalContactInvitationService invitations,
        ILogger logger,
        IReadOnlyList<MintedInvitation> minted,
        VisitRequest created,
        CancellationToken cancellationToken)
    {
        foreach (var (id, tokens) in minted)
        {
            try
            {
                await invitations.DispatchInvitationEmailAsync(id, tokens, cancellationToken);
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex,
                    "create-v2 post-commit operational-contact invitation failed for identity change {IdentityChangeId} of visit request {VisitRequestId}",
                    id, created.VisitRequestId);
            }
        }
    }
}
