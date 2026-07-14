using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationDetail;

public sealed class GetApiIntegrationDetailQueryHandler
    : IRequestHandler<GetApiIntegrationDetailQuery, ApiIntegrationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetApiIntegrationDetailQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ApiIntegrationDto> Handle(
        GetApiIntegrationDetailQuery request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureRead(_currentUser);

        var config = await _db.ApiConfigurations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ApiConfigId == request.ApiConfigId && c.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("ApiConfiguration", request.ApiConfigId);

        return ApiIntegrationMapper.ToDto(config, _currentUser);
    }
}
