using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the EF Core context exposing only the sets the Application
/// layer needs. Implemented by the Infrastructure <c>ApplicationDbContext</c>.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserAuthProvider> UserAuthProviders { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<OtpToken> OtpTokens { get; }
    DbSet<LoginLog> LoginLogs { get; }
    DbSet<SecurityEvent> SecurityEvents { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Campus> Campuses { get; }
    DbSet<Department> Departments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
