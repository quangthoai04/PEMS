using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Idempotency;
using PEMS.Application.Reports.Commands.SendDeptLeaderInvoiceToStaffLeader;
using PEMS.Application.Reports.Commands.SendDeptLeaderPersonnelReport;
using PEMS.Application.Reports.Commands.SendHoCampusReport;
using PEMS.Application.Reports.Commands.SendStaffLeaderDepartmentReport;
using PEMS.Application.Reports.Commands.SendStaffLeaderDeptInvoice;
using PEMS.Application.Reports.Commands.SendStaffLeaderPersonnelReport;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// G11 / R-103 — the parts of the send-idempotency contract that are decided in memory: what counts as a
/// usable key, and what counts as the same request.
///
/// <para>
/// These are the rules a database test would exercise only incidentally. If the fingerprint says two
/// genuinely different invoices are "the same request", the second one is silently never sent; if it says
/// two identical clicks are different, the guarantee is worth nothing. Both failures are invisible at the
/// storage layer, which sees only two hashes.
/// </para>
/// </summary>
public class EmailSendIdempotencyContractTests
{
    // ── The key ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_missing_key_is_refused_with_a_stable_code()
    {
        var error = Assert.Throws<ValidationException>(() => IdempotencyKey.RequireHash(null));
        Assert.Equal(EmailErrorCodes.IdempotencyKeyRequired, error.ErrorCode);

        var empty = Assert.Throws<ValidationException>(() => IdempotencyKey.RequireHash(string.Empty));
        Assert.Equal(EmailErrorCodes.IdempotencyKeyRequired, empty.ErrorCode);
    }

    [Theory]
    [InlineData("short")]                                   // below the minimum
    [InlineData("has space in it")]                         // space is excluded on purpose
    [InlineData("carriage\rreturn")]                        // header injection
    [InlineData("line\nfeed")]                              // header injection
    [InlineData("null\0byte")]
    [InlineData("tab\there")]
    public void A_malformed_key_is_refused_with_a_stable_code(string key)
    {
        var error = Assert.Throws<ValidationException>(() => IdempotencyKey.RequireHash(key));
        Assert.Equal(EmailErrorCodes.IdempotencyKeyInvalid, error.ErrorCode);
    }

    [Fact]
    public void An_over_long_key_is_refused()
    {
        var error = Assert.Throws<ValidationException>(
            () => IdempotencyKey.RequireHash(new string('a', IdempotencyKey.MaxLength + 1)));
        Assert.Equal(EmailErrorCodes.IdempotencyKeyInvalid, error.ErrorCode);
    }

    [Theory]
    [InlineData("6f1b2c3d-4e5f-4a6b-8c9d-0e1f2a3b4c5d")]    // the UUID the frontend mints
    [InlineData("01J8ZQ7YKX5W6M8N9P0Q1R2S3T")]              // a ULID
    [InlineData("abc-_.~+/=123456")]                        // base64url and friends
    public void A_well_formed_key_is_accepted(string key)
        => Assert.Equal(64, IdempotencyKey.RequireHash(key).Length);

    [Fact]
    public void The_key_is_opaque_and_case_sensitive()
    {
        // Two keys differing only in case are two DIFFERENT attempts. Normalising them would silently
        // merge a retry with an unrelated send that happened to be spelled the same way.
        Assert.NotEqual(IdempotencyKey.Hash("AbCdEfGh"), IdempotencyKey.Hash("abcdefgh"));
    }

    [Fact]
    public void Only_the_hash_ever_leaves_this_class()
    {
        const string key = "6f1b2c3d-4e5f-4a6b-8c9d-0e1f2a3b4c5d";
        var hash = IdempotencyKey.RequireHash(key);

        Assert.DoesNotContain(key, hash, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    // ── The fingerprint ─────────────────────────────────────────────────────────────────────────

    private const ulong Actor = 4242;

    private static SendStaffLeaderDeptInvoiceCommand Invoice(
        params (ulong Id, decimal Price)[] items)
        => new()
        {
            DepartmentId = 7,
            FromDate = new DateTime(2026, 7, 1),
            ToDate = new DateTime(2026, 7, 31),
            Note = "Hóa đơn tháng 7",
            Items = items.Select(i => new SendStaffLeaderDeptInvoiceItem
            {
                LogisticsItemId = i.Id,
                UnitPrice = i.Price,
            }).ToList(),
        };

    [Fact]
    public void The_same_request_has_the_same_fingerprint()
        => Assert.Equal(
            EmailSendFingerprint.Compute(Invoice((1, 100m), (2, 250m)), Actor),
            EmailSendFingerprint.Compute(Invoice((1, 100m), (2, 250m)), Actor));

    [Fact]
    public void Line_order_does_not_change_the_request()
    {
        // The user picked the same two lines; which order the grid happened to send them in is not a
        // business difference, and treating it as one would let a retry become a second invoice.
        Assert.Equal(
            EmailSendFingerprint.Compute(Invoice((1, 100m), (2, 250m)), Actor),
            EmailSendFingerprint.Compute(Invoice((2, 250m), (1, 100m)), Actor));
    }

    [Fact]
    public void Decimal_formatting_does_not_change_the_request()
        => Assert.Equal(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(Invoice((1, 100.00m)), Actor));

    [Fact]
    public void A_different_price_is_a_different_request()
        => Assert.NotEqual(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(Invoice((1, 101m)), Actor));

    [Fact]
    public void An_extra_line_is_a_different_request()
        => Assert.NotEqual(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(Invoice((1, 100m), (2, 250m)), Actor));

    [Fact]
    public void A_different_note_is_a_different_request()
    {
        var edited = Invoice((1, 100m));
        edited.Note = "Hóa đơn tháng 7 — đã sửa";

        Assert.NotEqual(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(edited, Actor));
    }

    [Fact]
    public void Whitespace_around_a_note_is_not_a_different_request()
    {
        var padded = Invoice((1, 100m));
        padded.Note = "  Hóa đơn tháng 7  ";

        Assert.Equal(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(padded, Actor));
    }

    [Fact]
    public void A_different_actor_is_a_different_request()
        => Assert.NotEqual(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor + 1));

    [Fact]
    public void A_different_department_is_a_different_request()
    {
        var other = Invoice((1, 100m));
        other.DepartmentId = 8;

        Assert.NotEqual(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(other, Actor));
    }

    [Fact]
    public void The_time_of_day_is_not_part_of_the_request()
    {
        // Report periods are chosen by day. Two clicks on the same "1/7 – 31/7" report a second apart
        // must not become two different requests just because a picker carried a timestamp.
        var withTime = Invoice((1, 100m));
        withTime.FromDate = new DateTime(2026, 7, 1, 13, 45, 12);
        withTime.ToDate = new DateTime(2026, 7, 31, 9, 3, 0);

        Assert.Equal(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(withTime, Actor));
    }

    [Fact]
    public void A_different_period_is_a_different_request()
    {
        var august = Invoice((1, 100m));
        august.FromDate = new DateTime(2026, 8, 1);
        august.ToDate = new DateTime(2026, 8, 31);

        Assert.NotEqual(
            EmailSendFingerprint.Compute(Invoice((1, 100m)), Actor),
            EmailSendFingerprint.Compute(august, Actor));
    }

    [Fact]
    public void Two_different_operations_with_identical_fields_do_not_collide()
    {
        var personnel = new SendStaffLeaderPersonnelReportCommand
        {
            UserId = 7, FromDate = new DateTime(2026, 7, 1), ToDate = new DateTime(2026, 7, 31), Note = "x",
        };
        var deptPersonnel = new SendDeptLeaderPersonnelReportCommand
        {
            UserId = 7, FromDate = new DateTime(2026, 7, 1), ToDate = new DateTime(2026, 7, 31), Note = "x",
        };

        Assert.NotEqual(
            EmailSendFingerprint.Compute(personnel, Actor),
            EmailSendFingerprint.Compute(deptPersonnel, Actor));
    }

    // ── Every route is covered ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every idempotent send command, and nothing else. A send added later without the marker would go out
    /// unprotected and nobody would notice, so the count is asserted rather than the list only.
    ///
    /// <para>
    /// Nine since G11-H, not six. The three additions are the client-addressed sends — manual compose,
    /// reply and reply-all — which are the only routes where the CALLER chooses the recipients, and so the
    /// only ones where a duplicate delivers a second human-written message to named people. The count is
    /// raised here deliberately; it is not a threshold to relax when a new command trips this test.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_send_command_declares_itself_idempotent()
    {
        var declared = typeof(SendHoCampusReportCommand).Assembly.GetTypes()
            .Where(t => typeof(IIdempotentEmailSend).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .ToList();

        // Nine TYPES, ten codes: ReplytoEmailCommand answers to two of them, because Reply and Reply All
        // address different people and must not share a reservation.
        Assert.Equal(9, declared.Count);

        var codes = declared
            .Select(t => ((IIdempotentEmailSend)Activator.CreateInstance(t)!).OperationCode)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        // Every code a default-constructed command reports is a declared one…
        Assert.All(codes, c => Assert.Contains(c, EmailSendOperations.All));
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());

        // …and the only declared code no default instance produces is Reply All, which needs the flag set.
        var unreached = EmailSendOperations.All.Except(codes, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { EmailSendOperations.ManualReplyAll }, unreached);

        Assert.Equal(
            EmailSendOperations.ManualReplyAll,
            new PEMS.Application.Emails.Commands.ReplytoEmail.ReplytoEmailCommand { ReplyAll = true }.OperationCode);
    }

    /// <summary>
    /// Every idempotent command's response must be replayable, or a successful send could not be
    /// returned to a duplicate request and the behaviour would throw at runtime instead.
    /// </summary>
    [Fact]
    public void Every_idempotent_command_returns_a_replayable_result()
    {
        var commands = typeof(SendHoCampusReportCommand).Assembly.GetTypes()
            .Where(t => typeof(IIdempotentEmailSend).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        foreach (var command in commands)
        {
            var response = command.GetInterfaces()
                .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(MediatR.IRequest<>))
                .GetGenericArguments()[0];

            Assert.True(typeof(IEmailSendResult).IsAssignableFrom(response),
                $"{command.Name} returns {response.Name}, which cannot be replayed.");

            // Replay constructs the response reflectively, so it needs a parameterless constructor.
            Assert.NotNull(Activator.CreateInstance(response));
        }
    }

    [Fact]
    public void Every_operation_code_fits_the_column()
    {
        // operation_code is VARCHAR(64); a longer constant would be truncated by MySQL in non-strict
        // mode and would then collide with its own prefix.
        foreach (var code in EmailSendOperations.All)
            Assert.InRange(code.Length, 1, 64);
    }

    // ── Delivery-outcome classification ─────────────────────────────────────────────────────────

    [Fact]
    public void A_configuration_refusal_proves_nothing_was_sent()
    {
        Assert.True(EmailDeliveryCodes.ProvesNothingWasSent(
            EmailDeliveryResult.Skipped(EmailDeliveryCodes.SmtpDisabled)));
        Assert.True(EmailDeliveryCodes.ProvesNothingWasSent(
            EmailDeliveryResult.Failed(EmailDeliveryCodes.SmtpDisabled, "off")));
        Assert.True(EmailDeliveryCodes.ProvesNothingWasSent(
            EmailDeliveryResult.Failed(EmailDeliveryCodes.SmtpMisconfigured, "no host")));
    }

    [Fact]
    public void An_smtp_exception_proves_nothing_at_all()
    {
        // This is the case the whole distinction exists for: the client threw, and it cannot tell
        // "refused before acceptance" from "accepted, acknowledgement lost".
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(
            EmailDeliveryResult.Failed(EmailDeliveryCodes.SmtpSendFailed, "boom")));
    }

    [Fact]
    public void An_unclassified_failure_code_reads_as_unknown()
    {
        // Fail-closed: a code nobody has classified must never be treated as a clean failure, because
        // that is the direction that sends a second email.
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(
            EmailDeliveryResult.Failed("SOMETHING_NEW", "?")));
    }

    // ── The dispatch claim ──────────────────────────────────────────────────────────────────────

    private sealed class RecordingStore : IEmailSendReservationStore
    {
        public int DispatchMarks { get; private set; }

        public System.Threading.Tasks.Task<EmailSendReservation> ReserveAsync(
            ulong actorUserId, string operationCode, string keyHash, string requestFingerprint,
            System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(
                new EmailSendReservation(EmailSendReservationOutcome.Reserved, 1));

        public System.Threading.Tasks.Task MarkDispatchingAsync(
            ulong reservationId, System.Threading.CancellationToken cancellationToken = default)
        {
            DispatchMarks++;
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task MarkSucceededAsync(
            ulong reservationId, string resultMessage, ulong? sentEmailId,
            System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task MarkFailedAsync(
            ulong reservationId, bool dispatchStarted, string? failureCode,
            System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact]
    public async System.Threading.Tasks.Task An_attempt_with_no_reservation_records_nothing()
    {
        var store = new RecordingStore();
        var attempt = new EmailSendAttempt(store);

        // The same sender serves paths that are not idempotent sends; those must not write rows.
        await attempt.MarkDispatchingAsync();

        Assert.Equal(0, store.DispatchMarks);
        Assert.False(attempt.DispatchStarted);
        attempt.RecordSentEmail(99);
        Assert.Null(attempt.SentEmailId);
    }

    [Fact]
    public async System.Threading.Tasks.Task A_reserved_attempt_records_the_dispatch_and_the_history_row()
    {
        var store = new RecordingStore();
        var attempt = new EmailSendAttempt(store);
        attempt.Begin(1);

        attempt.RecordSentEmail(555);
        await attempt.MarkDispatchingAsync();

        Assert.Equal(1, store.DispatchMarks);
        Assert.True(attempt.DispatchStarted);
        Assert.Equal(555ul, attempt.SentEmailId);
    }

    [Fact]
    public void Withdrawing_the_dispatch_claim_narrows_what_the_system_says()
    {
        var attempt = new EmailSendAttempt(new RecordingStore());
        attempt.Begin(1);

        attempt.WithdrawDispatchClaim();
        Assert.False(attempt.DispatchStarted);
    }

    [Fact]
    public async System.Threading.Tasks.Task A_failed_transition_write_leaves_the_attempt_pre_dispatch()
    {
        // If the transition itself cannot be persisted, nothing was handed to the provider either.
        // Claiming otherwise would strand the user's key over a database blip.
        var attempt = new EmailSendAttempt(new ThrowingStore());
        attempt.Begin(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => attempt.MarkDispatchingAsync());
        Assert.False(attempt.DispatchStarted);
    }

    private sealed class ThrowingStore : IEmailSendReservationStore
    {
        public System.Threading.Tasks.Task<EmailSendReservation> ReserveAsync(
            ulong actorUserId, string operationCode, string keyHash, string requestFingerprint,
            System.Threading.CancellationToken cancellationToken = default)
            => throw new InvalidOperationException();

        public System.Threading.Tasks.Task MarkDispatchingAsync(
            ulong reservationId, System.Threading.CancellationToken cancellationToken = default)
            => throw new InvalidOperationException();

        public System.Threading.Tasks.Task MarkSucceededAsync(
            ulong reservationId, string resultMessage, ulong? sentEmailId,
            System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task MarkFailedAsync(
            ulong reservationId, bool dispatchStarted, string? failureCode,
            System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
