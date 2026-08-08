using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitNotifications;
using PEMS.Domain.Constants;

namespace PEMS.Infrastructure.Services;

/// <summary>See <see cref="IVisitNotificationRecoveryService"/>.</summary>
public sealed class VisitNotificationRecoveryService : IVisitNotificationRecoveryService
{
    /// <summary>
    /// How far back a sweep looks.
    ///
    /// <para>
    /// Bounded so the query stays cheap as the tables grow, and because a notification about something
    /// that happened a fortnight ago is no longer news — by then the registrant has seen the state in
    /// the dashboard or asked. A week is long enough to cover a mail outage over a holiday.
    /// </para>
    /// </summary>
    private const int LookbackDays = 7;

    /// <summary>
    /// How long a transition is left alone before the sweep considers it.
    ///
    /// <para>
    /// The command that caused it sends its own message immediately, and that send is not instant. Any
    /// shorter and the sweep would race the inline attempt and send a duplicate — the ledger only
    /// prevents a second message once the FIRST one has been recorded as sent.
    /// </para>
    /// </summary>
    private const int SettleMinutes = 5;

    private readonly IApplicationDbContext _db;
    private readonly CampusRejectionEmail _rejectionEmail;
    private readonly ContactInvitationExpiryEmail _expiryEmail;
    private readonly RecoverableVisitEmailSender _mailer;
    private readonly ILogger<VisitNotificationRecoveryService> _logger;

    public VisitNotificationRecoveryService(
        IApplicationDbContext db,
        CampusRejectionEmail rejectionEmail,
        ContactInvitationExpiryEmail expiryEmail,
        RecoverableVisitEmailSender mailer,
        ILogger<VisitNotificationRecoveryService> logger)
    {
        _db = db;
        _rejectionEmail = rejectionEmail;
        _expiryEmail = expiryEmail;
        _mailer = mailer;
        _logger = logger;
    }

    public async Task<VisitNotificationRecoveryResult> RunOnceAsync(
        DateTime vietnamNow, int batchSize, CancellationToken cancellationToken)
    {
        var since = vietnamNow.AddDays(-LookbackDays);
        var until = vietnamNow.AddMinutes(-SettleMinutes);

        var rejections = await RecoverRejectionsAsync(since, until, batchSize, cancellationToken);
        var expiries = await RecoverContactExpiriesAsync(since, until, batchSize, cancellationToken);

        if (rejections > 0 || expiries > 0)
            _logger.LogInformation(
                "visit notification recovery: re-sent {Rejections} campus rejection(s), {Expiries} contact-expiry notice(s)",
                rejections, expiries);

        return new VisitNotificationRecoveryResult(rejections, expiries);
    }

    /// <summary>
    /// Rejection EVENTS in the window that never produced a successful email.
    ///
    /// <para>
    /// The audit row is the anchor rather than the campus, because the campus holds only its LATEST
    /// rejection: scanning campuses could not see that a campus rejected twice owes two notifications,
    /// and the second one is precisely the one at risk of having been lost (plan §37).
    /// </para>
    /// <para>
    /// The row's own <c>created_at</c> dates the event exactly — it is written once, inside the
    /// rejecting transaction, and nothing ever updates it.
    /// </para>
    /// </summary>
    private async Task<int> RecoverRejectionsAsync(
        DateTime since, DateTime until, int batchSize, CancellationToken ct)
    {
        var candidates = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == CampusRejectionEmail.RejectionAuditAction
                        && a.VisitInstanceId != null
                        && a.CreatedAt >= since
                        && a.CreatedAt <= until)
            .OrderBy(a => a.AuditLogId)
            .Take(batchSize)
            .Select(a => a.AuditLogId)
            .ToListAsync(ct);

        return await SendMissingAsync(_rejectionEmail, candidates, ct);
    }

    /// <summary>
    /// Invitations that expired in the window and never produced a successful expiry notice.
    ///
    /// <para>
    /// EXPIRED is set only by the maintenance sweep, and <c>updated_at</c> is stamped with it, so it
    /// dates the transition closely enough for a window this wide. Redacted rows are still valid
    /// candidates: redaction clears the FULL address and keeps the masked one, which is the only address
    /// this message prints.
    /// </para>
    /// </summary>
    private async Task<int> RecoverContactExpiriesAsync(
        DateTime since, DateTime until, int batchSize, CancellationToken ct)
    {
        var candidates = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.Status == IdentityChangeStatuses.Expired
                        && c.UpdatedAt != null
                        && c.UpdatedAt >= since
                        && c.UpdatedAt <= until)
            .OrderBy(c => c.UpdatedAt)
            .Take(batchSize)
            .Select(c => c.IdentityChangeId)
            .ToListAsync(ct);

        return await SendMissingAsync(_expiryEmail, candidates, ct);
    }

    /// <summary>
    /// Offers each candidate to the sender, which is the only thing that decides whether a message is
    /// actually owed. Deliberately NOT pre-filtered by a join against the email history here: that would
    /// put the "already notified" rule in a second place, and the two would drift.
    /// </summary>
    private async Task<int> SendMissingAsync(
        IRecoverableVisitEmail email, IReadOnlyList<ulong> candidates, CancellationToken ct)
    {
        var sent = 0;
        foreach (var id in candidates)
        {
            var outcome = await _mailer.SendOnceAsync(email, id, ct);
            if (outcome == RecoverableEmailOutcome.Sent)
                sent++;
        }
        return sent;
    }
}
