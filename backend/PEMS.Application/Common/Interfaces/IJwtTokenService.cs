using PEMS.Domain.Entities.Users;

namespace Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateJwtToken(User user);
}

