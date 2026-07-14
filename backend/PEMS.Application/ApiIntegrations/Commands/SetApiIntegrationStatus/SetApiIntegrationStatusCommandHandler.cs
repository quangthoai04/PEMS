using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.ApiIntegrations.Commands.SetApiIntegrationStatus;

public sealed class SetApiIntegrationStatusCommandHandler
    : IRequestHandler<SetApiIntegrationStatusCommand, ApiIntegrationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public SetApiIntegrationStatusCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ApiIntegrationDto> Handle(
        SetApiIntegrationStatusCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        var config = await _db.ApiConfigurations
            .FirstOrDefaultAsync(c => c.ApiConfigId == request.ApiConfigId && c.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("ApiConfiguration", request.ApiConfigId);

        if (request.Enable && config.LastTestStatus != "SUCCESS")
            throw new BusinessRuleException(
                "Không thể kích hoạt cấu hình khi chưa test kết nối thành công.",
                ApiIntegrationErrorCodes.TestRequiredBeforeEnable);

        var now = _clock.VietnamNow;
        config.Status = request.Enable ? ApiIntegrationStatuses.Active : ApiIntegrationStatuses.Inactive;
        config.UpdatedAt = now;
        config.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            Action = request.Enable ? "ENABLE_API_CONFIGURATION" : "DISABLE_API_CONFIGURATION",
            EntityType = "ApiConfiguration",
            EntityId = config.ApiConfigId,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return ApiIntegrationMapper.ToDto(config, _currentUser);
    }
}
