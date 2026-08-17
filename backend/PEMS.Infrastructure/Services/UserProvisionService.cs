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
        string? nationality,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        // Look up the existing account together with its role + status so we can decide
        // whether it may be linked as the visitor. Tracked (not AsNoTracking): a submission may
        // still need to BACKFILL phone/nationality below when the account is missing them.
        var existing = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        if (existing is not null)
        {
            // Provisioning only ever runs for the REGISTRANT (the create service invites operational
            // contacts instead of provisioning them), so a refusal here is reported against the
            // registrant — not with the contact's code, which used to be thrown from this backstop and
            // would have pointed the form at the wrong input.
            EnsureUsableAsVisitor(
                existing.Role.RoleCode,
                existing.Status,
                VisitRequestErrorMessages.RegistrantEmailNotEligible,
                VisitRequestErrorCodes.RegistrantEmailBelongsToInternalAccount);

            // Backfill only what the account is missing — a value the person already has on file
            // (a prior submission, a later self-service profile edit, or a Google login that ran
            // in between) is never overwritten by this snapshot.
            var backfilled = false;
            if (string.IsNullOrWhiteSpace(existing.Phone) && !string.IsNullOrWhiteSpace(phone))
            {
                existing.Phone = phone!.Trim();
                backfilled = true;
            }
            if (string.IsNullOrWhiteSpace(existing.Nationality) && !string.IsNullOrWhiteSpace(nationality))
            {
                existing.Nationality = nationality!.Trim();
                backfilled = true;
            }
            if (backfilled)
            {
                existing.UpdatedAt = utcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }

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
            Nationality = string.IsNullOrWhiteSpace(nationality) ? null : nationality.Trim(),
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

        EnsureUsableAsVisitor(
            existing.RoleCode,
            existing.Status,
            VisitRequestErrorMessages.ContactEmailNotEligible,
            VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount);
    }

    public async Task ValidateRegistrantEmailUsableForPublicFlowAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        var existing = await _db.Users.AsNoTracking()
            .Where(u => u.Email == normalized)
            .Select(u => new { RoleCode = u.Role.RoleCode, u.Status })
            .FirstOrDefaultAsync(cancellationToken);

        // A non-existent email is fine — a VISITOR account is created at the verify step.
        if (existing is null)
            return;

        EnsureUsableAsVisitor(
            existing.RoleCode,
            existing.Status,
            VisitRequestErrorMessages.RegistrantEmailNotEligible,
            VisitRequestErrorCodes.RegistrantEmailBelongsToInternalAccount);
    }

    /// <summary>
    /// Guards the rule that an address may only be linked when it belongs to an ACTIVE VISITOR
    /// account. Throws otherwise; never mutates the account.
    ///
    /// <para>The caller supplies the sentence and the code, because the same check answers two
    /// different questions — "can this be the registrant?" and "can this be a campus's operational
    /// contact?" — and an error that names the wrong field sends the user to the wrong input.</para>
    /// </summary>
    private static void EnsureUsableAsVisitor(
        string roleCode, string status, string notEligibleMessage, string notEligibleCode)
    {
        // Internal account (ADMIN/HO/STAFF/DEPARTMENT/STUDENT) — must not be repurposed. WHICH role
        // owns the address is never revealed. The role test itself comes from
        // OperationalContactEligibility so "which roles are external" is decided in ONE place: the
        // contact workflow asks the same question at four other doors, and two copies of this
        // predicate would eventually disagree about a role added later.
        if (!PEMS.Application.Delegations.Common.OperationalContactEligibility.IsExternalRole(roleCode))
            throw new ConflictException(notEligibleMessage, notEligibleCode);

        // Existing VISITOR must be ACTIVE to be linked. A separate code, because this one IS worth
        // telling apart: the address is the right KIND of account, it has simply been disabled, and
        // the way forward is support rather than a different address.
        if (!string.Equals(status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException(
                VisitRequestErrorMessages.AccountInactive,
                VisitRequestErrorCodes.VisitorAccountInactive);
    }
}
