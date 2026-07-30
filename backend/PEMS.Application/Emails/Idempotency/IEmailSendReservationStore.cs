using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Emails.Idempotency;

/// <summary>What a reservation attempt concluded.</summary>
public enum EmailSendReservationOutcome
{
    /// <summary>This request owns the attempt and must run the handler.</summary>
    Reserved,

    /// <summary>The same request already succeeded. Return its result; do nothing else.</summary>
    ReplaySucceeded,

    /// <summary>Another request with this key is running right now.</summary>
    InProgress,

    /// <summary>This key was already used for a DIFFERENT request. Refuse; send nothing.</summary>
    FingerprintMismatch,

    /// <summary>A previous attempt under this key reached the provider with no established result.</summary>
    OutcomeUnknown,
}

/// <param name="Outcome">What to do next.</param>
/// <param name="ReservationId">The row, when this request owns the attempt.</param>
/// <param name="ResultMessage">The stored success message, for a replay.</param>
public sealed record EmailSendReservation(
    EmailSendReservationOutcome Outcome,
    ulong ReservationId = 0,
    string? ResultMessage = null);

/// <summary>
/// Persistence for send reservations (G11 / R-103).
///
/// <para>
/// The interface is small on purpose: the state machine lives in the Application layer where it can be
/// read, and this port only promises that the transitions are durable and that two concurrent
/// <see cref="ReserveAsync"/> calls for one key cannot both return <see cref="EmailSendReservationOutcome.Reserved"/>.
/// That second promise is the whole point, and it is kept by the database's unique constraint plus a row
/// lock — not by anything an implementation could get subtly wrong in C#.
/// </para>
/// </summary>
public interface IEmailSendReservationStore
{
    /// <summary>
    /// Claims the attempt, or reports what the existing reservation says. Never throws for a duplicate:
    /// a unique-constraint violation is an expected race, and is resolved by re-reading the row.
    /// </summary>
    Task<EmailSendReservation> ReserveAsync(
        ulong actorUserId, string operationCode, string keyHash, string requestFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the outbound call is about to happen, and commits before it does. After this point
    /// the system can no longer claim nothing was sent.
    /// </summary>
    Task MarkDispatchingAsync(ulong reservationId, CancellationToken cancellationToken = default);

    /// <summary>Records provider acceptance and the message to replay to duplicates.</summary>
    Task MarkSucceededAsync(
        ulong reservationId, string resultMessage, ulong? sentEmailId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failure. <paramref name="dispatchStarted"/> decides between a clean failure the client
    /// may retry under the same key and an unknown outcome it may not.
    /// </summary>
    Task MarkFailedAsync(
        ulong reservationId, bool dispatchStarted, string? failureCode,
        CancellationToken cancellationToken = default);
}
