using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PEMS.Api.Extensions;
using PEMS.Api.Middleware;
using PEMS.Application;
using PEMS.Application.Common.Security;
using PEMS.Infrastructure;
using PEMS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── Listen address/port ────────────────────────────────────────────────────
// Railway injects PORT and expects the container to bind 0.0.0.0:$PORT (no
// fixed default — it's assigned per-deployment). Only override the URLs when
// PORT is actually set, so local `dotnet run` / Visual Studio keeps using
// launchSettings.json (http://localhost:5265) unchanged.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ── Application / Infrastructure services ────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Forwarded headers (Railway terminates TLS at its edge proxy and forwards
// plain HTTP to this container). Without this, UseHttpsRedirection()/UseHsts()
// below would see every request as HTTP and force a redirect loop. Railway's
// proxy IP isn't a fixed/known address, so KnownProxies/KnownNetworks are
// cleared to accept the forwarded headers from any upstream (safe here since
// the container is not directly reachable from the internet — Railway's edge
// is the only path in).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Auth policy options (SSO-first / dual-portal). Bound once and shared. ─────
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
    ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);

// ── Database (MySQL, database-first — schema is owned by manual SQL, not EF) ──
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ── Web API ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PEMS API", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token (without the 'Bearer' prefix).",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwtScheme] = Array.Empty<string>() });
    options.CustomSchemaIds(type => type.FullName);
});

// ── AuthN / AuthZ ────────────────────────────────────────────────────────────
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAppAuthorization();

// ── CORS ─────────────────────────────────────────────────────────────────────
// Origins come from config (Cors:AllowedOrigins), which Railway overrides via
// the Cors__AllowedOrigins__0 (etc.) environment variable. The localhost
// fallback below only applies in Development — Production must never silently
// accept localhost if the operator forgets to set the env var.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
const string CorsPolicy = "PemsFrontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        if (allowedOrigins is { Length: > 0 })
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else if (builder.Environment.IsDevelopment())
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                  .AllowAnyHeader().AllowAnyMethod();
        // Production with no configured origins: no origins are allowed
        // (browsers will block cross-origin calls) rather than falling back
        // to an insecure default.
    });
});

// ── Rate limiting ────────────────────────────────────────────────────────────
// Scoped policy applied ONLY to the account list/search endpoints (UC-95/UC-99)
// via [EnableRateLimiting("accounts-read")]. Endpoints without the attribute are
// untouched, so this has no blast radius on the rest of the system. Per-user fixed
// window: ADMIN/HO 60 req/min, other roles 30 req/min.
const string AccountsReadRateLimit = "accounts-read";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(AccountsReadRateLimit, httpContext =>
    {
        var roleCode = httpContext.User.FindFirst(PemsClaimTypes.RoleCode)?.Value;
        var permitPerMinute = roleCode is "ADMIN" or "HO" ? 60 : 30;

        var partitionKey = httpContext.User.Identity?.Name
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.OnRejected = async (context, token) =>
    {
        var response = context.HttpContext.Response;
        if (response.HasStarted) return;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        await response.WriteAsync(
            "{\"success\":false,\"errorCode\":\"RATE_LIMIT_EXCEEDED\",\"message\":\"Bạn thao tác quá nhanh. Vui lòng thử lại sau.\"}",
            token);
    };
});

var app = builder.Build();

// ── HTTP pipeline ────────────────────────────────────────────────────────────
// Must run before anything that inspects scheme/remote IP (HSTS, HTTPS
// redirection, auth, rate limiting) so they see the original client info
// instead of the proxy's.
app.UseForwardedHeaders();

// Exception handler is outermost so it catches everything; it does not clear
// headers, so CORS headers added downstream survive on error responses.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Baseline security headers on every response (safe for the JSON API + SPA).
app.UseMiddleware<SecurityHeadersMiddleware>();

// HSTS only outside Development so local HTTP dev keeps working. Requires that
// production is served fully over HTTPS (it is, behind the reverse proxy).
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors(CorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

// Enforces the named rate-limit policies declared above (only on annotated endpoints).
app.UseRateLimiter();

app.MapControllers();

app.Run();

