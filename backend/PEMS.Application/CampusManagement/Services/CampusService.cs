using PEMS.Domain.Entities.Campuses;
using Application.Common.Interfaces;

namespace Application.CampusManagement.Services;

public class CampusService : ICampusService
{
    private readonly ICampusRepository _campusRepository;

    public CampusService(ICampusRepository campusRepository)
    {
        _campusRepository = campusRepository;
    }

    public async Task<IEnumerable<Campus>> GetAllCampusesAsync()
    {
        return await _campusRepository.GetActiveCampusesAsync();
    }
}
