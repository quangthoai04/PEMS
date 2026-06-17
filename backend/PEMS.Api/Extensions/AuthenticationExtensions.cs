using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PEMS.Application.Common.Security;

namespace PEMS.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>Configures JWT bearer authentication from the JwtSettings section.</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("JwtSettings");
        var secretKey = jwt["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        if (ctx.Response.HasStarted) return;
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"message\":\"Authentication required.\"}");
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

        return services;
    }
}
