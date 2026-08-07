using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Infrastructure.FileStorage.GoogleDrive;
using PEMS.Infrastructure.Security;
using Xunit;

namespace PEMS.IntegrationTests.Storage;

/// <summary>
/// The <c>state</c> parameter is the ONLY thing gating the Google Drive OAuth callback: the callback has to
/// be anonymous, because Google redirects a browser to it with no Authorization header. So every property
/// asserted here is load-bearing — a state that could be edited would let anyone name themselves the admin
/// on an endpoint that stores a credential for a shared Drive account.
///
/// <para>
/// Runs against the REAL <see cref="AesGcmSecretProtector"/>, not a stub. A stub that returned its input
/// would pass every one of these while proving nothing: the refusals below exist because AES-GCM's
/// authentication tag fails closed, and that is exactly the part a double would replace.
/// </para>
/// </summary>
public sealed class GoogleDriveOAuthStateServiceTests
{
    [Fact]
    public void A_state_it_issued_round_trips_with_the_admin_who_started_the_flow()
    {
        var service = Create(out _);

        var result = service.Validate(service.Create(adminUserId: 42));

        Assert.True(result.IsValid);
        Assert.Equal(42ul, result.State!.AdminUserId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Two_reconnects_never_produce_the_same_state()
    {
        var service = Create(out _);

        Assert.NotEqual(service.Create(1), service.Create(1));
    }

    /// <summary>
    /// The privilege-escalation case. Flipping any byte must fail the authentication tag — otherwise a
    /// caller could rewrite <c>adminUserId</c> and have the reconnect attributed to someone else, or
    /// rewrite the deadline and use a state indefinitely.
    /// </summary>
    [Fact]
    public void An_altered_state_is_refused()
    {
        var service = Create(out _);
        var issued = service.Create(adminUserId: 42);

        // Change one character in the middle, keeping the length and the alphabet valid.
        var chars = issued.ToCharArray();
        chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';

        var result = service.Validate(new string(chars));

        Assert.False(result.IsValid);
        Assert.Null(result.State);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.InvalidState, result.FailureReason);
    }

    [Fact]
    public void A_state_sealed_by_another_deployment_is_refused()
    {
        var theirs = Create(out _);
        var ours = Create(out _, key: Convert.ToBase64String(new byte[32]));

        var result = ours.Validate(theirs.Create(adminUserId: 42));

        Assert.False(result.IsValid);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.InvalidState, result.FailureReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64url-at-all!!")]
    [InlineData("YWJj")] // valid base64url, not a sealed payload
    public void Junk_is_refused_without_throwing(string? state)
    {
        var result = Create(out _).Validate(state);

        Assert.False(result.IsValid);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.InvalidState, result.FailureReason);
    }

    /// <summary>
    /// Expiry is reported separately from forgery, because the two mean different things to the admin
    /// reading the toast: "start again, you took too long" versus "that link did not come from here".
    /// </summary>
    [Fact]
    public void A_state_older_than_its_window_is_refused_as_expired()
    {
        var service = Create(out var clock);
        var issued = service.Create(adminUserId: 42);

        clock.Advance(TimeSpan.FromMinutes(6));

        var result = service.Validate(issued);

        Assert.False(result.IsValid);
        Assert.Null(result.State);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.StateExpired, result.FailureReason);
    }

    [Fact]
    public void A_state_still_inside_its_window_is_accepted()
    {
        var service = Create(out var clock);
        var issued = service.Create(adminUserId: 42);

        clock.Advance(TimeSpan.FromMinutes(4));

        Assert.True(service.Validate(issued).IsValid);
    }

    /// <summary>
    /// The value travels in a query string, through Google, and back. Standard base64 would carry
    /// <c>+</c>, <c>/</c> and <c>=</c> — and a <c>+</c> that any hop decodes as a space comes back as a
    /// state that no longer authenticates, which would look exactly like tampering and would reproduce only
    /// for some tokens.
    /// </summary>
    [Fact]
    public void The_issued_state_is_url_safe()
    {
        var service = Create(out _);

        for (var i = 0; i < 20; i++)
        {
            var issued = service.Create(adminUserId: (ulong)(i + 1));
            Assert.DoesNotContain('+', issued);
            Assert.DoesNotContain('/', issued);
            Assert.DoesNotContain('=', issued);
            Assert.Equal(issued, Uri.EscapeDataString(issued));
        }
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    private static GoogleDriveOAuthStateService Create(out MovableClock clock, string? key = null)
    {
        clock = new MovableClock();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SecretProtectionKey"] =
                    key ?? Convert.ToBase64String(
                        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            })
            .Build();

        return new GoogleDriveOAuthStateService(new AesGcmSecretProtector(configuration), clock);
    }

    private sealed class MovableClock : IDateTimeService
    {
        private DateTime _utcNow = new(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => _utcNow;
        public DateTime VietnamNow => _utcNow.AddHours(7);

        public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
    }
}
