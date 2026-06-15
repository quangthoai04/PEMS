using System.Threading.Tasks;
using PEMS.Domain.Entities.Users;

namespace Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByEmailWithDetailsAsync(string email);
}

