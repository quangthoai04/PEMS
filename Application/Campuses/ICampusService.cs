using Domain.Entities;

namespace Application.Campuses;

public interface ICampusService
{
    Task<IEnumerable<Campus>> GetAllCampusesAsync();
}
