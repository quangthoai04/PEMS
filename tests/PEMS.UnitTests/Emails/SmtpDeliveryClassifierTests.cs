using System;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Authentication;
using PEMS.Application.Common.Interfaces;
using PEMS.Infrastructure.Email;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// How an SMTP send failure is classified — pure, no network. Mirrors
/// <c>ResendDeliveryClassifierTests</c>: before this classifier existed every SMTP exception collapsed
/// into <c>SMTP_SEND_FAILED</c>, indistinguishable whether the server rejected one recipient, refused
/// credentials, or the socket died — which made every failure look alike to anything reading
/// <c>sent_emails</c> later, and made "is a retry safe" impossible to answer (email fidelity plan, Phase D).
/// </summary>
public sealed class SmtpDeliveryClassifierTests
{
    // ── Recipient rejection — message-level ambiguous, never a clean rejection ────────────────────

    [Fact]
    public void A_single_failed_recipient_is_classified_but_stays_ambiguous_at_message_level()
    {
        var ex = new SmtpFailedRecipientException(
            SmtpStatusCode.MailboxUnavailable, "user@example.com", "550 mailbox unavailable");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.Equal(SmtpFailureCategory.RecipientRejected, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpRecipientRejected, result.Code);
        // PEMS sends one envelope covering TO+CC+BCC; another recipient in that same transaction may
        // already have been accepted, so a single rejected recipient proves nothing about the rest.
        Assert.False(result.IsDefinitiveFailure);
        Assert.True(result.IsAmbiguous);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public void Recipient_rejected_is_never_in_ProvesNothingWasSent()
    {
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(EmailDeliveryCodes.SmtpRecipientRejected));
    }

    // ── Auth — evidence-gated, never guessed from a bare status code ──────────────────────────────

    [Fact]
    public void Explicit_authentication_evidence_in_the_message_classifies_as_auth_failed()
    {
        var ex = new SmtpException(
            SmtpStatusCode.GeneralFailure,
            "The SMTP server requires a secure connection or the client was not authenticated.");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.Equal(SmtpFailureCategory.AuthFailed, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpAuthFailed, result.Code);
        Assert.True(result.IsDefinitiveFailure);
        Assert.False(result.IsAmbiguous);
    }

    [Fact]
    public void ClientNotPermitted_without_auth_evidence_is_NOT_classified_as_auth_failed()
    {
        // SmtpStatusCode.ClientNotPermitted (454) is a real .NET value, but real servers use it for
        // assorted temporary policy rejections — it is not proof of a credentials problem on its own.
        var ex = new SmtpException(SmtpStatusCode.ClientNotPermitted, "Temporarily unable to accept.");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.NotEqual(SmtpFailureCategory.AuthFailed, result.Category);
        Assert.Equal(SmtpFailureCategory.TemporaryRejected, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpTemporaryRejected, result.Code);
    }

    // ── Quota vs rate vs generic temporary — three distinct codes, evidence-gated ─────────────────

    [Fact]
    public void Explicit_quota_evidence_classifies_as_quota_exceeded()
    {
        var ex = new SmtpException(SmtpStatusCode.GeneralFailure, "Daily sending limit exceeded for this account.");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.Equal(SmtpFailureCategory.QuotaExceeded, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpQuotaExceeded, result.Code);
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(result.Code));
    }

    [Fact]
    public void Explicit_rate_evidence_classifies_as_rate_limited()
    {
        var ex = new SmtpException(SmtpStatusCode.GeneralFailure, "421 too many messages, please slow down.");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.Equal(SmtpFailureCategory.RateLimited, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpRateLimited, result.Code);
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(result.Code));
    }

    [Fact]
    public void Generic_temporary_rejection_with_no_evidence_either_way_is_its_own_code()
    {
        var ex = new SmtpException(SmtpStatusCode.MailboxBusy, "Mailbox busy, try again later.");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.Equal(SmtpFailureCategory.TemporaryRejected, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpTemporaryRejected, result.Code);
        Assert.NotEqual(EmailDeliveryCodes.SmtpRateLimited, result.Code);
        Assert.NotEqual(EmailDeliveryCodes.SmtpQuotaExceeded, result.Code);
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(result.Code));
    }

    [Fact]
    public void Insufficient_storage_is_also_a_generic_temporary_rejection()
    {
        var ex = new SmtpException(SmtpStatusCode.InsufficientStorage, "452 insufficient system storage");
        Assert.Equal(EmailDeliveryCodes.SmtpTemporaryRejected, SmtpDeliveryClassifier.Classify(ex).Code);
    }

    // ── TLS ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Server_demanding_starttls_classifies_as_tls_failed()
    {
        var ex = new SmtpException(SmtpStatusCode.MustIssueStartTlsFirst, "Must issue a STARTTLS command first.");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.Equal(SmtpFailureCategory.TlsFailed, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpTlsFailed, result.Code);
        Assert.True(result.IsDefinitiveFailure);
    }

    [Fact]
    public void An_inner_authentication_exception_on_a_generic_SmtpException_is_tls_failed()
    {
        var ex = new SmtpException(
            "Failure sending mail.", new AuthenticationException("The remote certificate is invalid."));

        Assert.Equal(EmailDeliveryCodes.SmtpTlsFailed, SmtpDeliveryClassifier.Classify(ex).Code);
    }

    [Fact]
    public void A_bare_authentication_exception_classifies_as_tls_failed()
    {
        var result = SmtpDeliveryClassifier.Classify(new AuthenticationException("Handshake failed."));
        Assert.Equal(EmailDeliveryCodes.SmtpTlsFailed, result.Code);
    }

    // ── Connection ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_bare_socket_exception_classifies_as_connection_failed()
    {
        var result = SmtpDeliveryClassifier.Classify(new SocketException());

        Assert.Equal(SmtpFailureCategory.ConnectionFailed, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpConnectionFailed, result.Code);
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(result.Code));
    }

    [Fact]
    public void An_inner_socket_exception_on_a_generic_SmtpException_is_connection_failed()
    {
        var ex = new SmtpException("Failure sending mail.", new SocketException());

        Assert.Equal(EmailDeliveryCodes.SmtpConnectionFailed, SmtpDeliveryClassifier.Classify(ex).Code);
    }

    // ── Timeout ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_timeout_exception_is_its_own_ambiguous_code()
    {
        var result = SmtpDeliveryClassifier.Classify(new TimeoutException());

        Assert.Equal(SmtpFailureCategory.Timeout, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpTimeout, result.Code);
        Assert.True(result.IsAmbiguous);
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(result.Code));
    }

    // ── Provider-rejected (definitive, generic bucket) ─────────────────────────────────────────────

    [Fact]
    public void A_well_formed_rejection_with_no_more_specific_category_is_provider_rejected()
    {
        var ex = new SmtpException(SmtpStatusCode.TransactionFailed, "Transaction failed.");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.Equal(SmtpFailureCategory.ProviderRejected, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpProviderRejected, result.Code);
        Assert.True(result.IsDefinitiveFailure);
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(result.Code));
    }

    // ── Network-unknown (the safe ambiguous default) ────────────────────────────────────────────

    [Fact]
    public void GeneralFailure_with_no_typed_or_textual_evidence_is_network_unknown()
    {
        var ex = new SmtpException(SmtpStatusCode.GeneralFailure, "Failure sending mail.");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.Equal(SmtpFailureCategory.NetworkUnknown, result.Category);
        Assert.Equal(EmailDeliveryCodes.SmtpNetworkUnknown, result.Code);
        Assert.True(result.IsAmbiguous);
        Assert.False(EmailDeliveryCodes.ProvesNothingWasSent(result.Code));
    }

    [Fact]
    public void A_completely_unrecognized_exception_is_network_unknown()
    {
        var result = SmtpDeliveryClassifier.Classify(new InvalidOperationException("something else"));
        Assert.Equal(EmailDeliveryCodes.SmtpNetworkUnknown, result.Code);
    }

    // ── Safe message never echoes provider/exception text ──────────────────────────────────────

    [Fact]
    public void The_safe_message_never_contains_the_rejected_recipient_address()
    {
        var ex = new SmtpFailedRecipientException(
            SmtpStatusCode.MailboxUnavailable, "victim@example.com", "550 mailbox unavailable: victim@example.com");

        var result = SmtpDeliveryClassifier.Classify(ex);

        Assert.DoesNotContain("victim@example.com", result.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── EmailAttemptRecord round-trip on every new code ────────────────────────────────────────

    [Theory]
    [InlineData("SMTP_AUTH_FAILED")]
    [InlineData("SMTP_RECIPIENT_REJECTED")]
    [InlineData("SMTP_RATE_LIMITED")]
    [InlineData("SMTP_QUOTA_EXCEEDED")]
    [InlineData("SMTP_TEMPORARY_REJECTED")]
    [InlineData("SMTP_TLS_FAILED")]
    [InlineData("SMTP_CONNECTION_FAILED")]
    [InlineData("SMTP_TIMEOUT")]
    [InlineData("SMTP_PROVIDER_REJECTED")]
    [InlineData("SMTP_NETWORK_UNKNOWN")]
    public void Every_new_code_round_trips_through_EmailAttemptRecord(string code)
    {
        var delivery = EmailDeliveryResult.Failed(code, "an operator-safe message");
        var formatted = EmailAttemptRecord.Format(delivery);

        Assert.Equal(code, EmailAttemptRecord.CodeOf(formatted));
        Assert.Equal(EmailAttemptOutcome.Unknown, EmailAttemptRecord.Classify(null, formatted));
    }
}
