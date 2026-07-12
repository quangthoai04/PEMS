using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Identity;

namespace PEMS.IntegrationTests.Api;

/// <summary>
/// JWT protocol boundary (§9.1): NumericDate claims stay UTC/Unix, lifetime is exact,
/// and the Vietnam wall-clock snapshot of the expiry is the SAME instant (never ±7h).
/// Pure unit tests — no database; lives here because JwtTokenService is Infrastructure.
/// </summary>
public class JwtLifetimeTests
{
    private static JwtTokenService CreateService(int minutes = 60)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "unit-test-secret-key-with-enough-length-0123456789",
            ["JwtSettings:AccessTokenMinutes"] = minutes.ToString(),
            ["JwtSettings:Issuer"] = "pems-tests",
            ["JwtSettings:Audience"] = "pems-tests",
        }).Build();
        return new JwtTokenService(config);
    }

    private static User TestUser() => new()
    {
        UserId = 42,
        Email = "jwt-test@pems.local",
        FullName = "JWT Test",
        RoleId = 1,
    };

    [Fact]
    public void AccessToken_Lives_Exactly_60_Minutes()
    {
        var before = DateTimeOffset.UtcNow;
        var result = CreateService(60).GenerateAccessToken(TestUser(), sessionId: 7, loginPortal: "INTERNAL");
        var after = DateTimeOffset.UtcNow;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        // exp − nbf must be the configured lifetime (±1s NumericDate truncation between
        // the two clock reads inside the service) — never off by hours.
        var lifetime = jwt.ValidTo - jwt.ValidFrom;
        Assert.InRange(lifetime.TotalSeconds, 3599, 3601);

        // exp is a real UTC/Unix NumericDate anchored at "now + 60m".
        Assert.InRange(
            new DateTimeOffset(jwt.ValidTo).ToUnixTimeSeconds(),
            before.AddMinutes(60).AddSeconds(-5).ToUnixTimeSeconds(),
            after.AddMinutes(60).AddSeconds(5).ToUnixTimeSeconds());
    }

    [Fact]
    public void ExpiresAt_Snapshot_Converted_To_VN_Is_Same_Instant_As_Exp_Claim()
    {
        var result = CreateService(60).GenerateAccessToken(TestUser(), sessionId: 7, loginPortal: "INTERNAL");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        // The DTO carries the UTC expiry; the persistence-side representation is the VN
        // wall-clock of that SAME instant. Round-tripping back to Unix must not move it.
        var vnWallClock = VietnamTime.FromUtc(result.ExpiresAt);
        var backToUtc = VietnamTime.ToUtc(vnWallClock);

        Assert.Equal(TimeSpan.FromHours(7), vnWallClock - backToUtc);
        Assert.Equal(
            new DateTimeOffset(jwt.ValidTo).ToUnixTimeSeconds(),
            new DateTimeOffset(DateTime.SpecifyKind(backToUtc, DateTimeKind.Utc)).ToUnixTimeSeconds());
    }
}
