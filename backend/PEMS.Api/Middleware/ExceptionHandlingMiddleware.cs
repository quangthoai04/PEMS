using System.Text.Json;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Api.Middleware;

/// <summary>
/// Converts known application exceptions into safe HTTP responses. Never leaks
/// stack traces or internal reasons to the client.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        int status;
        object payload;

        switch (ex)
        {
            case ValidationException validation:
                status = StatusCodes.Status400BadRequest;
                payload = new { message = validation.Message, errors = validation.Errors };
                _logger.LogInformation("Validation failed: {Message}", validation.Message);
                break;

            case AuthenticationFailedException auth:
                status = StatusCodes.Status401Unauthorized;
                payload = new { message = auth.Message };
                _logger.LogInformation("Authentication failed ({Reason}).", auth.InternalReason ?? "n/a");
                break;

            case ForbiddenException forbidden:
                status = StatusCodes.Status403Forbidden;
                payload = new { message = forbidden.Message };
                break;

            case NotFoundException notFound:
                status = StatusCodes.Status404NotFound;
                payload = new { message = notFound.Message };
                break;

            case ConflictException conflict:
                status = StatusCodes.Status409Conflict;
                payload = new { message = conflict.Message };
                break;

            case BusinessRuleException business:
                status = StatusCodes.Status422UnprocessableEntity;
                payload = new { message = business.Message };
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                payload = new { message = "An unexpected error occurred. Please try again later." };
                _logger.LogError(ex, "Unhandled exception processing {Path}.", context.Request.Path);
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
