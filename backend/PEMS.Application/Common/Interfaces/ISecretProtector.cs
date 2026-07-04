namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Symmetric at-rest protection for credential payloads stored in DB columns such as
/// api_configurations.credentials_json_encrypted. Implemented with AES-256-GCM in
/// Infrastructure; ciphertext is opaque base64. Never log inputs or outputs.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
