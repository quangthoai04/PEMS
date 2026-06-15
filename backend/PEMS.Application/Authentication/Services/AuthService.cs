using Application.Authentication.Dtos;
using Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace Application.Authentication.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetUserByEmailWithDetailsAsync(request.Email);

        if (user == null || user.Status != "Active" || user.DeletedAt != null) return null;

        if (user.PasswordHash != null)
        {
            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash)) return null;
        }
        else
        {
            if (request.Password != "Fpt@12345") return null;
        }

        var roleCode = user.Role.RoleCode;
        if (roleCode != "HO" && roleCode != "ADMIN" && roleCode != "VISITOR")
        {
            var userCampusCode = user.Campus?.CampusCode;
            if (userCampusCode != request.CampusCode)
            {
                throw new System.Exception(
                    $"Tài khoản này thuộc cơ sở [{userCampusCode}], " +
                    $"không được phép đăng nhập vào cơ sở [{request.CampusCode}]!");
            }
        }

        var token = _jwtTokenService.GenerateJwtToken(user);
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
}
