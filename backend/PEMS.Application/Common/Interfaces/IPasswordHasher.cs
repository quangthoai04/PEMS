namespace Application.Common.Interfaces;

public interface IPasswordHasher
{
    bool VerifyPassword(string password, string hash);
}

