using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Verifies an FEID (FPT eID) login credential/code and returns the verified
/// identity. FEID has its own dedicated verifier (mirroring
/// <see cref="IGoogleTokenValidator"/>) rather than a single generic
/// <c>IExternalIdentityVerifier</c>, to match the codebase's per-provider pattern.
///
/// IMPORTANT: implementations must NEVER fabricate a successful identity. When no
/// real FEID provider is configured they must throw a coded
/// <c>AuthBusinessException(FEID_NOT_CONFIGURED, 403)</c> so the system fails in a
/// controlled, honest way instead of faking a login.
/// </summary>
public interface IFeidIdentityVerifier
{
    Task<ExternalIdentityResult> VerifyAsync(
        string idTokenOrCode,
        CancellationToken cancellationToken = default);
}
