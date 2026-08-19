using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PEMS.Api.Authentication;
using PEMS.Application.Common.Security;

namespace PEMS.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// <see cref="HttpContext.Items"/> key <see cref="JwtBearerEvents.OnAuthenticationFailed"/> uses
    /// to hand its exception-derived errorCode to <see cref="JwtBearerEvents.OnChallenge"/> — the two
    /// events run on the same request but only OnAuthenticationFailed sees WHY validation failed.
    /// </summary>
    private const string AuthChallengeErrorCodeKey = "PEMS.AuthChallengeErrorCode";

    /// <summary>
    /// Configures JWT bearer authentication from the JwtSettings section. When (and only when) the fail-closed
    /// E2E gate is fully open (Testing env + explicit flag + secret + profile file — see
    /// <see cref="E2ETestAuthGate.IsEnabledFor"/>), it ALSO registers the server-side-profile test scheme and
    /// makes it the default authenticate/challenge scheme for the run. In Development/Production the gate is
    /// closed, so JWT stays the only scheme and the test handler/profile store are never registered.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var jwt = configuration.GetSection("JwtSettings");
        var secretKey = jwt["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

        var e2eEnabled = E2ETestAuthGate.IsEnabledFor(environment.EnvironmentName);
        var defaultScheme = e2eEnabled ? E2ETestAuthGate.SchemeName : JwtBearerDefaults.AuthenticationScheme;

        var authBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = defaultScheme;
                options.DefaultChallengeScheme = defaultScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwt["Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = PemsClaimTypes.UserId
                };

                // Return a JSON body for 401 / 403 instead of an empty response.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/api/files"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    // Records WHY authentication failed so OnChallenge can pick a stable errorCode
                    // instead of one generic message for every reason — an expired token and a
                    // missing/malformed one are different facts and the frontend already has
                    // separate localized copy for each (errors:api.TOKEN_EXPIRED vs UNAUTHORIZED).
                    OnAuthenticationFailed = ctx =>
                    {
                        if (ctx.Exception is SecurityTokenExpiredException)
                            ctx.HttpContext.Items[AuthChallengeErrorCodeKey] = AuthErrorCodes.TokenExpired;
                        return Task.CompletedTask;
                    },
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        if (ctx.Response.HasStarted) return;
                        var errorCode = ctx.HttpContext.Items[AuthChallengeErrorCodeKey] as string
                            ?? AuthErrorCodes.Unauthorized;
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            success = false,
                            errorCode,
                            message = "Authentication required.",
                        }));
                    },
                    OnForbidden = async ctx =>
                    {
                        if (ctx.Response.HasStarted) return;
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"message\":\"You do not have permission to perform this action.\"}");
                    }
                };
            });

        // Fail-closed: the test scheme + its server-side profile store exist ONLY when the gate is open.
        if (e2eEnabled)
        {
            services.AddSingleton<E2ETestProfileStore>();
            authBuilder.AddScheme<AuthenticationSchemeOptions, E2ETestAuthHandler>(E2ETestAuthGate.SchemeName, _ => { });
        }

        return services;
    }
}
