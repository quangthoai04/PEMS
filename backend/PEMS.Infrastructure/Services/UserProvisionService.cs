using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Auto-provisions a Visitor account on first visit-request submission.
/// Idempotent for VISITOR accounts: if an ACTIVE VISITOR already exists for the email
/// its userId is returned. A non-VISITOR (internal) account is never repurposed — the
/// submit is rejected instead.
/// </summary>
public sealed class UserProvisionService : IUserProvisionService
{
    private readonly IApplicationDbContext _db;

    public UserProvisionService(IApplicationDbContext db) => _db = db;

    public async Task<ulong> EnsureVisitorAccountAsync(
        string email,
        string fullName,
        string? phone,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        // Look up the existing account together with its role + status so we can decide
        // whether it may be linked as the visitor — never modifying it here.
        var existing = await _db.Users.AsNoTracking()
            .Where(u => u.Email == normalized)
            .Select(u => new { u.UserId, RoleCode = u.Role.RoleCode, u.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            EnsureExistingAccountUsableAsVisitor(existing.RoleCode, existing.Status);
            return existing.UserId;
        }

        // Look up the Visitor role
        var role = await _db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Visitor, cancellationToken)
            ?? throw new InvalidOperationException(
                "Visitor role not found. Ensure the database is seeded with role code 'VISITOR'.");

        var newUser = new User
        {
            // UserId is DB-generated (BIGINT AUTO_INCREMENT).
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

    public async Task ValidateContactEmailCanBeUsedForVisitorAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        var existing = await _db.Users.AsNoTracking()
            .Where(u => u.Email == normalized)
            .Select(u => new { RoleCode = u.Role.RoleCode, u.Status })
            .FirstOrDefaultAsync(cancellationToken);

        // A non-existent email is fine — it will be created as a VISITOR at the verify step.
        if (existing is null)
            return;

        EnsureExistingAccountUsableAsVisitor(existing.RoleCode, existing.Status);
    }

    /// <summary>
    /// Guards the rule that a contact email may only be linked when it belongs to an ACTIVE
    /// VISITOR account. Throws otherwise; never mutates the account.
    /// </summary>
    private static void EnsureExistingAccountUsableAsVisitor(string roleCode, string status)
    {
        // Internal account (ADMIN/HO/STAFF/DEPARTMENT/STUDENT) — must not be repurposed.
        if (!string.Equals(roleCode, RoleCodes.Visitor, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(
                "Email đầu mối liên hệ không thể dùng để tạo tài khoản VISITOR. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ.",
                VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount);

        // Existing VISITOR must be ACTIVE to be linked.
        if (!string.Equals(status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException(
                "Tài khoản VISITOR tương ứng với email này hiện không hoạt động. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ.",
                VisitRequestErrorCodes.VisitorAccountInactive);
    }
}
