using System.Diagnostics;
using System.Text.Json;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;

namespace PEMS.Api.Middleware;

/// <summary>
/// Converts known application exceptions into safe HTTP responses.
///
/// Production responses NEVER leak exception details, stack traces, connection
/// strings or SQL — only <c>success</c>, <c>errorCode</c>, <c>message</c> and a
/// <c>traceId</c> for support. Development additionally surfaces <c>error</c> /
/// <c>stackTrace</c> on the 500 path to aid debugging.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    // Same timestamp contract as MVC responses: Vietnam wall-clock with explicit +07:00
    // (e.g. the OTP cooldown's retryAt) — this serializer bypasses AddJsonOptions.
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new PEMS.Api.Serialization.VietnamDateTimeJsonConverter());
        options.Converters.Add(new PEMS.Api.Serialization.VietnamNullableDateTimeJsonConverter());
        return options;
    }

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        int status;
        object payload;

        switch (ex)
        {
            case ValidationException validation:
                status = StatusCodes.Status400BadRequest;
                payload = new { success = false, message = validation.Message, errors = validation.Errors, traceId };
                _logger.LogInformation("Validation failed: {Message}", validation.Message);
                break;

            case AuthBusinessException authBiz:
                status = authBiz.StatusCode;
                payload = new { success = false, errorCode = authBiz.ErrorCode, message = authBiz.Message, traceId };
                _logger.LogInformation("Auth business failure ({Code}).", authBiz.ErrorCode);
                break;

            case AuthenticationFailedException auth:
                status = StatusCodes.Status401Unauthorized;
                payload = new { success = false, message = auth.Message, traceId };
                _logger.LogInformation("Authentication failed ({Reason}).", auth.InternalReason ?? "n/a");
                break;

            case ForbiddenException forbidden:
                status = StatusCodes.Status403Forbidden;
                payload = new { success = false, message = forbidden.Message, traceId };
                break;

            case NotFoundException notFound:
                status = StatusCodes.Status404NotFound;
                payload = new { success = false, message = notFound.Message, traceId };
                break;

            case OtpChallengeException otp:
                // UC-17 OTP challenge contract: machine-readable errorCode + attempt metadata.
                // The frontend renders from errorCode/metadata — never by parsing the message.
                status = otp.StatusCode;
                payload = new
                {
                    success = false,
                    errorCode = otp.ErrorCode,
                    message = otp.Message,
                    remainingAttempts = otp.RemainingAttempts,
                    retryAfterSeconds = otp.RetryAfterSeconds,
                    retryAt = otp.RetryAt,
                    humanVerificationRequired = otp.HumanVerificationRequired,
                    traceId
                };
                _logger.LogInformation("OTP challenge failure ({Code}).", otp.ErrorCode);
                break;

            case ConflictException conflict:
                status = StatusCodes.Status409Conflict;
                payload = conflict.Data is null
                    ? new { success = false, errorCode = conflict.ErrorCode, message = conflict.Message, traceId }
                    : new { success = false, errorCode = conflict.ErrorCode, message = conflict.Message, data = conflict.Data, traceId };
                break;

            case BusinessRuleException business:
                status = StatusCodes.Status422UnprocessableEntity;
                payload = new { success = false, errorCode = business.ErrorCode, message = business.Message, traceId };
                break;

            case BadHttpRequestException badRequest:
                status = StatusCodes.Status400BadRequest;
                payload = new { success = false, message = "Dữ liệu không hợp lệ: " + badRequest.Message, traceId };
                _logger.LogInformation("BadHttpRequestException: {Message}", badRequest.Message);
                break;


            default:
                status = StatusCodes.Status500InternalServerError;
                _logger.LogError(ex, "Unhandled exception processing {Path} (traceId {TraceId}).", context.Request.Path, traceId);

                // The user-facing `message` is ALWAYS a safe, generic Vietnamese string — even in
                // Development. Raw exception text / inner exception / SQL / stack traces are logged
                // above and, in Development only, attached as SEPARATE diagnostic fields (error,
                // innerError, rootCause, stackTrace) for the network tab. They must NEVER appear in
                // `message`, because the frontend renders `message` to the user.
                const string safeMessage = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.";

                payload = _environment.IsDevelopment()
                    ? (object)new
                    {
                        success = false,
                        errorCode = AuthErrorCodes.InternalServerError,
                        message = safeMessage,
                        traceId,
                        // Developer-only diagnostics (logs / network tab) — never shown in the UI.
                        error = ex.Message,
                        innerError = ex.InnerException?.Message,
                        rootCause = GetRootCauseMessage(ex),
                        stackTrace = ex.StackTrace
                    }
                    : (object)new
                    {
                        success = false,
                        errorCode = AuthErrorCodes.InternalServerError,
                        message = safeMessage,
                        traceId
                    };
                break;
        }

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started; cannot write error body for {Path}.", context.Request.Path);
            return;
        }

        // Note: we deliberately do NOT call Response.Clear() so CORS headers added
        // by the (inner) CORS middleware are preserved on error responses.
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        if (status == StatusCodes.Status429TooManyRequests && ex is OtpChallengeException { RetryAfterSeconds: not null } otpEx)
        {
            context.Response.Headers.Append("Retry-After", otpEx.RetryAfterSeconds.Value.ToString());
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    /// <summary>Walk InnerException chain to find the deepest root cause message.</summary>
    private static string? GetRootCauseMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
            current = current.InnerException;
        return current == ex ? null : current.Message;
    }
}
