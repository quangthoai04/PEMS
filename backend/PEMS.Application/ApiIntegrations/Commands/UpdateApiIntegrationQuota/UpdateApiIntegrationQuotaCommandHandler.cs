using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.ApiIntegrations.Commands.UpdateApiIntegrationQuota;

public sealed class UpdateApiIntegrationQuotaCommandHandler
    : IRequestHandler<UpdateApiIntegrationQuotaCommand, ApiQuotaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdateApiIntegrationQuotaCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ApiQuotaDto> Handle(
        UpdateApiIntegrationQuotaCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        var config = await _db.ApiConfigurations
            .FirstOrDefaultAsync(c => c.ApiConfigId == request.ApiConfigId && c.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("ApiConfiguration", request.ApiConfigId);

        var now = _clock.VietnamNow;
        var period = now.ToString("yyyyMM");

        var quota = await _db.ApiUsageQuotas.FirstOrDefaultAsync(
            q => q.ApiConfigId == config.ApiConfigId
                 && q.CampusScopeKey == "GLOBAL"
                 && q.PeriodYyyymm == period,
            cancellationToken);

        if (quota is null)
        {
            quota = new ApiUsageQuota
            {
                ApiConfigId = config.ApiConfigId,
                CampusId = null,
                CampusScopeKey = "GLOBAL",
                PeriodYyyymm = period,
                UsedCount = 0,
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            };
            _db.ApiUsageQuotas.Add(quota);
        }

        quota.MonthlyLimit = request.MonthlyLimit;
        quota.UpdatedAt = now;
        quota.UpdatedBy = _currentUser.UserId;

        // Keep the config's default quota in sync for future periods.
        config.MonthlyQuota = (uint)request.MonthlyLimit;
        config.UpdatedAt = now;
        config.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            Action = "UPDATE_API_QUOTA",
            EntityType = "ApiUsageQuota",
            EntityId = quota.ApiUsageQuotaId == 0 ? null : quota.ApiUsageQuotaId,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new ApiQuotaDto
        {
            ApiUsageQuotaId = quota.ApiUsageQuotaId,
            ApiConfigId = quota.ApiConfigId,
            CampusId = quota.CampusId,
            CampusScopeKey = quota.CampusScopeKey,
            PeriodYyyymm = quota.PeriodYyyymm,
            MonthlyLimit = quota.MonthlyLimit,
            UsedCount = quota.UsedCount,
            LastUsedAt = quota.LastUsedAt,
        };
    }
}
