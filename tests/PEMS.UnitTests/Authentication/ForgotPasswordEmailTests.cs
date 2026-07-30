using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Authentication.Commands.ForgotPassword;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Authentication;

/// <summary>
/// Forgot-password (C-11), the one send point in the Auth batch.
///
/// <para>
/// Two properties have to hold at once and they pull in opposite directions. The endpoint must answer
/// identically whether or not the address belongs to an account — otherwise it becomes an account
/// enumeration oracle — while still emailing a real code to the accounts that qualify. Every test here
/// asserts the response AND what was (or was not) handed to the dispatcher, because a difference in
/// either one is the leak.
/// </para>
/// </summary>
public class ForgotPasswordEmailTests
{
    private const string KnownEmail = "owner@fpt.edu.vn";
    private const ulong UserId = 700;
    private const string IssuedCode = "418293";

    /// <summary>Issues a fixed code and remembers the arguments it was called with.</summary>
    private sealed class RecordingOtpService : IOtpService
    {
        public List<(ulong? UserId, string Purpose)> Created { get; } = new();

        public int CodeMinutes => 15;
        public int VisitRequestCodeMinutes => 5;

        public Task<string> CreateAsync(User user, string purpose, string? ip, string? ua, CancellationToken ct = default)
        {
            Created.Add((user.UserId, purpose));
            return Task.FromResult(IssuedCode);
        }

        public Task<string> CreateForEmailAsync(string email, string purpose, string? ip, string? ua, CancellationToken ct = default)
            => throw new NotSupportedException("Password reset issues a code for a known user.");
        public Task<OtpVerificationResult> VerifyAsync(string email, string purpose, string rawCode, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<OtpChallengeIssue> CreateChallengeAsync(string email, string purpose, string submissionId, string issueReason, string? ip, string? ua, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<OtpChallengeVerification> VerifyChallengeAsync(string sessionToken, string email, string purpose, string submissionId, string rawCode, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<OtpChallengeIssue> ResendChallengeAsync(string sessionToken, string email, string purpose, string? ip, string? ua, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<OtpChallengeIssue> RecoverChallengeAsync(string email, string purpose, string submissionId, string? ip, string? ua, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public RecordingOtpService Otp { get; } = new();
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public RecordingSecurityAuditService Audit { get; } = new();

        public ForgotPasswordCommandHandler Handler() => new(
            Db, Otp, Dispatcher, Audit, NullLogger<ForgotPasswordCommandHandler>.Instance);

        public Task<PEMS.Application.Authentication.Models.MessageResponse> Run(string email)
            => Handler().Handle(new ForgotPasswordCommand { Email = email }, CancellationToken.None);
    }

    /// <summary>
    /// Seeds an account. <paramref name="withLocalPassword"/> false leaves an SSO-only account, which has
    /// no password to reset.
    /// </summary>
    private static Harness Seed(
        string status = UserStatuses.Active, bool withLocalPassword = true, string email = KnownEmail)
    {
        var h = new Harness();
        var user = Uc106TestData.CreateUser(UserId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, null, 1);
        user.Email = email;
        user.FullName = "Nguyễn Văn Ánh";
        user.Status = status;
        user.PasswordHash = withLocalPassword ? "hash" : null;
        h.Db.Users.Add(user);

        if (!withLocalPassword)
            h.Db.UserAuthProviders.Add(new UserAuthProvider
            {
                UserId = UserId,
                ProviderType = ProviderTypes.GoogleSso,
                ProviderSubject = "google-subject-1",
                ProviderEmail = email,
                IsEnabled = true,
                LinkedAt = new DateTime(2026, 1, 1),
            });

        h.Db.SaveChanges();
        return h;
    }

    // ── The one case that sends ──────────────────────────────────────────────

    [Fact]
    public async Task An_active_local_password_account_is_emailed_the_code_from_the_template()
    {
        var h = Seed();

        await h.Run(KnownEmail);

        var sent = Assert.Single(h.Dispatcher.Sent);
        Assert.Equal(SystemEmailTemplates.AuthPasswordResetOtp, sent.TemplateCode);
        Assert.Equal(KnownEmail, sent.To.Email);
        Assert.Equal(IssuedCode, sent.Variables["otpCode"]);
        Assert.Equal("Nguyễn Văn Ánh", sent.Variables["fullName"]);
        // The lifetime stated in the mail is the one the token was really given, not a number typed
        // into the template or the handler.
        Assert.Equal(h.Otp.CodeMinutes.ToString(), sent.Variables["expireMinutes"]);
        Assert.Null(sent.TrustedBlocks);            // no action URL in this mail
        Assert.Equal(EmailLanguages.Vi, sent.Language);
    }

    [Fact]
    public async Task The_code_is_issued_before_it_is_emailed()
    {
        var h = Seed();

        await h.Run(KnownEmail);

        // The token exists first: a delivery failure then leaves a code the owner can still be given by
        // another route, rather than an account promised a code that was never created.
        var created = Assert.Single(h.Otp.Created);
        Assert.Equal(UserId, created.UserId);
        Assert.Equal(OtpPurposes.ChangeSensitiveAction, created.Purpose);
    }

    // ── Every case that must NOT send ────────────────────────────────────────

    [Theory]
    [InlineData("nobody@fpt.edu.vn", UserStatuses.Active, true)]      // no such account
    [InlineData(KnownEmail, UserStatuses.Inactive, true)]             // not active
    [InlineData(KnownEmail, UserStatuses.Locked, true)]               // locked
    [InlineData(KnownEmail, UserStatuses.PendingEmailConfirmation, true)]
    [InlineData(KnownEmail, UserStatuses.Active, false)]              // SSO only — nothing to reset
    public async Task No_code_is_sent_when_the_account_does_not_qualify(
        string requested, string status, bool withLocalPassword)
    {
        var h = Seed(status, withLocalPassword);

        await h.Run(requested);

        Assert.Empty(h.Dispatcher.Sent);
        Assert.Empty(h.Otp.Created);
    }

    [Fact]
    public async Task The_answer_is_identical_whether_or_not_the_address_belongs_to_an_account()
    {
        var known = await Seed().Run(KnownEmail);
        var unknown = await Seed().Run("nobody@fpt.edu.vn");
        var ssoOnly = await Seed(withLocalPassword: false).Run(KnownEmail);

        // Same words, so the response cannot be used to test whether an address is registered.
        Assert.Equal(known.Message, unknown.Message);
        Assert.Equal(known.Message, ssoOnly.Message);
    }

    [Fact]
    public async Task A_send_failure_is_swallowed_and_answers_the_same_way()
    {
        var h = Seed();
        h.Dispatcher.ThrowOnSend = new InvalidOperationException("SMTP down");

        var failed = await h.Run(KnownEmail);
        var ok = await Seed().Run(KnownEmail);

        // A broken mail path must not turn into a different answer — that difference is itself an oracle.
        Assert.Equal(ok.Message, failed.Message);
        // …and the code was still issued, so the reset is not lost.
        Assert.Single(h.Otp.Created);
    }

    // ── The secret must not travel anywhere else ─────────────────────────────

    [Fact]
    public async Task The_raw_code_appears_only_as_the_otpCode_variable()
    {
        var h = Seed();

        await h.Run(KnownEmail);

        var sent = Assert.Single(h.Dispatcher.Sent);
        Assert.Equal(IssuedCode, sent.Variables["otpCode"]);
        // Not in the recipient, not in a trusted block, not smuggled into another variable.
        Assert.DoesNotContain(IssuedCode, sent.To.Email);
        Assert.DoesNotContain(IssuedCode, sent.To.DisplayName ?? string.Empty);
        Assert.DoesNotContain(
            sent.Variables.Where(v => v.Key != "otpCode").Select(v => v.Value),
            v => v.Contains(IssuedCode, StringComparison.Ordinal));
        // The template code carries the sensitivity flag that keeps the body out of the history.
        Assert.True(SensitiveEmailHistory.OmitsBody(sent.TemplateCode));
    }
}
