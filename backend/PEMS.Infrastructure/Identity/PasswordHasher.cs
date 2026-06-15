using Application.Common.Interfaces;

namespace PEMS.Infrastructure.Identity;

public class PasswordHasher : IPasswordHasher
{
    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
