using System.Threading.RateLimiting;
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

// ── Application / Infrastructure services ────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Auth policy options (SSO-first / dual-portal). Bound once and shared. ─────
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
    ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);

// ── Per-campus visit form v2 feature flag (default OFF). ──────────────────────
var perCampusFormV2Options = builder.Configuration
    .GetSection(PEMS.Application.Common.Options.PerCampusFormV2Options.SectionName)
    .Get<PEMS.Application.Common.Options.PerCampusFormV2Options>()
    ?? new PEMS.Application.Common.Options.PerCampusFormV2Options();
builder.Services.AddSingleton(perCampusFormV2Options);

// ── Database (MySQL, database-first — schema is owned by manual SQL, not EF) ──
// Every pooled connection is pinned to time_zone '+07:00' so CURRENT_TIMESTAMP defaults
// and trigger NOW() produce Vietnam wall-clock, per the PEMS persistence policy.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .AddInterceptors(new VietnamTimeZoneConnectionInterceptor()));

// ── Web API ──────────────────────────────────────────────────────────────────
// Timestamps cross the API boundary as ISO 8601 with the explicit +07:00 offset
// (Vietnam wall-clock), never as ambiguous offset-less strings.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new PEMS.Api.Serialization.VietnamDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new PEMS.Api.Serialization.VietnamNullableDateTimeJsonConverter());
    });
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

// ── CORS (origins from config; falls back to common dev origins) ─────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
const string CorsPolicy = "PemsFrontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        if (allowedOrigins is { Length: > 0 })
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                  .AllowAnyHeader().AllowAnyMethod();
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

