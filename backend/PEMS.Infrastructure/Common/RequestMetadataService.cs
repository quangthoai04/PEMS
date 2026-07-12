using Microsoft.AspNetCore.Http;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Common;

/// <summary>
/// Extracts client IP / User-Agent from the CURRENT HTTP request (server-side) so
/// handlers never trust spoofable values from the request body. X-Forwarded-For's first
/// hop wins when present (reverse-proxy deployments), else the socket remote address.
/// </summary>
public sealed class RequestMetadataService : IRequestMetadataService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestMetadataService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public string? IpAddress
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null) return null;

            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.Split(',')[0].Trim();
                if (first.Length > 0) return first;
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent
        => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.FirstOrDefault();
}
