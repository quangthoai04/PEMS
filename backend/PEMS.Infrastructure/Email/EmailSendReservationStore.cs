using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Idempotency;
using PEMS.Domain.Entities.Emails;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// Durable reservations for report/invoice sends (G11 / R-103), on MySQL.
///
/// <para>
/// Two design points carry the guarantee.
/// </para>
/// <para>
/// <b>The database decides, not this class.</b> Claiming an attempt is <c>INSERT IGNORE</c> followed by
/// <c>SELECT … FOR UPDATE</c> inside a short transaction — the same technique the visit-request duplicate
/// guard already uses. Two concurrent requests carrying one key queue on the row lock, so exactly one can
/// see <c>RESERVED</c> and take it. A check-then-act in C# would have a window between the check and the
/// act, and that window is precisely where a double-click lands.
/// </para>
/// <para>
/// <b>Nothing here is tracked by EF.</b> Every write is raw, parameterised SQL. The context is shared
/// with the handler, and <c>SaveChangesAsync</c> has no partial mode — a tracked reservation entity would
/// be flushed by whichever unrelated save happened next, at whatever state it was in. Raw statements make
/// each transition its own committed fact.
/// </para>
/// </summary>
public sealed class EmailSendReservationStore : IEmailSendReservationStore
{
    private readonly IApplicationDbContext _db;

    public EmailSendReservationStore(IApplicationDbContext db) => _db = db;

    public async Task<EmailSendReservation> ReserveAsync(
        ulong actorUserId, string operationCode, string keyHash, string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var now = VietnamTime.Now();

        // The transaction covers the claim and nothing else. It is committed before the handler runs, so
        // no lock is held across PDF generation or across SMTP — the dispatcher already requires callers
        // not to do that, and a reservation is no excuse to start.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // INSERT IGNORE: losing the race is the normal case, not an error. The unique key is
        // (actor, operation, key hash), so a duplicate simply changes nothing and we read what is there.
        await _db.Database.ExecuteSqlRawAsync(
            @"INSERT IGNORE INTO email_send_idempotency
                  (actor_user_id, operation_code, idempotency_key_hash, request_fingerprint,
                   state, attempt_count, created_at, updated_at)
              VALUES ({0}, {1}, {2}, {3}, {4}, 0, {5}, {5})",
            new object[] { actorUserId, operationCode, keyHash, requestFingerprint, EmailSendStates.Reserved, now },
            cancellationToken);

        // FOR UPDATE: whoever holds this row decides alone. A second request blocks here until the first
        // has committed its transition, and then reads the truth rather than a stale snapshot.
        var row = (await _db.EmailSendIdempotencies
                .FromSqlRaw(
                    @"SELECT * FROM email_send_idempotency
                      WHERE actor_user_id = {0} AND operation_code = {1} AND idempotency_key_hash = {2}
                      FOR UPDATE",
                    actorUserId, operationCode, keyHash)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (row is null)
        {
            // Neither the insert nor the read produced a row. Rather than send an email on a reservation
            // that does not exist, fail — the caller sees a normal error and can retry with the same key.
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(
                "Could not establish a send reservation; refusing to dispatch without one.");
        }

        // A different request under the same key. Refuse before anything is generated or sent.
        if (!string.Equals(row.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new EmailSendReservation(EmailSendReservationOutcome.FingerprintMismatch);
        }

        var outcome = row.State switch
        {
            EmailSendStates.Succeeded => EmailSendReservationOutcome.ReplaySucceeded,
            EmailSendStates.OutcomeUnknown => EmailSendReservationOutcome.OutcomeUnknown,

            // A clean failure is the one state a same-key retry may resume from, because it is the one
            // the system can prove left nothing behind.
            EmailSendStates.FailedBeforeDispatch => EmailSendReservationOutcome.Reserved,

            // RESERVED, PREPARING or DISPATCHING. attempt_count is the discriminator: a row nobody has
            // claimed yet is 0, and taking it below makes it 1. Timestamps would NOT work here — the
            // column is DATETIME at second precision, so a claim in the same second as the insert leaves
            // created_at equal to updated_at. Anything already claimed reads as in-progress; nothing here
            // is allowed to decide that another request has "probably" died.
            _ => row.AttemptCount == 0
                ? EmailSendReservationOutcome.Reserved
                : EmailSendReservationOutcome.InProgress,
        };

        if (outcome != EmailSendReservationOutcome.Reserved)
        {
            await transaction.CommitAsync(cancellationToken);
            return new EmailSendReservation(outcome, row.EmailSendIdempotencyId, row.ResultMessage);
        }

        // Take it: PREPARING is written inside the same transaction as the read, so the row is no longer
        // claimable by the time the lock is released.
        await _db.Database.ExecuteSqlRawAsync(
            @"UPDATE email_send_idempotency
                 SET state = {0}, attempt_count = attempt_count + 1, updated_at = {1},
                     failure_code = NULL, dispatch_started_at = NULL, completed_at = NULL
               WHERE email_send_idempotency_id = {2}",
            new object[] { EmailSendStates.Preparing, now, row.EmailSendIdempotencyId },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new EmailSendReservation(EmailSendReservationOutcome.Reserved, row.EmailSendIdempotencyId);
    }

    public Task MarkDispatchingAsync(ulong reservationId, CancellationToken cancellationToken = default)
        => _db.Database.ExecuteSqlRawAsync(
            @"UPDATE email_send_idempotency
                 SET state = {0}, dispatch_started_at = {1}, updated_at = {1}
               WHERE email_send_idempotency_id = {2}",
            new object[] { EmailSendStates.Dispatching, VietnamTime.Now(), reservationId },
            cancellationToken);

    public Task MarkSucceededAsync(
        ulong reservationId, string resultMessage, ulong? sentEmailId,
        CancellationToken cancellationToken = default)
    {
        var now = VietnamTime.Now();

        // NULLIF rather than a null parameter: EF's raw-SQL parameters have no store type for DBNull and
        // throw outright ("no store type mapping for properties of type 'DBNull'"). Passing a sentinel and
        // letting SQL turn it back into NULL keeps the statement provider-agnostic. Zero is safe here —
        // sent_email_id is an AUTO_INCREMENT key and is never 0.
        return _db.Database.ExecuteSqlRawAsync(
            @"UPDATE email_send_idempotency
                 SET state = {0}, result_message = {1}, sent_email_id = NULLIF({2}, 0),
                     failure_code = NULL, completed_at = {3}, updated_at = {3}
               WHERE email_send_idempotency_id = {4}",
            new object[]
            {
                EmailSendStates.Succeeded,
                Truncate(resultMessage, 500),
                sentEmailId ?? 0UL,
                now,
                reservationId,
            },
            cancellationToken);
    }

    public Task MarkFailedAsync(
        ulong reservationId, bool dispatchStarted, string? failureCode,
        CancellationToken cancellationToken = default)
    {
        var now = VietnamTime.Now();
        var state = dispatchStarted ? EmailSendStates.OutcomeUnknown : EmailSendStates.FailedBeforeDispatch;

        // Same NULLIF reason as above. An empty failure code is not a meaningful value, so mapping "" to
        // NULL loses nothing — and a failure with no stable code is the common case (a NotFoundException
        // raised without one), which is exactly the path a DBNull parameter would have crashed.
        //
        // A clean failure clears dispatch_started_at so the row cannot later be mistaken for one that
        // reached the provider; the verify script asserts exactly this invariant.
        return _db.Database.ExecuteSqlRawAsync(
            @"UPDATE email_send_idempotency
                 SET state = {0}, failure_code = NULLIF({1}, ''), completed_at = {2}, updated_at = {2},
                     dispatch_started_at = CASE WHEN {3} = 1 THEN dispatch_started_at ELSE NULL END
               WHERE email_send_idempotency_id = {4}",
            new object[]
            {
                state,
                failureCode is null ? string.Empty : Truncate(failureCode, 64),
                now,
                dispatchStarted ? 1 : 0,
                reservationId,
            },
            cancellationToken);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
