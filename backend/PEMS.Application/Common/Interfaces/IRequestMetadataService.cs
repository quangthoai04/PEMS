namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Server-derived metadata of the current HTTP request. Handlers use this instead of
/// accepting IP/User-Agent from the request body — client-supplied values are spoofable
/// and must never be trusted for rate limiting or audit rows.
/// </summary>
public interface IRequestMetadataService
{
    /// <summary>Client IP (X-Forwarded-For first hop when present, else the socket address).</summary>
    string? IpAddress { get; }

    /// <summary>Raw User-Agent header, if any.</summary>
    string? UserAgent { get; }
}
