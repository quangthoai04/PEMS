using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.Partners.Contacts.Common;

/// <summary>Shared guards for contact mutations (scope check, email dedupe, primary swap).</summary>
public static class PartnerContactWriteSupport
{
    public static async Task<Partner> LoadPartnerForManageAsync(
        IApplicationDbContext db, ICurrentUserService user, ulong partnerId, CancellationToken ct)
    {
        var partner = await db.Partners
            .FirstOrDefaultAsync(p => p.PartnerId == partnerId, ct)
            ?? throw new NotFoundException("Partner", partnerId);

        if (!PartnerAccess.CanManagePartnerChildren(user, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền quản lý người liên hệ của đối tác này.", 403);
        return partner;
    }

    /// <summary>Blocks two contacts of one partner sharing the same (normalized) email.</summary>
    public static async Task EnsureEmailUniqueAsync(
        IApplicationDbContext db, ulong partnerId, string? email, ulong? excludeContactId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var normalized = email.Trim().ToLowerInvariant();
        var duplicated = await db.PartnerContacts.AnyAsync(
            c => c.PartnerId == partnerId
                 && c.Email != null && c.Email.ToLower() == normalized
                 && (excludeContactId == null || c.ContactId != excludeContactId),
            ct);
        if (duplicated)
            throw new ConflictException("Email này đã được dùng cho một người liên hệ khác của đối tác.",
                PartnerErrorCodes.ContactEmailDuplicated);
    }

    /// <summary>One partner keeps exactly one ACTIVE primary contact.</summary>
    public static async Task UnsetOtherPrimariesAsync(
        IApplicationDbContext db, ulong partnerId, ulong? keepContactId, CancellationToken ct)
    {
        var others = await db.PartnerContacts
            .Where(c => c.PartnerId == partnerId && c.IsPrimary
                        && (keepContactId == null || c.ContactId != keepContactId))
            .ToListAsync(ct);
        foreach (var other in others) other.IsPrimary = false;
    }
}
