using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

public sealed class UpdateAccountRoleCommandHandler : IRequestHandler<UpdateAccountRoleCommand, UpdateAccountRoleResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessionService;
    private readonly IDateTimeService _clock;

    public UpdateAccountRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _clock = clock;
    }

    public async Task<UpdateAccountRoleResponse> Handle(UpdateAccountRoleCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUser.UserId;
        var actorCampus = _currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUser.RoleCode);

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        // Staff Leaders may only manage users already within their own campus.
        // (Visitors have no campus, so they can be promoted into the leader's campus.)
        if (!privileged)
        {
            if (actorCampus is null)
                throw new ForbiddenException("Your account is not assigned to a campus and cannot manage accounts.");
            if (user.PrimaryCampusId is not null && user.PrimaryCampusId != actorCampus)
                throw new ForbiddenException("You can only manage accounts within your own campus.");
        }

        var oldRoleCode = user.Role?.RoleCode ?? "UNKNOWN";
        var oldValues = JsonSerializer.Serialize(new
        {
            roleCode = oldRoleCode,
            subRole = user.SubRole,
            campusId = user.PrimaryCampusId,
            departmentId = user.DepartmentId,
        });

        var shape = await AccountProvisioningRules.ResolveAsync(
            _db, request.NewRoleCode, request.SubRole, request.PrimaryCampusId, request.DepartmentId,
            privileged, actorCampus, cancellationToken);

        var now = _clock.UtcNow;
        user.RoleId = shape.RoleId;
        user.SubRole = shape.SubRole;
        user.DepartmentId = shape.DepartmentId;
        user.PrimaryCampusId = shape.PrimaryCampusId;
        user.UpdatedAt = now;
        user.UpdatedBy = actorId;
        // Existing GOOGLE_SSO / FEID / LOCAL_PASSWORD providers are intentionally kept.

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = shape.PrimaryCampusId ?? actorCampus,
            Action = "UPDATE_ACCOUNT_ROLE",
            EntityType = "User",
            EntityId = user.UserId,
            OldValuesJson = oldValues,
            NewValuesJson = JsonSerializer.Serialize(new
            {
                roleCode = shape.RoleCode,
                subRole = shape.SubRole,
                campusId = shape.PrimaryCampusId,
                departmentId = shape.DepartmentId,
            }),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Revoke active sessions so the user must re-authenticate with the new role.
        var revoked = await _sessionService.RevokeAllActiveSessionsAsync(
            user.UserId, SessionRevokeReasons.RoleChanged, actorId, cancellationToken);

        return new UpdateAccountRoleResponse
        {
            UserId = user.UserId,
            RoleCode = shape.RoleCode,
            PrimaryCampusId = shape.PrimaryCampusId,
            RevokedSessions = revoked,
        };
    }
}
