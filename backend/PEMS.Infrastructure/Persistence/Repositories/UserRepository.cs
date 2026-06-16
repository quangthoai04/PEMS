using Microsoft.EntityFrameworkCore;
using Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;

namespace PEMS.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByEmailWithDetailsAsync(string email)
    {
        return await _context.Users
            .Include(u => u.Role)
            .Include(u => u.PrimaryCampus)
            .FirstOrDefaultAsync(u => u.Email == email);
    }
}
