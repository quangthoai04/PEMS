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

var app = builder.Build();

// ── HTTP pipeline ────────────────────────────────────────────────────────────
// Exception handler is outermost so it catches everything; it does not clear
// headers, so CORS headers added downstream survive on error responses.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors(CorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
