using Microsoft.AspNetCore.Http;
using PEMS.Application.Emails.Idempotency;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// Reads <c>Idempotency-Key</c> off the current request.
///
/// <para>
/// It reads and nothing more — no validation, no normalisation, no trimming. What counts as a usable key
/// is a contract decision and lives in <see cref="IdempotencyKey"/>; if this class trimmed whitespace,
/// two keys the client considers different would silently become one.
/// </para>
/// <para>
/// A duplicated header is treated as no key at all. HTTP allows the repetition, the client never means
/// it, and picking one of the two would be a guess about which attempt the caller thought they were
/// naming.
/// </para>
/// </summary>
public sealed class HttpIdempotencyKeyAccessor : IIdempotencyKeyAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpIdempotencyKeyAccessor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public string? CurrentKey
    {
        get
        {
            var headers = _httpContextAccessor.HttpContext?.Request.Headers;
            if (headers is null) return null;

            var values = headers[IdempotencyKey.HeaderName];
            return values.Count == 1 ? values[0] : null;
        }
    }
}
