using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Auto-provisions a Visitor account on first visit-request submission.
/// Idempotent: if the email already exists the existing userId is returned.
/// </summary>
public sealed class UserProvisionService : IUserProvisionService
{
    private readonly IApplicationDbContext _db;

    public UserProvisionService(IApplicationDbContext db) => _db = db;

    public async Task<string> EnsureVisitorAccountAsync(
        string email,
        string fullName,
        string? phone,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        // Return existing account without modification
        var existing = await _db.Users.AsNoTracking()
            .Where(u => u.Email == normalized)
            .Select(u => u.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
            return existing;

        // Look up the Visitor role
        var role = await _db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Visitor, cancellationToken)
            ?? throw new InvalidOperationException(
                "Visitor role not found. Ensure the database is seeded with role code 'VISITOR'.");

        var newUser = new User
        {
            UserId      = Guid.NewGuid().ToString(),
            FullName    = fullName.Trim(),
            Email       = normalized,
            Phone       = phone?.Trim(),
            RoleId      = role.RoleId,
            Status      = UserStatuses.Active,
            CreatedVia  = CreatedViaValues.VisitorForm,
            CreatedAt   = utcNow,
            // No password — user authenticates via Google SSO
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync(cancellationToken);

        return newUser.UserId;
    }
}
