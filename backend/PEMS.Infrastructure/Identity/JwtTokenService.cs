using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AccessTokenResult GenerateAccessToken(User user, string sessionId, string loginPortal)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");

        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

        var durationMinutes = double.TryParse(jwtSettings["AccessTokenMinutes"], out var m)
            ? m
            : double.TryParse(jwtSettings["DurationInMinutes"], out var legacy) ? legacy : 60d;

        var expiresAt = DateTime.UtcNow.AddMinutes(durationMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(PemsClaimTypes.UserId, user.UserId),
            new(PemsClaimTypes.Email, user.Email),
            new(PemsClaimTypes.RoleId, user.RoleId),
            new(PemsClaimTypes.RoleCode, user.Role?.RoleCode ?? string.Empty),
            new(PemsClaimTypes.SessionId, sessionId),
            new(PemsClaimTypes.LoginPortal, loginPortal),
            // Standard role claim so [Authorize(Roles=...)] keeps working.
            new(ClaimTypes.Role, user.Role?.RoleCode ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName)
        };

        if (!string.IsNullOrEmpty(user.SubRole))
            claims.Add(new Claim(PemsClaimTypes.SubRole, user.SubRole));
        if (!string.IsNullOrEmpty(user.PrimaryCampusId))
            claims.Add(new Claim(PemsClaimTypes.PrimaryCampusId, user.PrimaryCampusId));
        if (!string.IsNullOrEmpty(user.DepartmentId))
            claims.Add(new Claim(PemsClaimTypes.DepartmentId, user.DepartmentId));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(tokenString, expiresAt);
    }
}
