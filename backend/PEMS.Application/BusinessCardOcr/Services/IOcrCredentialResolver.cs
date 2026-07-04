using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Application.BusinessCardOcr.Services;

/// <summary>
/// Resolves the raw Google service-account JSON for a config, in priority order:
/// 1) credentials_json_encrypted (decrypted via ISecretProtector),
/// 2) environment variable named by secret_ref,
/// 3) GOOGLE_APPLICATION_CREDENTIALS file path.
/// Returns null when nothing is configured. NEVER log the returned value.
/// </summary>
public interface IOcrCredentialResolver
{
    string? Resolve(ApiConfiguration config);
}
