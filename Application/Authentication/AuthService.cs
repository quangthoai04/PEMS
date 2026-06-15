using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Infrastructure._Persistence;
using Domain.Entities;

namespace Application.Authentication;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        // 1. Tìm user theo email, load Role để lấy role_code
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Campus)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.DeletedAt == null);

        if (user == null || user.Status != "Active") return null;

        // 2. Kiểm tra mật khẩu
        if (user.PasswordHash != null)
        {
            // VISITOR / Guest: BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;
        }
        else
        {
            // Tài khoản nội bộ SSO: dùng mật khẩu mặc định trong dev
            if (request.Password != "Fpt@12345") return null;
        }

        // 3. Kiểm tra cơ sở (STAFF / DEPT phải khớp campus)
        var roleCode = user.Role.RoleCode;
        if (roleCode != "HO" && roleCode != "ADMIN" && roleCode != "VISITOR")
        {
            var userCampusCode = user.Campus?.CampusCode;
            if (userCampusCode != request.CampusCode)
            {
                throw new Exception(
                    $"Tài khoản này thuộc cơ sở [{userCampusCode}], " +
                    $"không được phép đăng nhập vào cơ sở [{request.CampusCode}]!");
            }
        }

        // 4. Sinh JWT
        var token = GenerateJwtToken(user);
        return new LoginResponse
        {
            Token    = token,
            UserId   = user.UserId,
            Email    = user.Email,
            FullName = user.FullName,
            RoleCode = roleCode,
            CampusCode = user.Campus?.CampusCode
        };
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId),
            new Claim(ClaimTypes.Email,          user.Email),
            new Claim(ClaimTypes.Name,           user.FullName),
            new Claim(ClaimTypes.Role,           user.Role.RoleCode),
            new Claim("CampusCode",              user.Campus?.CampusCode ?? "HO"),
            new Claim("SubRole",                 user.SubRole ?? "")
        };

        var token = new JwtSecurityToken(
            issuer:   jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims:   claims,
            expires:  DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["DurationInMinutes"])),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
