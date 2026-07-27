using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.ConfirmAccountEmail;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.EmailConfirmation;

/// <summary>
/// P0 #1 confirm flow: a valid, unexpired, PENDING token activates the account EXACTLY once; replay is
/// idempotent (ALREADY_CONFIRMED, no second activation); unknown / expired / superseded / email-mismatched
/// / already-active tokens never activate; and confirming supersedes any other live token for the user.
/// </summary>
public class ConfirmAccountEmailCommandHandlerTests
{
    /// <summary>Deterministic token service so the seeded hash matches what the handler computes.</summary>
    private sealed class FakeTokens : IEmailActionTokenService
    {
        public string GenerateRawToken() => Guid.NewGuid().ToString("N");
        public string Hash(string rawToken) => "h:" + rawToken;
        public string BuildPublicActionUrl(string rawToken) => "http://x/" + rawToken;
        public string BuildDepartmentAssignmentUrl(ulong visitInstanceId, ulong participantId) => "http://x";
        public string BuildLogisticsDetailUrl(ulong logisticsItemId) => "http://x";
        public string BuildVisitInstanceDetailUrl(ulong visitRequestId, ulong visitInstanceId) => "http://x";
    }

    private const ulong UserId = 700;
    private const string Email = "owner@fpt.edu.vn";

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeDateTimeService Clock { get; } = new();
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public Mock<PEMS.Application.Accounts.Common.IAccountEmailConfirmationService> Confirmations { get; } = new();
        public FakeTokens Tokens { get; } = new();
        public ConfirmAccountEmailCommandHandler Handler { get; }

        public Harness()
        {
            Confirmations.Setup(c => c.BuildLoginUrl()).Returns("http://x/login");
            Handler = new ConfirmAccountEmailCommandHandler(Db, Tokens, Clock, Dispatcher, Confirmations.Object);
        }

        public Task<ConfirmAccountEmailResponse> Confirm(string token) =>
            Handler.Handle(new ConfirmAccountEmailCommand { Token = token }, CancellationToken.None);
    }

    private static Harness Seed(
        string rawToken, string? status = null, DateTime? expiresAt = null,
        string? userStatus = null, string? targetEmail = null, string? userEmail = null)
    {
        var h = new Harness();
        var user = Uc106TestData.CreateUser(UserId, Uc106TestData.StudentRoleId, null, null);
        user.Email = userEmail ?? Email;
        user.Status = userStatus ?? UserStatuses.PendingEmailConfirmation;
        h.Db.Users.Add(user);
        h.Db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = UserId,
            TargetEmail = targetEmail ?? Email,
            TokenHash = h.Tokens.Hash(rawToken),
            Status = status ?? AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = expiresAt ?? h.Clock.VietnamNow.AddDays(1),
            CreatedAt = h.Clock.VietnamNow,
        });
        h.Db.SaveChanges();
        return h;
    }

    [Fact]
    public async Task Valid_token_activates_the_account_once()
    {
        var h = Seed("valid");

        var res = await h.Confirm("valid");

        Assert.True(res.Success);
        Assert.Equal(ConfirmAccountEmailStatuses.Confirmed, res.Status);
        Assert.Equal(UserStatuses.Active, (await h.Db.Users.SingleAsync()).Status);
        var row = await h.Db.AccountEmailConfirmations.SingleAsync();
        Assert.Equal(AccountEmailConfirmationStatuses.Confirmed, row.Status);
        Assert.NotNull(row.ConfirmedAt);

        // The "you can sign in now" mail is sent ONLY here — after confirmation — and comes from the
        // activated template, not from a string in this handler.
        var sent = h.Dispatcher.Single(SystemEmailTemplates.AccountActivated);
        Assert.Equal(Email, sent.To.Email);
        Assert.Contains("http://x/login", sent.TrustedBlocks![EmailTrustedBlocks.ActionBlock]);
    }

    [Fact]
    public async Task An_unusable_token_sends_no_activation_mail()
    {
        var h = Seed("valid", expiresAt: new FakeDateTimeService().VietnamNow.AddHours(-1));

        await h.Confirm("valid");

        Assert.Empty(h.Dispatcher.Sent);
    }

    [Fact]
    public async Task Replay_of_confirmed_token_is_idempotent_and_does_not_reactivate()
    {
        var h = Seed("valid");
        await h.Confirm("valid");

        var res = await h.Confirm("valid");

        Assert.True(res.Success);
        Assert.Equal(ConfirmAccountEmailStatuses.AlreadyConfirmed, res.Status);
        Assert.Equal(UserStatuses.Active, (await h.Db.Users.SingleAsync()).Status);
    }

    [Fact]
    public async Task Unknown_token_is_invalid_and_leaves_the_account_pending()
    {
        var h = Seed("valid");

        var res = await h.Confirm("nope");

        Assert.False(res.Success);
        Assert.Equal(ConfirmAccountEmailStatuses.Invalid, res.Status);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await h.Db.Users.SingleAsync()).Status);
    }

    [Fact]
    public async Task Expired_token_is_rejected_and_marked_expired()
    {
        var h = Seed("valid", expiresAt: new FakeDateTimeService().VietnamNow.AddHours(-1));

        var res = await h.Confirm("valid");

        Assert.False(res.Success);
        Assert.Equal(ConfirmAccountEmailStatuses.Expired, res.Status);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await h.Db.Users.SingleAsync()).Status);
        Assert.Equal(AccountEmailConfirmationStatuses.Expired, (await h.Db.AccountEmailConfirmations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Token_whose_target_email_no_longer_matches_the_account_is_invalid()
    {
        var h = Seed("valid", targetEmail: "old@fpt.edu.vn", userEmail: "new@fpt.edu.vn");

        var res = await h.Confirm("valid");

        Assert.Equal(ConfirmAccountEmailStatuses.Invalid, res.Status);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await h.Db.Users.SingleAsync()).Status);
    }

    [Fact]
    public async Task Token_for_an_already_active_account_does_not_reactivate()
    {
        var h = Seed("valid", userStatus: UserStatuses.Active);

        var res = await h.Confirm("valid");

        Assert.Equal(ConfirmAccountEmailStatuses.Invalid, res.Status);
    }

    [Fact]
    public async Task Superseded_token_is_invalid()
    {
        var h = Seed("valid", status: AccountEmailConfirmationStatuses.Superseded);

        var res = await h.Confirm("valid");

        Assert.Equal(ConfirmAccountEmailStatuses.Invalid, res.Status);
    }

    [Fact]
    public async Task Confirming_supersedes_other_live_tokens_for_the_same_user()
    {
        var h = Seed("valid");
        h.Db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = UserId,
            TargetEmail = Email,
            TokenHash = h.Tokens.Hash("other"),
            Status = AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = h.Clock.VietnamNow.AddDays(1),
            CreatedAt = h.Clock.VietnamNow,
        });
        await h.Db.SaveChangesAsync();

        await h.Confirm("valid");

        var other = await h.Db.AccountEmailConfirmations.SingleAsync(c => c.TokenHash == h.Tokens.Hash("other"));
        Assert.Equal(AccountEmailConfirmationStatuses.Superseded, other.Status);
        Assert.Equal(ConfirmAccountEmailStatuses.Invalid, (await h.Confirm("other")).Status);   // old link dead
    }
}
