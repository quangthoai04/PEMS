namespace PEMS.Api.Middleware;

/// <summary>
/// Adds baseline security response headers to every response. PEMS exposes a JSON
/// API (the SPA is served separately), so these headers are safe and do not affect
/// the success contract.
///
/// HSTS is intentionally NOT set here — it is enabled via <c>app.UseHsts()</c> in
/// production only (see <c>Program.cs</c>) so local HTTP dev is not broken.
/// A strict Content-Security-Policy is applied only to non-Swagger paths outside
/// Development, because the Swagger UI relies on inline scripts/styles.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Set headers just before the response is sent so they survive short-circuits.
        context.Response.OnStarting(() =>
        {
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            // Strong CSP for a pure-JSON API. Skipped for Swagger UI and in Development
            // so the interactive docs keep working. The public email-action pages are the one
            // route that renders real HTML with an inline-styled <form method="post">, so the
            // strict JSON-API policy (which forbids all styles and all form submission) would
            // silently break their Accept/Decline buttons in Production — give that one route
            // prefix a narrowly relaxed policy instead of loosening the policy everywhere.
            var isSwagger = context.Request.Path.StartsWithSegments("/swagger");
            var isPublicEmailAction = context.Request.Path.StartsWithSegments("/api/public/email-actions");
            if (!_environment.IsDevelopment() && !isSwagger)
            {
                headers["Content-Security-Policy"] = isPublicEmailAction
                    ? "default-src 'none'; script-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'"
                    : "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
