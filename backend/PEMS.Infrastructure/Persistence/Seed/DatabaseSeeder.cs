using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seeding of RBAC permissions, the role→permission matrix and (in dev)
/// test accounts. Assumes the schema from <c>database/scripts/pems_full.sql</c>
/// (incl. roles/campuses/departments) is already applied. Safe to run repeatedly.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeService _clock;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IDateTimeService clock,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _logger = logger;
    }

    public async Task SeedAsync(bool includeDevAccounts, CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);

        if (includeDevAccounts)
            await SeedDevAccountsAsync(cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _db.Permissions
            .Select(p => p.PermissionCode)
            .ToListAsync(cancellationToken);
        var existing = existingCodes.ToHashSet();

        var added = 0;
        foreach (var def in PermissionSeed.All)
        {
            if (existing.Contains(def.Code))
                continue;

            _db.Permissions.Add(new Permission
            {
                PermissionId = Guid.NewGuid().ToString(),
                PermissionCode = def.Code,
                Name = def.Name,
                PermissionGroup = def.Group,
                IsSystem = def.IsSystem,
                CreatedAt = _clock.UtcNow
            });
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} permissions.", added);
        }
    }

    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var roles = await _db.Roles
            .Where(r => r.DeletedAt == null)
            .ToDictionaryAsync(r => r.RoleCode, r => r.RoleId, cancellationToken);

        var permissions = await _db.Permissions
            .ToDictionaryAsync(p => p.PermissionCode, p => p.PermissionId, cancellationToken);

        var existing = (await _db.RolePermissions
                .Select(rp => new { rp.RoleId, rp.PermissionId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        var added = 0;
        foreach (var grant in PermissionMatrixSeed.Build())
        {
            if (!roles.TryGetValue(grant.RoleCode, out var roleId)) continue;
            if (!permissions.TryGetValue(grant.PermissionCode, out var permissionId)) continue;
            if (existing.Contains((roleId, permissionId))) continue;

            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
                PermissionLevel = grant.Level,
                GrantedAt = _clock.UtcNow
            });
            existing.Add((roleId, permissionId));
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} role-permission grants.", added);
        }
    }

    private async Task SeedDevAccountsAsync(CancellationToken cancellationToken)
    {
        var roles = await _db.Roles
            .Where(r => r.DeletedAt == null)
            .ToDictionaryAsync(r => r.RoleCode, r => r.RoleId, cancellationToken);

        foreach (var def in DevAccountSeed.All)
        {
            var email = def.Email.Trim().ToLowerInvariant();
            var exists = await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);
            if (exists) continue;

            if (!roles.TryGetValue(def.RoleCode, out var roleId))
            {
                _logger.LogWarning("Skipping dev account {Email}: role {Role} not found.", email, def.RoleCode);
                continue;
            }

            var isVisitor = def.RoleCode == RoleCodes.Visitor;

            string? campusId = null;
            string? departmentId = null;

            if (!isVisitor)
            {
                campusId = await _db.Campuses
                    .Where(c => c.CampusCode == def.CampusCode)
                    .Select(c => c.CampusId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (campusId is null)
                {
                    _logger.LogWarning("Skipping dev account {Email}: campus {Campus} not found.", email, def.CampusCode);
                    continue;
                }

                if (def.DepartmentCode is not null)
                {
                    departmentId = await _db.Departments
                        .Where(d => d.CampusId == campusId && d.DepartmentCode == def.DepartmentCode)
                        .Select(d => d.DepartmentId)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (departmentId is null)
                    {
                        _logger.LogWarning("Skipping dev account {Email}: department {Dept} not found.", email, def.DepartmentCode);
                        continue;
                    }
                }
            }

            var user = new User
            {
                UserId = Guid.NewGuid().ToString(),
                FullName = def.FullName,
                Email = email,
                PasswordHash = _passwordHasher.Hash(def.Password),
                RoleId = roleId,
                SubRole = def.SubRole,
                PrimaryCampusId = campusId,
                DepartmentId = departmentId,
                Status = UserStatuses.Active,
                EmailVerifiedAt = _clock.UtcNow,
                MustSetPassword = false,
                MustChangePassword = false,
                CreatedVia = CreatedViaValues.AdminCreated,
                CreatedAt = _clock.UtcNow
            };

            _db.Users.Add(user);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to seed dev account {Email}.", email);
                _db.Users.Remove(user);
                continue;
            }

            _db.UserAuthProviders.Add(new UserAuthProvider
            {
                AuthProviderId = Guid.NewGuid().ToString(),
                UserId = user.UserId,
                ProviderType = ProviderTypes.LocalPassword,
                ProviderEmail = email,
                IsEnabled = true,
                LinkedAt = _clock.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded dev account {Email} ({Role}).", email, def.RoleCode);
        }
    }
}
