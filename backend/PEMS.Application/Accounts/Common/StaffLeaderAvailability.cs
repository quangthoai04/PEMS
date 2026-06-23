using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Single source of truth for "can HO create a Staff Leader (Trưởng phòng IC) for this
/// campus?". Implements the full case matrix of the HO_CREATE_STAFF_LEADER spec (§8, §9, §10):
/// it resolves the campus + its single ACTIVE IC department, then inspects
/// <c>campuses.ic_head_user_id</c> and <c>departments.head_user_id</c> to detect an existing
/// leader (ACTIVE / INACTIVE / LOCKED), a reference mismatch, a partial reference, or an
/// unlinked Staff Leader.
///
/// <para>
/// Both the read-only availability query (UC-96 pre-check, served to the create modal) and the
/// write-side <c>CreateAccountCommandHandler</c> use this so the UX hint and the authoritative
/// server check can never diverge. The query maps <see cref="Result"/> to a DTO; the handler
/// calls <see cref="EnsureCanCreate"/> to throw the spec-mandated HTTP error.
/// </para>
/// </summary>
internal static class StaffLeaderAvailability
{
    /// <summary>The mutually-exclusive outcomes of the campus availability check.</summary>
    public enum Outcome
    {
        CanCreate,
        CampusNotFound,
        CampusInactive,
        NoActiveIcDepartment,
        MultipleActiveIcDepartments,
        LeaderActive,
        LeaderInactive,
        LeaderLocked,
        ReferenceMismatch,
        PartialReference,
        UnlinkedStaffLeader,
    }

    public sealed record ExistingLeader(ulong UserId, string FullName, string Email, string Status);

    public sealed record Result(
        Outcome Kind,
        ulong CampusId,
        string? CampusName,
        ulong? IcDepartmentId,
        string? IcDepartmentName,
        ExistingLeader? Leader)
    {
        public bool CanCreate => Kind == Outcome.CanCreate;

        /// <summary>Stable machine-readable blocking reason, or null when <see cref="CanCreate"/>.</summary>
        public string? BlockingReason => Kind switch
        {
            Outcome.CanCreate => null,
            Outcome.CampusNotFound => AccountErrorCodes.CampusNotFound,
            Outcome.CampusInactive => AccountErrorCodes.CampusInactive,
            Outcome.NoActiveIcDepartment => AccountErrorCodes.IcDepartmentMissing,
            Outcome.MultipleActiveIcDepartments => AccountErrorCodes.IcDepartmentMultiple,
            Outcome.LeaderActive => AccountErrorCodes.StaffLeaderAlreadyExistsActive,
            Outcome.LeaderInactive => AccountErrorCodes.StaffLeaderExistsInactive,
            Outcome.LeaderLocked => AccountErrorCodes.StaffLeaderExistsLocked,
            Outcome.ReferenceMismatch => AccountErrorCodes.IcLeaderReferenceMismatch,
            Outcome.PartialReference => AccountErrorCodes.IcLeaderPartialReference,
            Outcome.UnlinkedStaffLeader => AccountErrorCodes.UnlinkedStaffLeaderExists,
            _ => null,
        };

        /// <summary>Safe, user-facing Vietnamese message for the outcome.</summary>
        public string Message => Kind switch
        {
            Outcome.CanCreate => "Cơ sở này chưa có Trưởng phòng IC. Có thể tạo Staff Leader mới.",
            Outcome.CampusNotFound => "Cơ sở được chọn không tồn tại.",
            Outcome.CampusInactive => "Cơ sở được chọn đang không hoạt động. Vui lòng kích hoạt cơ sở trước.",
            Outcome.NoActiveIcDepartment =>
                "Cơ sở được chọn chưa có Phòng Hợp tác Quốc tế đang hoạt động. Vui lòng tạo hoặc kích hoạt phòng IC trước.",
            Outcome.MultipleActiveIcDepartments =>
                "Dữ liệu cơ sở không hợp lệ: có nhiều hơn một Phòng Hợp tác Quốc tế đang hoạt động. Vui lòng kiểm tra lại Quản lý phòng ban.",
            Outcome.LeaderActive =>
                "Cơ sở này đã có Trưởng phòng Hợp tác Quốc tế đang hoạt động. Vui lòng sử dụng chức năng thay thế Staff Leader nếu muốn đổi người phụ trách.",
            Outcome.LeaderInactive =>
                "Cơ sở này đang có Trưởng phòng IC bị vô hiệu hóa. Vui lòng khôi phục tài khoản cũ hoặc sử dụng chức năng thay thế Staff Leader.",
            Outcome.LeaderLocked =>
                "Cơ sở này đã có Trưởng phòng IC nhưng tài khoản đang bị khóa. Vui lòng xử lý trạng thái khóa hoặc sử dụng chức năng thay thế Staff Leader sau khi xác nhận.",
            Outcome.ReferenceMismatch =>
                "Dữ liệu Trưởng phòng IC của cơ sở không nhất quán giữa campus và phòng IC. Vui lòng kiểm tra và sửa dữ liệu trước khi tạo Staff Leader mới.",
            Outcome.PartialReference =>
                "Dữ liệu Trưởng phòng IC của cơ sở chưa đồng bộ giữa campus và phòng IC. Vui lòng đồng bộ dữ liệu trước khi tạo Staff Leader mới.",
            Outcome.UnlinkedStaffLeader =>
                "Đã tồn tại tài khoản Staff Leader trong cơ sở nhưng chưa được liên kết đúng với campus/phòng IC. Vui lòng kiểm tra dữ liệu trước khi tạo mới.",
            _ => "Không thể tạo Staff Leader cho cơ sở này.",
        };
    }

    /// <summary>
    /// Evaluates the availability of a Staff Leader slot for <paramref name="campusId"/>.
    /// Read-only (no tracking, no writes). The write-side handler must still re-check inside its
    /// transaction to defeat a concurrent create.
    /// </summary>
    public static async Task<Result> ResolveAsync(
        IApplicationDbContext db, ulong campusId, CancellationToken cancellationToken)
    {
        var campus = await db.Campuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampusId == campusId, cancellationToken);
        if (campus is null)
            return new Result(Outcome.CampusNotFound, campusId, null, null, null, null);
        if (campus.Status != EntityStatuses.Active)
            return new Result(Outcome.CampusInactive, campusId, campus.Name, null, null, null);

        // BR-SL-05/06/07/08: the campus must have EXACTLY ONE active IC department.
        var icDepts = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == campusId
                     && d.DepartmentType == "IC"
                     && d.Status == EntityStatuses.Active)
            .Select(d => new { d.DepartmentId, d.Name, d.HeadUserId })
            .ToListAsync(cancellationToken);
        if (icDepts.Count == 0)
            return new Result(Outcome.NoActiveIcDepartment, campusId, campus.Name, null, null, null);
        if (icDepts.Count > 1)
            return new Result(Outcome.MultipleActiveIcDepartments, campusId, campus.Name, null, null, null);

        var icDept = icDepts[0];
        var campusHead = campus.IcHeadUserId;
        var deptHead = icDept.HeadUserId;

        Result With(Outcome kind, ExistingLeader? leader = null) =>
            new(kind, campusId, campus.Name, icDept.DepartmentId, icDept.Name, leader);

        // Both heads set → either a consistent leader, or a mismatch (data lệch loại 1, C5).
        if (campusHead is not null && deptHead is not null)
        {
            if (campusHead != deptHead)
                return With(Outcome.ReferenceMismatch);

            var leader = await LoadLeaderAsync(db, campusHead.Value, cancellationToken);
            return With(LeaderOutcome(leader?.Status), leader);
        }

        // Exactly one head set → partial reference (data lệch loại 2).
        if (campusHead is not null || deptHead is not null)
        {
            var leader = await LoadLeaderAsync(db, (campusHead ?? deptHead)!.Value, cancellationToken);
            return With(Outcome.PartialReference, leader);
        }

        // Neither head set. Detect an unlinked STAFF/LEADER on the campus+IC dept (data lệch loại 3).
        var unlinked = await db.Users.AsNoTracking()
            .Where(u => u.PrimaryCampusId == campusId
                     && u.DepartmentId == icDept.DepartmentId
                     && u.SubRole == UserSubRoles.Leader
                     && u.Role.RoleCode == RoleCodes.Staff)
            .Select(u => new ExistingLeader(u.UserId, u.FullName, u.Email, u.Status))
            .FirstOrDefaultAsync(cancellationToken);
        if (unlinked is not null)
            return With(Outcome.UnlinkedStaffLeader, unlinked);

        return With(Outcome.CanCreate);
    }

    /// <summary>
    /// Throws the spec-mandated HTTP error (404 / 422 / 409) carrying the error code and, where
    /// relevant, the existing leader data. Returns the single ACTIVE IC department id on success.
    /// </summary>
    public static ulong EnsureCanCreate(Result result)
    {
        if (result.CanCreate)
            return result.IcDepartmentId!.Value;

        switch (result.Kind)
        {
            case Outcome.CampusNotFound:
                throw new NotFoundException(result.Message);

            case Outcome.CampusInactive:
            case Outcome.NoActiveIcDepartment:
                throw new BusinessRuleException(result.Message, result.BlockingReason!);

            default:
                throw new ConflictException(result.Message, result.BlockingReason!, BuildData(result));
        }
    }

    /// <summary>The <c>data</c> payload surfaced to the client for a 409 (matches spec §9 examples).</summary>
    private static object BuildData(Result result) => result.Leader is null
        ? new { campusId = result.CampusId, campusName = result.CampusName }
        : new
        {
            campusId = result.CampusId,
            campusName = result.CampusName,
            existingLeaderId = result.Leader.UserId,
            existingLeaderName = result.Leader.FullName,
            existingLeaderEmail = result.Leader.Email,
            existingLeaderStatus = result.Leader.Status,
        };

    /// <summary>True when the campus currently has a single consistent Staff Leader (any status),
    /// i.e. it is in a state where Replace Staff Leader can run.</summary>
    public static bool IsReplaceable(Result result) =>
        result.Kind is Outcome.LeaderActive or Outcome.LeaderInactive or Outcome.LeaderLocked;

    /// <summary>
    /// For the Replace Staff Leader flow (the inverse of <see cref="EnsureCanCreate"/>): a replace
    /// needs the campus to already have ONE consistent Staff Leader. Throws the spec 404/422/409
    /// for every other state (no leader → use Create; inconsistent head data → cleanup first).
    /// Returns when the campus has a replaceable leader. See REPLACE_STAFF_LEADER spec §9/§10/§15.
    /// </summary>
    public static void EnsureReplaceable(Result result)
    {
        switch (result.Kind)
        {
            case Outcome.LeaderActive:
            case Outcome.LeaderInactive:
            case Outcome.LeaderLocked:
                return; // a single consistent leader exists — replace can proceed.

            case Outcome.CampusNotFound:
                throw new NotFoundException("Không tìm thấy cơ sở được chọn.");

            case Outcome.CampusInactive:
                throw new BusinessRuleException(
                    "Cơ sở được chọn đang không hoạt động.", AccountErrorCodes.CampusInactive);

            case Outcome.NoActiveIcDepartment:
                throw new BusinessRuleException(
                    "Cơ sở này chưa có Phòng Hợp tác Quốc tế đang hoạt động.", AccountErrorCodes.IcDepartmentMissing);

            case Outcome.MultipleActiveIcDepartments:
                throw new ConflictException(
                    "Dữ liệu không hợp lệ: cơ sở có nhiều hơn một phòng IC đang hoạt động.",
                    AccountErrorCodes.IcDepartmentMultiple);

            case Outcome.CanCreate:
                throw new ConflictException(
                    "Cơ sở này chưa có Staff Leader. Vui lòng dùng chức năng tạo Staff Leader.",
                    AccountErrorCodes.CampusHasNoStaffLeader);

            default: // ReferenceMismatch / PartialReference / UnlinkedStaffLeader
                throw new ConflictException(
                    "Dữ liệu Staff Leader không nhất quán giữa campus và phòng IC. Vui lòng đồng bộ dữ liệu trước khi thay thế.",
                    result.BlockingReason ?? AccountErrorCodes.IcLeaderReferenceMismatch);
        }
    }

    private static Outcome LeaderOutcome(string? status) => status switch
    {
        UserStatuses.Inactive => Outcome.LeaderInactive,
        UserStatuses.Locked => Outcome.LeaderLocked,
        _ => Outcome.LeaderActive, // ACTIVE or any unexpected status → block as "already exists".
    };

    private static Task<ExistingLeader?> LoadLeaderAsync(
        IApplicationDbContext db, ulong userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new ExistingLeader(u.UserId, u.FullName, u.Email, u.Status))
            .FirstOrDefaultAsync(cancellationToken);
}
