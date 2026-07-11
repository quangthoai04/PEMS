namespace PEMS.Application.Common.Exceptions;

/// <summary>
/// Typed failure of the UC-17 OTP challenge flow (issue / verify / recover). Carries the
/// HTTP status, a machine-readable <see cref="ErrorCode"/> (see <c>OtpErrorCodes</c>) and
/// the attempt metadata the client renders (<c>remainingAttempts</c>,
/// <c>retryAfterSeconds</c>, <c>humanVerificationRequired</c>) — the frontend must never
/// parse the Vietnamese message. Serialized by <c>ExceptionHandlingMiddleware</c>.
/// </summary>
public class OtpChallengeException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }
    public int? RemainingAttempts { get; }
    public int? RetryAfterSeconds { get; }
    public bool HumanVerificationRequired { get; }

    public OtpChallengeException(
        int statusCode,
        string errorCode,
        string message,
        int? remainingAttempts = null,
        int? retryAfterSeconds = null,
        bool humanVerificationRequired = false) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        RemainingAttempts = remainingAttempts;
        RetryAfterSeconds = retryAfterSeconds;
        HumanVerificationRequired = humanVerificationRequired;
    }
}
