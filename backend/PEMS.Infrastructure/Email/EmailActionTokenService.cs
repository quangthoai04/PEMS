using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// Default <see cref="IEmailActionTokenService"/>. Random tokens + SHA-256 hashing (same scheme as
/// the auth refresh/OTP tokens) and URL building from configured base addresses:
///   App:PublicApiBaseUrl  → where the public email-action endpoint is reachable (the API itself),
///   App:FrontendBaseUrl   → the SPA, for the login-required department-assignment link.
/// </summary>
public sealed class EmailActionTokenService : IEmailActionTokenService
{
    private readonly string _publicApiBaseUrl;
    private readonly string _frontendBaseUrl;

    public EmailActionTokenService(IConfiguration configuration)
    {
        _publicApiBaseUrl = (configuration["App:PublicApiBaseUrl"] ?? "http://localhost:5265").TrimEnd('/');
        _frontendBaseUrl = (configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    }

    public string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    public string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public string BuildPublicActionUrl(string rawToken)
        => $"{_publicApiBaseUrl}/api/public/email-actions/{Uri.EscapeDataString(rawToken)}";

    public string BuildDepartmentAssignmentUrl(ulong visitInstanceId, ulong participantId)
        => $"{_frontendBaseUrl}/dashboard/visit/department-tasks/{participantId}";
}
