using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Infrastructure._Persistence;

namespace Application.Campuses;

public class CampusService : ICampusService
{
    private readonly ApplicationDbContext _context;

    public CampusService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Campus>> GetAllCampusesAsync()
    {
        return await _context.Campuses
            .Where(c => c.Status == "active")
            .OrderBy(c => c.CampusCode)
            .ToListAsync();
    }
}
