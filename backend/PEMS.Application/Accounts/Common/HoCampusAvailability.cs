using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Single source of truth for "can HO create a new HO account for this campus?". Implements the
/// case matrix of the HO_CREATE_HO_ACCOUNT spec (§6): a campus may have at most ONE HO, so the
/// create is allowed only when the campus has no HO in any status (ACTIVE / INACTIVE / LOCKED).
/// An existing HO blocks with a per-status reason; more than one HO row is flagged as
/// inconsistent data.
///
/// <para>
/// Both the read-only precheck query (served to the create modal) and the write-side
/// <c>CreateAccountCommandHandler</c> use this so the UX hint and the authoritative server check
/// can never diverge. The query maps <see cref="Result"/> to a DTO; the handler calls
/// <see cref="EnsureCanCreate"/> to throw the spec-mandated HTTP error.
/// </para>
/// </summary>
internal static class HoCampusAvailability
{
    public enum Outcome
    {
        CanCreate,
        CampusNotFound,
        CampusInactive,
        HoActive,
        HoInactive,
        HoLocked,
        MultipleHo,
    }

    public sealed record ExistingHo(ulong UserId, string FullName, string Email, string Status);

    public sealed record Result(
        Outcome Kind,
        ulong CampusId,
        string? CampusName,
        ExistingHo? Ho)
    {
        public bool CanCreate => Kind == Outcome.CanCreate;

        /// <summary>Stable machine-readable reason, or null when <see cref="CanCreate"/>.</summary>
        public string? ReasonCode => Kind switch
        {
            Outcome.CanCreate => null,
            Outcome.CampusNotFound => AccountErrorCodes.CampusNotFound,
            Outcome.CampusInactive => AccountErrorCodes.CampusInactive,
            Outcome.HoActive => AccountErrorCodes.CampusHoAlreadyActive,
            Outcome.HoInactive => AccountErrorCodes.CampusHoInactiveExists,
            Outcome.HoLocked => AccountErrorCodes.CampusHoLockedExists,
            Outcome.MultipleHo => AccountErrorCodes.CampusHoDataInconsistent,
            _ => null,
        };

        /// <summary>Safe, user-facing Vietnamese message for the outcome.</summary>
        public string Message => Kind switch
        {
            Outcome.CanCreate => "Cơ sở hợp lệ. Có thể tạo tài khoản HO.",
            Outcome.CampusNotFound => "Cơ sở được chọn không tồn tại.",
            Outcome.CampusInactive => "Cơ sở được chọn đang không hoạt động. Vui lòng kích hoạt cơ sở trước.",
            Outcome.HoActive =>
                "Cơ sở này đã có tài khoản HO đang hoạt động. Không thể tạo thêm HO cho cùng một cơ sở.",
            Outcome.HoInactive =>
                "Cơ sở này đang có tài khoản HO bị vô hiệu hóa. Vui lòng khôi phục tài khoản hiện tại hoặc sử dụng chức năng thay thế HO cơ sở.",
            Outcome.HoLocked =>
                "Cơ sở này đã có tài khoản HO nhưng tài khoản đang bị khóa. Vui lòng xử lý trạng thái khóa hoặc thực hiện luồng thay thế HO sau khi xác minh.",
            Outcome.MultipleHo =>
                "Dữ liệu không nhất quán: cơ sở này đang có nhiều hơn một tài khoản HO. Vui lòng xử lý dữ liệu trước khi tạo tài khoản mới.",
            _ => "Không thể tạo tài khoản HO cho cơ sở này.",
        };
    }

    /// <summary>
    /// Evaluates whether a new HO can be created for <paramref name="campusId"/>. Read-only. The
    /// write-side handler must still re-check inside its transaction to defeat a concurrent create.
    /// </summary>
    public static async Task<Result> ResolveAsync(
        IApplicationDbContext db, ulong campusId, CancellationToken cancellationToken)
    {
        var campus = await db.Campuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampusId == campusId, cancellationToken);
        if (campus is null)
            return new Result(Outcome.CampusNotFound, campusId, null, null);
        if (campus.Status != EntityStatuses.Active)
            return new Result(Outcome.CampusInactive, campusId, campus.Name, null);

        // Every HO on this campus, any status — the campus may have at most one (§3).
        var hos = await db.Users.AsNoTracking()
            .Where(u => u.PrimaryCampusId == campusId && u.Role.RoleCode == RoleCodes.Ho)
            .Select(u => new ExistingHo(u.UserId, u.FullName, u.Email, u.Status))
            .ToListAsync(cancellationToken);

        if (hos.Count == 0)
            return new Result(Outcome.CanCreate, campusId, campus.Name, null);
        if (hos.Count > 1)
            return new Result(Outcome.MultipleHo, campusId, campus.Name, null);

        var ho = hos[0];
        var kind = ho.Status switch
        {
            UserStatuses.Inactive => Outcome.HoInactive,
            UserStatuses.Locked => Outcome.HoLocked,
            _ => Outcome.HoActive, // ACTIVE or any unexpected status → block as "already exists".
        };
        return new Result(kind, campusId, campus.Name, ho);
    }

    /// <summary>
    /// Throws the spec-mandated HTTP error (404 / 422 / 409) carrying the error code and, where
    /// relevant, the existing HO data. No-op when the campus can accept a new HO.
    /// </summary>
    public static void EnsureCanCreate(Result result)
    {
        if (result.CanCreate)
            return;

        switch (result.Kind)
        {
            case Outcome.CampusNotFound:
                throw new NotFoundException(result.Message);

            case Outcome.CampusInactive:
                throw new BusinessRuleException(result.Message, result.ReasonCode!);

            default:
                throw new ConflictException(result.Message, result.ReasonCode!, BuildData(result));
        }
    }

    /// <summary>The <c>data</c> payload surfaced to the client for a 409 (matches spec §6 examples).</summary>
    private static object BuildData(Result result) => result.Ho is null
        ? new { campusId = result.CampusId, campusName = result.CampusName }
        : new
        {
            campusId = result.CampusId,
            campusName = result.CampusName,
            existingHoId = result.Ho.UserId,
            existingHoName = result.Ho.FullName,
            existingHoEmail = result.Ho.Email,
            existingHoStatus = result.Ho.Status,
        };
}
