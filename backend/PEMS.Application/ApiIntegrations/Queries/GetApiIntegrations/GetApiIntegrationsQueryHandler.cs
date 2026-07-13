using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.ApiIntegrations.Queries.GetApiIntegrations;

public sealed class GetApiIntegrationsQueryHandler
    : IRequestHandler<GetApiIntegrationsQuery, List<ApiIntegrationDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetApiIntegrationsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<ApiIntegrationDto>> Handle(
        GetApiIntegrationsQuery request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureRead(_currentUser);

        var query = _db.ApiConfigurations.AsNoTracking().Where(c => c.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(request.Purpose))
            query = query.Where(c => c.Purpose == request.Purpose);

        var configs = await query.OrderBy(c => c.ApiConfigId).ToListAsync(cancellationToken);
        return configs.Select(c => ApiIntegrationMapper.ToDto(c, _currentUser)).ToList();
    }
}
