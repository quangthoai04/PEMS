using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Infrastructure.FileStorage.GoogleDrive;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Storage;

/// <summary>
/// Which refresh token a Drive call uses, and — the part that matters — which one it must NOT fall back to.
///
/// <para>
/// The token used to be a field on an options object bound at start-up, so replacing an expired one meant
/// editing a config file or a Railway variable and restarting the process. Moving it into
/// <c>api_configurations.credentials_json_encrypted</c> is what lets an ADMIN replace it from a screen; these
/// tests pin the precedence that makes that true, and the one case where falling back would hide a fault
/// instead of surviving it.
/// </para>
/// </summary>
public sealed class GoogleDriveCredentialResolverTests
{
    [Fact]
    public async Task A_stored_credential_is_preferred_over_the_configured_one()
    {
        await using var db = ApiIntegrationsTestDbContext.Create();
        var protector = new StubSecretProtector();
        db.ApiConfigurations.Add(DriveRow(protector, "token-from-the-database"));
        await db.SaveChangesAsync();

        var resolved = await Create(db, protector, "token-from-the-environment")
            .ResolveRefreshTokenAsync();

        Assert.Equal("token-from-the-database", resolved);
    }

    /// <summary>
    /// The migration affordance: a deployment whose ADMIN has not reconnected yet keeps working on the
    /// token its environment already supplies. This is the whole reason the Railway variable can stay in
    /// place for the length of the rollout instead of being swapped in the same deploy as the code.
    /// </summary>
    [Fact]
    public async Task With_no_stored_credential_the_configured_token_is_used()
    {
        await using var db = ApiIntegrationsTestDbContext.Create();

        var resolved = await Create(db, new StubSecretProtector(), "token-from-the-environment")
            .ResolveRefreshTokenAsync();

        Assert.Equal("token-from-the-environment", resolved);
    }

    [Fact]
    public async Task A_soft_deleted_drive_row_is_not_a_credential()
    {
        await using var db = ApiIntegrationsTestDbContext.Create();
        var protector = new StubSecretProtector();
        var row = DriveRow(protector, "token-from-the-database");
        row.DeletedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        db.ApiConfigurations.Add(row);
        await db.SaveChangesAsync();

        var resolved = await Create(db, protector, "token-from-the-environment")
            .ResolveRefreshTokenAsync();

        Assert.Equal("token-from-the-environment", resolved);
    }

    [Fact]
    public async Task With_neither_source_configured_there_is_no_token()
    {
        await using var db = ApiIntegrationsTestDbContext.Create();

        var resolved = await Create(db, new StubSecretProtector(), configuredToken: null)
            .ResolveRefreshTokenAsync();

        Assert.Null(resolved);
    }

    /// <summary>
    /// The fail-closed rule. A credential that IS stored and cannot be decrypted means the protection key
    /// changed — typically <c>Security:SecretProtectionKey</c> was never pinned, so it was derived from a
    /// JWT secret that has since rotated.
    ///
    /// <para>
    /// Falling through to the environment token here would leave every upload working and the console
    /// showing "connected", with the actual fault invisible until the day the environment variable is
    /// removed — which is the final step of this very rollout, and the worst possible moment to discover it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_unreadable_stored_credential_fails_closed_instead_of_falling_back()
    {
        await using var db = ApiIntegrationsTestDbContext.Create();
        db.ApiConfigurations.Add(new ApiConfiguration
        {
            ApiCode = GoogleDriveIntegrationConstants.ApiCode,
            Name = GoogleDriveIntegrationConstants.Name,
            BaseUrl = GoogleDriveIntegrationConstants.BaseUrl,
            CredentialsJsonEncrypted = "written-with-a-key-this-host-no-longer-has",
        });
        await db.SaveChangesAsync();

        var resolver = Create(db, new ThrowingSecretProtector(), "token-from-the-environment");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => resolver.ResolveRefreshTokenAsync());

        Assert.Equal(GoogleDriveErrorCodes.CredentialUnreadable, ex.ErrorCode);
    }

    /// <summary>Decrypted, but not the credential envelope — indistinguishable in effect from not decrypting.</summary>
    [Fact]
    public async Task A_stored_credential_without_a_refresh_token_is_unreadable_too()
    {
        await using var db = ApiIntegrationsTestDbContext.Create();
        var protector = new StubSecretProtector();
        db.ApiConfigurations.Add(new ApiConfiguration
        {
            ApiCode = GoogleDriveIntegrationConstants.ApiCode,
            Name = GoogleDriveIntegrationConstants.Name,
            BaseUrl = GoogleDriveIntegrationConstants.BaseUrl,
            CredentialsJsonEncrypted = protector.Protect("""{"clientSecret":"wrong document"}"""),
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create(db, protector, "token-from-the-environment").ResolveRefreshTokenAsync());

        Assert.Equal(GoogleDriveErrorCodes.CredentialUnreadable, ex.ErrorCode);
    }

    /// <summary>
    /// Nothing about the token reaches the refusal. The message is read by an ADMIN in a browser and the
    /// exception is logged by the host, so a credential appearing in either would be a leak in two places.
    /// </summary>
    [Fact]
    public async Task The_refusal_never_carries_the_token_it_could_not_read()
    {
        await using var db = ApiIntegrationsTestDbContext.Create();
        const string ciphertext = "a-ciphertext-that-cannot-be-opened";
        db.ApiConfigurations.Add(new ApiConfiguration
        {
            ApiCode = GoogleDriveIntegrationConstants.ApiCode,
            Name = GoogleDriveIntegrationConstants.Name,
            BaseUrl = GoogleDriveIntegrationConstants.BaseUrl,
            CredentialsJsonEncrypted = ciphertext,
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create(db, new ThrowingSecretProtector(), "token-from-the-environment")
                .ResolveRefreshTokenAsync());

        Assert.DoesNotContain(ciphertext, ex.Message);
        Assert.DoesNotContain("token-from-the-environment", ex.Message);
    }

    /// <summary>
    /// Nothing is cached. An ADMIN reconnect writes a new row value and the very next call must see it —
    /// without that, "no restart needed" would be false and the whole feature would be a slower version of
    /// editing a variable.
    /// </summary>
    [Fact]
    public async Task A_replaced_credential_is_picked_up_by_the_next_call()
    {
        await using var db = ApiIntegrationsTestDbContext.Create();
        var protector = new StubSecretProtector();
        var row = DriveRow(protector, "the-original-token");
        db.ApiConfigurations.Add(row);
        await db.SaveChangesAsync();

        var resolver = Create(db, protector, configuredToken: null);
        Assert.Equal("the-original-token", await resolver.ResolveRefreshTokenAsync());

        row.CredentialsJsonEncrypted = protector.Protect(
            new GoogleDriveCredentialEnvelope { RefreshToken = "the-reconnected-token" }.ToJson());
        await db.SaveChangesAsync();

        Assert.Equal("the-reconnected-token", await resolver.ResolveRefreshTokenAsync());
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    private static ApiConfiguration DriveRow(ISecretProtector protector, string refreshToken) => new()
    {
        ApiCode = GoogleDriveIntegrationConstants.ApiCode,
        Name = GoogleDriveIntegrationConstants.Name,
        BaseUrl = GoogleDriveIntegrationConstants.BaseUrl,
        CredentialsJsonEncrypted = protector.Protect(
            new GoogleDriveCredentialEnvelope { RefreshToken = refreshToken }.ToJson()),
    };

    private static GoogleDriveCredentialResolver Create(
        IApplicationDbContext db, ISecretProtector protector, string? configuredToken)
        => new(
            db,
            protector,
            Options.Create(new GoogleDriveOptions { RefreshToken = configuredToken }),
            NullLogger<GoogleDriveCredentialResolver>.Instance);

    /// <summary>Reversible, not secret — these tests are about precedence, not about AES.</summary>
    private sealed class StubSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "enc:" + plaintext;

        public string Unprotect(string ciphertext) => ciphertext.StartsWith("enc:", StringComparison.Ordinal)
            ? ciphertext[4..]
            : throw new System.Security.Cryptography.CryptographicException("tag mismatch");
    }

    /// <summary>A protector holding the wrong key: every stored credential is opaque to it.</summary>
    private sealed class ThrowingSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "enc:" + plaintext;

        public string Unprotect(string ciphertext)
            => throw new System.Security.Cryptography.CryptographicException("tag mismatch");
    }
}
