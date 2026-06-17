using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PEMS.Api.Extensions;
using PEMS.Api.Middleware;
using PEMS.Application;
using PEMS.Infrastructure;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// ── Application / Infrastructure services ────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Database ─────────────────────────────────────────────────────────────────
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

// ── Seeding (Development or Seed:Enabled) — best-effort, never crashes startup ─
var runSeed = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:Enabled");
if (runSeed)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        var includeDevAccounts = app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("Seed:DevAccounts");
        await seeder.SeedAsync(includeDevAccounts);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database seeding skipped/failed (is the database available and migrated?).");
    }
}

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
