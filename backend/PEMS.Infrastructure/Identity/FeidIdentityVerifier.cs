using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// FEID (FPT eID) identity verifier.
///
/// There is currently NO real FEID provider credential/endpoint wired into PEMS,
/// so this adapter deliberately fails in a controlled way
/// (<c>AuthBusinessException(FEID_NOT_CONFIGURED, 403)</c>) instead of fabricating a
/// successful login. The verification logic against a real FEID API is left as a
/// clearly-marked TODO so it can be filled in without changing the calling handler.
///
/// Config section (appsettings <c>"Feid"</c>):
/// <code>
/// "Feid": { "BaseUrl": "", "ClientId": "", "ClientSecret": "" }
/// </code>
/// </summary>
public sealed class FeidIdentityVerifier : IFeidIdentityVerifier
{
    private readonly ILogger<FeidIdentityVerifier> _logger;
    private readonly string? _baseUrl;
    private readonly string? _clientId;
    private readonly string? _clientSecret;

    public FeidIdentityVerifier(IConfiguration configuration, ILogger<FeidIdentityVerifier> logger)
    {
        _logger = logger;

        var section = configuration.GetSection("Feid");
        _baseUrl = section["BaseUrl"];
        _clientId = section["ClientId"];
        _clientSecret = section["ClientSecret"];
    }

    /// <summary>True only when every required FEID credential is present in config.</summary>
    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_baseUrl)
        && !string.IsNullOrWhiteSpace(_clientId)
        && !string.IsNullOrWhiteSpace(_clientSecret);

    public Task<ExternalIdentityResult> VerifyAsync(
        string idTokenOrCode, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("FEID login attempted but no FEID provider is configured (\"Feid\" section is empty).");
            throw new AuthBusinessException(
                AuthErrorCodes.FeidNotConfigured,
                "FEID sign-in is not available yet. Please use another sign-in method or contact the administrator.",
                403);
        }

        // TODO(FEID integration): when a real FEID provider exists, perform the
        // exchange/verification here using _baseUrl/_clientId/_clientSecret and the
        // supplied idTokenOrCode, then return a populated ExternalIdentityResult:
        //
        //   return new ExternalIdentityResult
        //   {
        //       Provider        = ProviderTypes.FeId,
        //       ProviderSubject = <verified subject>,
        //       Email           = <verified email>,
        //       EmailVerified   = true,
        //       DisplayName     = <name>,
        //       StudentCode     = <student code, if any>,
        //       Cohort          = <cohort, e.g. "K19">,
        //   };
        //
        // Eligibility (e.g. AuthOptions.StudentFeidMinCohort) must be enforced AFTER a
        // real identity is obtained — throw AuthBusinessException(FEID_NOT_ELIGIBLE, 403)
        // when the cohort is below the minimum. Until then we never fake success:
        _logger.LogError("FEID provider is configured but the verification integration is not implemented.");
        throw new AuthBusinessException(
            AuthErrorCodes.FeidNotConfigured,
            "FEID sign-in is not available yet. Please use another sign-in method or contact the administrator.",
            403);
    }
}
