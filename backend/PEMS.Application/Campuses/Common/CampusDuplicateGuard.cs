using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Enforces the "only city may duplicate" rule (00_COMMON_RULES §4) for UC-81 create and
/// UC-85 update. Comparison is done in memory after normalization (campus rows are few),
/// which keeps phone normalization identical to <see cref="CampusNormalization"/> and
/// avoids fragile EF translation of custom string cleaning. Throws
/// <see cref="ConflictException"/> (409) with a stable error code on the first clash.
/// </summary>
internal static class CampusDuplicateGuard
{
    /// <param name="excludeCampusId">The current campus id on update; null on create.</param>
    public static async Task EnsureUniqueAsync(
        IApplicationDbContext db,
        string normalizedCode,
        string normalizedName,
        string normalizedAddress,
        string normalizedPhone,
        string normalizedEmail,
        ulong? excludeCampusId,
        CancellationToken ct)
    {
        var others = await db.Campuses.AsNoTracking()
            .Where(c => excludeCampusId == null || c.CampusId != excludeCampusId.Value)
            .Select(c => new { c.CampusCode, c.Name, c.Address, c.Phone, c.Email })
            .ToListAsync(ct);

        var nameKey = normalizedName.ToLowerInvariant();
        var addressKey = normalizedAddress.ToLowerInvariant();
        var phoneKey = CampusNormalization.PhoneKey(normalizedPhone);

        foreach (var c in others)
        {
            if (CampusNormalization.Code(c.CampusCode) == normalizedCode)
                throw new ConflictException("Mã campus đã tồn tại.", CampusErrorCodes.CampusCodeAlreadyExists);

            if (CampusNormalization.Text(c.Name).ToLowerInvariant() == nameKey)
                throw new ConflictException("Tên campus đã tồn tại.", CampusErrorCodes.CampusNameAlreadyExists);

            if (!string.IsNullOrWhiteSpace(c.Address)
                && CampusNormalization.Text(c.Address).ToLowerInvariant() == addressKey)
                throw new ConflictException("Địa chỉ này đã được sử dụng cho campus khác.", CampusErrorCodes.CampusAddressAlreadyExists);

            if (!string.IsNullOrWhiteSpace(c.Phone)
                && CampusNormalization.PhoneKey(c.Phone) == phoneKey)
                throw new ConflictException("Số điện thoại này đã được sử dụng cho campus khác.", CampusErrorCodes.CampusPhoneAlreadyExists);

            if (!string.IsNullOrWhiteSpace(c.Email)
                && CampusNormalization.Email(c.Email) == normalizedEmail)
                throw new ConflictException("Email này đã được sử dụng cho campus khác.", CampusErrorCodes.CampusEmailAlreadyExists);
        }
    }
}
