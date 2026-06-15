using System.Collections.Generic;
using System.Threading.Tasks;
using PEMS.Domain.Entities.Campuses;

namespace Application.Common.Interfaces;

public interface ICampusRepository
{
    Task<IEnumerable<Campus>> GetActiveCampusesAsync();
}
