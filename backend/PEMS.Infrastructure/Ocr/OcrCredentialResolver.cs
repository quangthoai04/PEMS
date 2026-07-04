using System;
using System.IO;
using PEMS.Application.BusinessCardOcr.Services;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Infrastructure.Ocr;

/// <summary>
/// Resolution order (02 prompt §2): encrypted DB column → env var named by secret_ref
/// → GOOGLE_APPLICATION_CREDENTIALS file path. The returned raw JSON must never be
/// logged or serialized into any response.
/// </summary>
public sealed class OcrCredentialResolver : IOcrCredentialResolver
{
    private readonly ISecretProtector _secretProtector;

    public OcrCredentialResolver(ISecretProtector secretProtector) => _secretProtector = secretProtector;

    public string? Resolve(ApiConfiguration config)
    {
        if (!string.IsNullOrEmpty(config.CredentialsJsonEncrypted))
        {
            try { return _secretProtector.Unprotect(config.CredentialsJsonEncrypted); }
            catch { /* corrupt/rotated key — fall through to secret_ref */ }
        }

        if (!string.IsNullOrWhiteSpace(config.SecretRef))
        {
            var fromEnv = Environment.GetEnvironmentVariable(config.SecretRef.Trim());
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                // The env var may hold the JSON itself or a path to the key file.
                var value = fromEnv.Trim();
                if (value.StartsWith('{')) return value;
                if (File.Exists(value)) return File.ReadAllText(value);
            }
        }

        var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(credentialPath) && File.Exists(credentialPath))
            return File.ReadAllText(credentialPath);

        return null;
    }
}
