using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.VisitNotifications;

/// <summary>
/// One system email that is allowed to be RETRIED later, described independently of when it is sent.
///
/// <para>
/// It exists because a post-commit notification has two callers with the same content and different
/// timing: the command that caused the transition, sending immediately, and the recovery sweep, sending
/// later because the first attempt failed. Writing the message twice is how the retry ends up saying
/// something slightly different from the original — a different window format, a stale campus name, or
/// a variable the template no longer declares.
/// </para>
/// </summary>
public interface IRecoverableVisitEmail
{
    /// <summary>The registered template this message renders from.</summary>
    string TemplateCode { get; }

    /// <summary>
    /// The <c>sent_emails.related_type</c> the message is recorded against. Together with the related id
    /// it is the key on which "has this already been sent?" is answered, so both must be written by
    /// <see cref="BuildAsync"/> and by nothing else.
    /// </summary>
    string RelatedType { get; }

    /// <summary>
    /// Builds the message for one related object, reading whatever context it needs, or null when there
    /// is nothing to send — a deleted campus, a request with no registrant address. Null is a decision,
    /// not a failure: the sender treats it as "nothing owed" and stops asking.
    /// </summary>
    Task<SystemEmailRequest?> BuildAsync(ulong relatedId, CancellationToken cancellationToken);
}

/// <summary>The outcome of one <see cref="RecoverableVisitEmailSender.SendOnceAsync"/> call.</summary>
public enum RecoverableEmailOutcome
{
    /// <summary>Handed to the provider and accepted.</summary>
    Sent,

    /// <summary>A successful message for this object already exists — nothing was sent.</summary>
    AlreadySent,

    /// <summary>There is nothing to send (no recipient, no context).</summary>
    NothingToSend,

    /// <summary>Tried and failed. A <c>sent_emails</c> row records it; the sweep may try again.</summary>
    Failed,

    /// <summary>Too many failed attempts. Left alone for an operator rather than retried forever.</summary>
    Exhausted,

    /// <summary>
    /// A previous attempt may already have reached the provider. Automatic resend is refused for good
    /// (plan §42) — only a person who can establish what actually happened may send it now.
    /// </summary>
    OutcomeUnknown,

    /// <summary>
    /// Not attempted right now — the backoff after the last failure has not elapsed, or another worker
    /// holds the claim for this event. Nothing is lost; the next sweep reconsiders it.
    /// </summary>
    Deferred,
}

/// <summary>
/// A cross-process claim on ONE business event, so that two application instances sweeping at the same
/// moment cannot both send its notification (plan §46).
///
/// <para>
/// An in-memory lock would be worthless here: the whole point is that PEMS may run as more than one
/// process, and the recovery worker is hosted in every one of them.
/// </para>
/// </summary>
public interface IEmailRecoveryLock
{
    /// <summary>
    /// Tries to claim <paramref name="key"/> without waiting. Returns null when somebody else holds it.
    /// Disposing the returned handle releases the claim.
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// Sends a <see cref="IRecoverableVisitEmail"/> at most once successfully, however many times it is
/// asked.
///
/// <para>
/// The durability comes from rows that already exist. <see cref="ISystemEmailDispatcher"/> writes a
/// <c>sent_emails</c> row BEFORE handing anything to the provider and then writes back the truthful
/// outcome, so after a failed send there is a FAILED row carrying the template, the related object and
/// the error. That row is the retry ledger: "already sent" is a SENT row for this (template, related
/// object), and the attempt count is simply how many rows exist. No new table, no new column, and no
/// flag that could disagree with the email history a person actually reads.
/// </para>
/// <para>
/// This is what makes the two repair cases work. A rejection cannot be retried by rejecting again — the
/// campus is already REJECTED and the command refuses — and an expiry sweep cannot re-notify, because
/// it only ever selects PENDING rows and the invitation is EXPIRED. Both are answered by asking a
/// different question: not "did the business transition happen again?" but "did this transition ever
/// produce a successful message?".
/// </para>
/// <para>
/// Never throws. A notification problem must not become the caller's problem after the caller's
/// business change is committed (plan §11).
/// </para>
/// </summary>
public sealed class RecoverableVisitEmailSender
{
    /// <summary>
    /// How many times one notification may be attempted before it is left for an operator.
    ///
    /// <para>
    /// A cap rather than endless retries because the failures that survive several attempts are not
    /// transient: a malformed recipient address, a template an administrator broke. Retrying those on
    /// every sweep buys nothing and buries the real failures in the log.
    /// </para>
    /// </summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// The wait after the first failure. Each further attempt doubles it, so five attempts span roughly
    /// fifteen minutes to four hours — inside the sweep's seven-day window, and long enough that a mail
    /// server being restarted is not hammered every tick (plan §45).
    /// </summary>
    private static readonly TimeSpan FirstBackoff = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _db;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IEmailRecoveryLock _claims;
    private readonly IDateTimeService _clock;
    private readonly ILogger<RecoverableVisitEmailSender> _logger;

    public RecoverableVisitEmailSender(
        IApplicationDbContext db, ISystemEmailDispatcher dispatcher,
        IEmailRecoveryLock claims, IDateTimeService clock,
        ILogger<RecoverableVisitEmailSender> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _claims = claims;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Sends the message for <paramref name="relatedId"/> unless one has already succeeded.
    ///
    /// <para>
    /// <b>Call it AFTER your business transaction has committed.</b> The dispatcher shares this
    /// DbContext and saves — see <see cref="ISystemEmailDispatcher"/> for why that matters.
    /// </para>
    /// </summary>
    public async Task<RecoverableEmailOutcome> SendOnceAsync(
        IRecoverableVisitEmail email, ulong relatedId, CancellationToken cancellationToken)
    {
        try
        {
            var verdict = await VerdictAsync(email, relatedId, cancellationToken);
            if (verdict is not null) return verdict.Value;

            // ── Claim the event, so a second worker sweeping the same second does not send a second
            //    copy. Held only across this one send and released by the using. ──
            await using var claim = await _claims.TryAcquireAsync(
                $"pems:visit-notify:{email.RelatedType}:{relatedId}", cancellationToken);
            if (claim is null)
            {
                _logger.LogDebug(
                    "notification {TemplateCode} for {RelatedType} {RelatedId} is being handled elsewhere",
                    email.TemplateCode, email.RelatedType, relatedId);
                return RecoverableEmailOutcome.Deferred;
            }

            // Asked again under the claim: the worker that just released it may have been sending this
            // very message. Without this the claim would only narrow the race rather than close it.
            var settled = await VerdictAsync(email, relatedId, cancellationToken);
            if (settled is not null) return settled.Value;

            var request = await email.BuildAsync(relatedId, cancellationToken);
            if (request is null)
                return RecoverableEmailOutcome.NothingToSend;

            var result = await _dispatcher.SendAsync(request, cancellationToken);
            if (result.Delivery.Status == EmailDeliveryStatus.Sent)
                return RecoverableEmailOutcome.Sent;

            // Skipped (SMTP off) and Failed are both "not delivered". Neither throws, and the row the
            // dispatcher wrote already says which it was — including, now, whether it says so provably.
            _logger.LogWarning(
                "notification {TemplateCode} for {RelatedType} {RelatedId} was not delivered ({Status}, {Code})",
                email.TemplateCode, email.RelatedType, relatedId,
                result.Delivery.Status, result.Delivery.Code);
            return RecoverableEmailOutcome.Failed;
        }
        catch (Exception ex)
        {
            // A broken template throws out of the dispatcher BEFORE any row is written, so this path
            // leaves no attempt behind — which is correct: the next sweep should try again once an
            // operator has fixed the template, and the business state was never in doubt.
            _logger.LogError(ex,
                "notification {TemplateCode} for {RelatedType} {RelatedId} could not be sent",
                email.TemplateCode, email.RelatedType, relatedId);
            return RecoverableEmailOutcome.Failed;
        }
    }

    /// <summary>
    /// Decides from the email history alone whether this message must NOT be sent right now, or returns
    /// null to mean "go ahead".
    ///
    /// <para>
    /// The order is the safety argument. An accepted message ends it. An attempt whose outcome cannot be
    /// established ends it too and for good, because the recipient may be holding that copy — that is
    /// checked BEFORE the attempt cap so an ambiguous failure is never mistaken for a retryable one that
    /// simply has budget left.
    /// </para>
    /// </summary>
    private async Task<RecoverableEmailOutcome?> VerdictAsync(
        IRecoverableVisitEmail email, ulong relatedId, CancellationToken ct)
    {
        // Joined to the template rather than matched on a captured template id: the id can change when
        // an administrator replaces a template row, and "did this event get its email?" is a question
        // about the CODE.
        var rows = await (
            from e in _db.SentEmails.AsNoTracking()
            join t in _db.EmailTemplates.AsNoTracking() on e.EmailTemplateId equals t.EmailTemplateId
            where t.TemplateCode == email.TemplateCode
                  && e.RelatedType == email.RelatedType
                  && e.RelatedId == relatedId
            select new { e.Status, e.ErrorMessage, e.LastAttemptAt }).ToListAsync(ct);

        if (rows.Count == 0) return null;

        var outcomes = rows
            .Select(r => EmailAttemptRecord.Classify(r.Status, r.ErrorMessage))
            .ToList();

        if (outcomes.Contains(EmailAttemptOutcome.Accepted))
            return RecoverableEmailOutcome.AlreadySent;

        if (outcomes.Contains(EmailAttemptOutcome.Unknown))
        {
            // Deliberately loud and deliberately terminal: this is the one case a person has to look at,
            // and it stays visible in the email history for them to find (plan §43, §44).
            _logger.LogError(
                "notification {TemplateCode} for {RelatedType} {RelatedId} has an attempt whose delivery "
                + "cannot be established; it will NOT be retried automatically",
                email.TemplateCode, email.RelatedType, relatedId);
            return RecoverableEmailOutcome.OutcomeUnknown;
        }

        if (rows.Count >= MaxAttempts)
        {
            _logger.LogError(
                "notification {TemplateCode} for {RelatedType} {RelatedId} has failed {Attempts} times "
                + "and will not be retried automatically",
                email.TemplateCode, email.RelatedType, relatedId, rows.Count);
            return RecoverableEmailOutcome.Exhausted;
        }

        // Backoff from the most recent attempt, doubling per attempt already made.
        var last = rows.Max(r => r.LastAttemptAt);
        if (last is not null)
        {
            var wait = TimeSpan.FromTicks(FirstBackoff.Ticks * (1L << (rows.Count - 1)));
            if (_clock.VietnamNow < last.Value.Add(wait))
                return RecoverableEmailOutcome.Deferred;
        }

        return null;
    }
}
