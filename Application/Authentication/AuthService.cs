using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Infrastructure._Persistence;
using Domain.Entities;

namespace Application.Authentication
{
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
            // 1. Tìm người dùng theo Email trong DbSet Useraccounts
            var user = await _context.Useraccounts
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            // Nếu không tìm thấy hoặc tài khoản bị khóa (IsActive = 0)
            if (user == null || user.IsActive != true) return null;

            // 2. Kiểm tra mật khẩu dựa trên thiết kế seed data của nhóm bạn
            if (user.PasswordHash != null)
            {
                // TÀI KHOẢN GUEST: Kiểm tra khớp mã băm BCrypt
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                if (!isPasswordValid) return null;
            }
            else
            {
                // TÀI KHOẢN SSO NỘI BỘ (HO, Admin, Staff, Student): 
                // Theo kịch bản đồ án, cho phép gõ pass mặc định "Fpt@12345" để bypass login
                if (request.Password != "Fpt@12345") return null;
            }

            // 3. KIỂM TRA ĐỒNG BỘ CƠ SỞ (CAMPUS CHECK)
            // Quy tắc nghiệp vụ: 
            // - Tài khoản HO (Head Office) và Guest (Khách ngoài) có CampusCode = NULL nên được phép bỏ qua.
            // - Tài khoản Admin, Staff, Student bắt buộc CampusCode dưới DB phải trùng khít với nơi chọn trên UI.
            if (user.RoleCode != "HO" && user.RoleCode != "Guest")
            {
                if (user.CampusCode != request.CampusCode)
                {
                    // Quăng lỗi cụ thể để Controller bắt được và trả về thông báo cho UI
                    throw new Exception($"Tài khoản này thuộc cơ sở [{user.CampusCode}], không được phép đăng nhập vào cơ sở [{request.CampusCode}]!");
                }
            }

            // 4. ĐĂNG NHẬP THÀNH CÔNG -> Tiến hành sinh mã JWT Token...
            var token = GenerateJwtToken(user);

            return new LoginResponse
            {
                Token = token,
                UserId = user.UserId.ToString(),
                Email = user.Email,
                FullName = user.FullName,
                RoleCode = user.RoleCode,
                CampusCode = user.CampusCode
            };
        }

        private string GenerateJwtToken(Useraccount user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Nạp thông tin người dùng vào Token (Claims) phục vụ phân quyền ở UI/API sau này
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.RoleCode),
                new Claim("CampusCode", user.CampusCode ?? "HO")
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(jwtSettings["DurationInMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}