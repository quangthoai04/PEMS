using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Emails.Idempotency;

/// <summary>
/// The reservation the current request is running under, if any (G11 / R-103).
///
/// <para>
/// It exists to answer one question at the moment of failure: <b>had the outbound call started?</b> That
/// answer decides between a failure the user may retry under the same key and one they may not, and it
/// is only knowable at the point in <c>ReportEmailSender</c> where SMTP is about to be called — six
/// handlers deep from where the exception is caught.
/// </para>
/// <para>
/// A scoped object rather than a parameter threaded through six handlers, an interface and a dispatcher:
/// the alternative would have put idempotency plumbing into signatures that have nothing to do with it,
/// and any caller that forgot to pass it would have failed silently and wrongly — reporting a clean
/// failure for a message that had already gone out.
/// </para>
/// </summary>
public sealed class EmailSendAttempt
{
    private readonly IEmailSendReservationStore _store;

    public EmailSendAttempt(IEmailSendReservationStore store) => _store = store;

    /// <summary>The reservation, or null when this request is not an idempotent send.</summary>
    public ulong? ReservationId { get; private set; }

    /// <summary>True once the outbound call has been recorded as started.</summary>
    public bool DispatchStarted { get; private set; }

    /// <summary>The history row the send produced, when it got that far.</summary>
    public ulong? SentEmailId { get; private set; }

    /// <summary>Called by the idempotency behaviour once it owns the attempt.</summary>
    public void Begin(ulong reservationId)
    {
        ReservationId = reservationId;
        DispatchStarted = false;
        SentEmailId = null;
    }

    /// <summary>
    /// Records the dispatch transition durably, BEFORE the provider is called. Does nothing when there
    /// is no reservation — the same sender serves paths that are not idempotent sends.
    /// </summary>
    public async Task MarkDispatchingAsync(CancellationToken cancellationToken = default)
    {
        if (ReservationId is not { } id) return;

        await _store.MarkDispatchingAsync(id, cancellationToken);

        // Set only after the write succeeded. If persisting the transition fails, the attempt is still
        // provably pre-dispatch, and treating it as unknown would strand a key for no reason.
        DispatchStarted = true;
    }

    /// <summary>
    /// Withdraws the dispatch claim, for the cases where the provider was demonstrably never contacted —
    /// SMTP switched off, or no host and no pickup directory configured. Those refusals happen before a
    /// socket is opened, so calling them "unknown outcome" would strand the user's key over a
    /// configuration problem that sent nothing.
    ///
    /// <para>
    /// Only ever called with a result that <see cref="Common.Interfaces.EmailDeliveryCodes.ProvesNothingWasSent"/>
    /// accepts. It narrows what the system claims; it never widens it.
    /// </para>
    /// </summary>
    public void WithdrawDispatchClaim() => DispatchStarted = false;

    /// <summary>Links the reservation to the history row the send produced.</summary>
    public void RecordSentEmail(ulong sentEmailId)
    {
        if (ReservationId is not null) SentEmailId = sentEmailId;
    }
}
