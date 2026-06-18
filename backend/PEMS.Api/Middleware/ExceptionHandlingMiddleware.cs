using System.Diagnostics;
using System.Text.Json;
using PEMS.Application.Common.Exceptions;

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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

            case ConflictException conflict:
                status = StatusCodes.Status409Conflict;
                payload = new { success = false, message = conflict.Message, traceId };
                break;

            case BusinessRuleException business:
                status = StatusCodes.Status422UnprocessableEntity;
                payload = new { success = false, message = business.Message, traceId };
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                _logger.LogError(ex, "Unhandled exception processing {Path} (traceId {TraceId}).", context.Request.Path, traceId);

                // Generic, safe message for everyone. Only Development adds raw details.
                const string genericMessage = "An unexpected error occurred. Please try again later.";
                payload = _environment.IsDevelopment()
                    ? new
                    {
                        success = false,
                        errorCode = "INTERNAL_ERROR",
                        message = genericMessage,
                        traceId,
                        error = ex.Message,
                        stackTrace = ex.StackTrace
                    }
                    : new
                    {
                        success = false,
                        errorCode = "INTERNAL_ERROR",
                        message = genericMessage,
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
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
