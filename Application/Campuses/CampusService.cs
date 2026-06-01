using Application.Campuses;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Infrastructure._Persistence;

namespace Application.Campuses
{
    public class CampusService : ICampusService
    {
        private readonly ApplicationDbContext _context;

        public CampusService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Fptcampus>> GetAllCampusesAsync()
        {
            return await _context.Fptcampuses.ToListAsync();
        }
    }
}