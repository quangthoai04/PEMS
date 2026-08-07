using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.ApiIntegrations.Commands.DisconnectGoogleDriveOAuth;

/// <summary>
/// Forgets the stored Drive credential.
///
/// <para>
/// It does NOT call Google's revoke endpoint. Revoking would invalidate the grant for the shared account
/// itself, and the account is used by more than this deployment (a developer's local database holds its own
/// copy of the same token) — so a disconnect here would break machines whose operator did nothing. What
/// this deployment stops using is what this deployment stops storing.
/// </para>
/// <para>
/// The environment fallback still applies afterwards, for as long as the rollout keeps it: disconnecting
/// removes the database credential, which is all it claims to do.
/// </para>
/// </summary>
public sealed class DisconnectGoogleDriveOAuthCommandHandler
    : IRequestHandler<DisconnectGoogleDriveOAuthCommand, ApiIntegrationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public DisconnectGoogleDriveOAuthCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ApiIntegrationDto> Handle(
        DisconnectGoogleDriveOAuthCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        // Single-argument ctor on purpose: NotFoundException(string, string) is the (message, errorCode)
        // overload, so passing the api_code as a second string would silently publish it as an error code.
        var config = await _db.ApiConfigurations.FirstOrDefaultAsync(
                         c => c.ApiCode == GoogleDriveIntegrationConstants.ApiCode && c.DeletedAt == null,
                         cancellationToken)
                     ?? throw new NotFoundException("Chưa có cấu hình Google Drive để ngắt kết nối.");

        var now = _clock.VietnamNow;

        config.CredentialsJsonEncrypted = null;
        // The last verdict described the credential that just went away; leaving it would show a green
        // "SUCCESS" beside "chưa kết nối".
        config.LastTestStatus = null;
        config.LastTestedAt = null;
        config.LastTestMessage = null;
        config.UpdatedAt = now;
        config.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            Action = GoogleDriveIntegrationConstants.AuditDisconnect,
            EntityType = GoogleDriveIntegrationConstants.AuditEntityType,
            EntityId = config.ApiConfigId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return ApiIntegrationMapper.ToDto(config, _currentUser);
    }
}
