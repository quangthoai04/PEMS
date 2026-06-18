namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// A controlled authentication / authorisation business failure that carries a stable
/// machine-readable <see cref="ErrorCode"/> and the HTTP <see cref="StatusCode"/> to
/// return. Unlike <see cref="AuthenticationFailedException"/> (always 401 + generic
/// message), this is used by the dual-portal login flow where the client must
/// distinguish wrong-portal / campus / not-found cases (403 / 400).
/// <para>
/// The <see cref="Message"/> is safe to surface; the frontend usually replaces it with a
/// localized message keyed off <see cref="ErrorCode"/>.
/// </para>
/// </summary>
public class AuthBusinessException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }

    public AuthBusinessException(string errorCode, string message, int statusCode = 403)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}
