using PEMS.Domain.Entities.Campuses;

namespace Application.CampusManagement.Services;

public interface ICampusService
{
    Task<IEnumerable<Campus>> GetAllCampusesAsync();
}
