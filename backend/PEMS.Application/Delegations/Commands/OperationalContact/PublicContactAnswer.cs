using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// The two things the public (no-login) answer path has to do for itself, before the canonical
/// accept/decline command takes over.
/// </summary>
internal static class PublicContactAnswer
{
    /// <summary>
    /// Resolves a raw link to the invitation it belongs to and proves the link is one the caller may
    /// use for <paramref name="requiredIntendedAction"/>.
    ///
    /// <para>
    /// Read-only: it consumes nothing. The canonical command re-locks the row and re-checks everything
    /// inside its own transaction, so this is a fast refusal for an obviously dead link, not the
    /// authorization itself. Every failure answers the same way — an unknown token, a foreign token, a
    /// typo and an expired one are indistinguishable, so the endpoint cannot be used to discover
    /// whether an address was ever invited.
    /// </para>
    /// </summary>
    public static async Task<VisitRequestIdentityChange> LoadInvitationAsync(
        IApplicationDbContext db, IEmailActionTokenService tokens,
        string? rawToken, string requiredIntendedAction, CancellationToken ct)
    {
        var changeId = await OperationalContactGuards.ResolveChangeIdAsync(db, tokens, rawToken, ct)
            ?? throw new ConflictException(
                "Liên kết không hợp lệ.", OperationalContactErrorCodes.ConfirmationNotFound);

        var intended = await OperationalContactGuards.IntendedActionOfAsync(db, tokens, rawToken, ct);
        if (!string.Equals(intended, requiredIntendedAction, StringComparison.Ordinal))
            throw new ConflictException(
                "Liên kết này không dùng cho thao tác vừa chọn.",
                OperationalContactErrorCodes.ConfirmationNotFound);

        return await db.VisitRequestIdentityChanges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentityChangeId == changeId, ct)
            ?? throw new ConflictException(
                "Liên kết không hợp lệ.", OperationalContactErrorCodes.ConfirmationNotFound);
    }

    /// <summary>
    /// Settles a DECLINE for an address that has no account at all.
    ///
    /// <para>
    /// The canonical decline command records who answered, and here there is nobody to record —
    /// provisioning an account just to store an actor id would leave a Visitor account behind for
    /// somebody who has explicitly refused to take part. So the invitation is settled with a null
    /// actor and the identity event says the answer came from the email link.
    /// </para>
    /// <para>
    /// Everything else is identical to the authenticated decline: the row is LOCKED, the pending
    /// checks run inside the transaction, the token is consumed with its intended action, every
    /// sibling link of the same invitation dies, and the campus keeps whatever contact it had — a
    /// declined INITIAL_CONFIRMATION leaves the campus behind the gate, a declined TRANSFER leaves the
    /// present holder exactly where they were.
    /// </para>
    /// </summary>
    public static async Task<OperationalContactActionResponse> DeclineWithoutAccountAsync(
        IApplicationDbContext db, IEmailActionTokenService tokens,
        VisitRequestIdentityChange untracked, string rawToken, string? reason, CancellationToken ct)
    {
        const int retentionDays = 90;
        var now = DateTime.Now;
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var correlationId = Guid.NewGuid().ToString("N");

        await using var tx = await db.BeginTransactionAsync(ct);

        var rows = await db.VisitRequestIdentityChanges
            .FromSqlRaw(
                "SELECT * FROM visit_request_identity_changes WHERE identity_change_id = {0} FOR UPDATE",
                untracked.IdentityChangeId)
            .ToListAsync(ct);
        var change = rows.SingleOrDefault()
            ?? throw new ConflictException(
                "Liên kết không hợp lệ.", OperationalContactErrorCodes.ConfirmationNotFound);

        var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
            db, change.VisitRequestId, change.VisitInstanceId, ct);

        // A replay of the same public decline is the same answer, not a failure.
        if (change.Status == IdentityChangeStatuses.Declined)
        {
            await tx.CommitAsync(ct);
            return Respond(visit, instance, change, idempotent: true);
        }

        OperationalContactGuards.EnsurePending(change, now);

        var token = await OperationalContactGuards.LoadLiveTokenAsync(
            db, tokens, rawToken, change.IdentityChangeId, now, ct,
            requiredIntendedAction: EmailIntendedActions.Decline);

        change.Status = IdentityChangeStatuses.Declined;
        change.DeclinedAt = now;
        change.Reason = trimmedReason;
        change.RetentionUntil = now.AddDays(retentionDays);
        change.UpdatedAt = now;

        token.UsedAt = now;
        token.UsedAction = EmailIntendedActions.Decline;
        token.ResultStatus = EmailActionResultStatuses.Success;

        await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            db, EmailActionTargetTypes.VisitRequestIdentityChange, change.IdentityChangeId,
            "Lời mời đã bị từ chối.", now, ct);

        var eventType = change.ChangeKind == IdentityChangeKinds.Transfer
            ? "OPERATIONAL_CONTACT_TRANSFER_DECLINED"
            : "OPERATIONAL_CONTACT_CONFIRMATION_DECLINED";

        db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = change.IdentityChangeId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            EventType = eventType,
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Declined,
            // No account answered — the link did. Naming a user here would attribute the refusal to
            // somebody who does not exist in the system.
            ActorUserId = null,
            EmailMasked = change.NewEmailMasked,
            Reason = trimmedReason,
            CorrelationId = correlationId,
            CreatedAt = now,
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = null,
            Action = eventType,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            VisitRequestId = visit.VisitRequestId,
            CorrelationId = correlationId,
            SourceType = "IDENTITY",
            Reason = trimmedReason,
            CreatedAt = now,
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Respond(visit, instance, change, idempotent: false);
    }

    private static OperationalContactActionResponse Respond(
        VisitRequest visit, VisitRequestCampus instance, VisitRequestIdentityChange change, bool idempotent)
        => new(
            visit.VisitRequestId, instance.VisitInstanceId, visit.RequestCode ?? string.Empty,
            change.ChangeKind, IdentityChangeStatuses.Declined, instance.Status, visit.Status,
            idempotent,
            change.ChangeKind == IdentityChangeKinds.Transfer
                ? "Bạn đã từ chối nhận chuyển giao vai trò đầu mối vận hành. Đầu mối hiện tại của cơ sở giữ nguyên quyền."
                : "Bạn đã từ chối lời mời làm đầu mối vận hành cho cơ sở này.");
}
