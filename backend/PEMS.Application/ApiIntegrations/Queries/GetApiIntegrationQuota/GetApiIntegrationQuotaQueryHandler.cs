using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationQuota;

public sealed class GetApiIntegrationQuotaQueryHandler
    : IRequestHandler<GetApiIntegrationQuotaQuery, List<ApiQuotaDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetApiIntegrationQuotaQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<ApiQuotaDto>> Handle(
        GetApiIntegrationQuotaQuery request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureRead(_currentUser);

        var exists = await _db.ApiConfigurations
            .AnyAsync(c => c.ApiConfigId == request.ApiConfigId && c.DeletedAt == null, cancellationToken);
        if (!exists) throw new NotFoundException("ApiConfiguration", request.ApiConfigId);

        return await _db.ApiUsageQuotas.AsNoTracking()
            .Where(q => q.ApiConfigId == request.ApiConfigId)
            .OrderByDescending(q => q.PeriodYyyymm)
            .ThenBy(q => q.CampusScopeKey)
            .Take(36)
            .Select(q => new ApiQuotaDto
            {
                ApiUsageQuotaId = q.ApiUsageQuotaId,
                ApiConfigId = q.ApiConfigId,
                CampusId = q.CampusId,
                CampusScopeKey = q.CampusScopeKey,
                PeriodYyyymm = q.PeriodYyyymm,
                MonthlyLimit = q.MonthlyLimit,
                UsedCount = q.UsedCount,
                LastUsedAt = q.LastUsedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
