using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.Common.Interfaces;
using PEMS.Domain.Entities.Campuses;
using PEMS.Infrastructure.Persistence;

namespace PEMS.Infrastructure.Persistence.Repositories;

public class CampusRepository : ICampusRepository
{
    private readonly ApplicationDbContext _context;

    public CampusRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Campus>> GetActiveCampusesAsync()
    {
        return await _context.Campuses
            .Where(c => c.Status == "active")
            .OrderBy(c => c.CampusCode)
            .ToListAsync();
    }
}
