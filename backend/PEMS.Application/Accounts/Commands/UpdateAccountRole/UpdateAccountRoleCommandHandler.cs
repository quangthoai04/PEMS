using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

/// <summary>
/// UC-100 Update Account Role, hardened per the "safe account role change" spec.
///
/// The account being re-roled may still be running a delegation — as its Host, its Coordinator, an
/// invited/assigned participant, the owner of an open logistics task, or the head of a department.
/// Changing the role would strip the permissions those duties depend on, so the handler refuses the
/// change (409) instead of silently transferring, removing or deferring anything: every
/// responsibility must be resolved through its own flow first (spec §1/§5).
///
/// Ordering is the guarantee, not just the checks: everything runs inside one transaction that
/// starts by taking a shared row lock on the target user (see <see cref="IUserMutationLockService"/>),
/// and the entity is not mutated until every validation and the dependency check have passed — a
/// blocked request must leave the database byte-for-byte unchanged (spec §12/§21).
/// </summary>
public sealed class UpdateAccountRoleCommandHandler : IRequestHandler<UpdateAccountRoleCommand, UpdateAccountRoleResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessionService;
    private readonly IDateTimeService _clock;
    private readonly IEmailService _emailService;
    private readonly IUserMutationLockService _lockService;

    public UpdateAccountRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        IDateTimeService clock,
        IEmailService emailService,
        IUserMutationLockService lockService)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _clock = clock;
        _emailService = emailService;
        _lockService = lockService;
    }

    public async Task<UpdateAccountRoleResponse> Handle(UpdateAccountRoleCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUser.UserId;
        var actorCampus = _currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUser.RoleCode);
        var isStaffLeaderCaller = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        // ── Lock FIRST, read after. A concurrent assign-host / invite-participant / assign-logistics
        //    transaction takes the same lock, so the two serialize and neither can commit against a
        //    view of the account the other has already invalidated (spec §13.5). The incoming
        //    department head (when one is supplied) is locked in the SAME call: the service orders
        //    ids ascending, which taking two separate locks in request order would not (spec §13.4). ──
        var lockUserIds = request.ReplacementDepartmentHeadUserId is { } replacementId
                          && replacementId != request.UserId
            ? new[] { request.UserId, replacementId }
            : new[] { request.UserId };
        await _lockService.LockUsersAsync(lockUserIds, cancellationToken);

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        // Staff Leaders may only manage users already within their own campus.
        if (!privileged)
        {
            if (actorCampus is null)
                throw new ForbiddenException("Your account is not assigned to a campus and cannot manage accounts.");
            if (user.PrimaryCampusId is not null && user.PrimaryCampusId != actorCampus)
                throw new ForbiddenException("You can only manage accounts within your own campus.");
        }

        var oldRoleCode = user.Role?.RoleCode ?? "UNKNOWN";
        var oldSubRole = user.SubRole;
        var oldDepartmentId = user.DepartmentId;
        var oldPrimaryCampusId = user.PrimaryCampusId;
        var oldStudentCode = user.StudentCode;
        var oldFullName = user.FullName;
        var oldEmail = user.Email;

        // ── UC-100-SL: a Staff Leader cannot change their own role and cannot touch a LOCKED
        //    account; the manageable target/role set is enforced right after. ──
        if (isStaffLeaderCaller)
        {
            if (actorId is not null && user.UserId == actorId.Value)
                throw new ForbiddenException("Bạn không thể thay đổi vai trò của chính mình.");
            if (user.Status == UserStatuses.Locked)
                throw new BusinessRuleException(
                    "Tài khoản đang bị khóa vì lý do bảo mật và không thể cập nhật vai trò tại đây.");

            EnsureStaffLeaderManageableTarget(oldRoleCode, oldSubRole);
            EnsureStaffLeaderManageableNewRole(request.NewRoleCode);
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            fullName = oldFullName,
            email = oldEmail,
            roleCode = oldRoleCode,
            subRole = oldSubRole,
            campusId = oldPrimaryCampusId,
            departmentId = oldDepartmentId,
            studentCode = oldStudentCode,
        });

        // ── Identity fields (Họ tên / Email), Staff Leader flow only ─────────────────────────────
        // Whether identity may be edited is derived from the target's ORIGINAL role/sub-role loaded
        // from the DB (NOT NewRoleCode / the dropdown), so promoting a STAFF/STAFF to STUDENT keeps
        // identity editable while a DEPARTMENT/STAFF stays locked regardless of the new role.
        var canEditIdentity = isStaffLeaderCaller && (
            (oldRoleCode == RoleCodes.Staff && oldSubRole == UserSubRoles.Staff) ||
            (oldRoleCode == RoleCodes.Department && oldSubRole == UserSubRoles.Leader) ||
            oldRoleCode == RoleCodes.Student);

        var requestedFullName = request.FullName?.Trim();
        var requestedEmail = request.Email?.Trim().ToLowerInvariant();
        var attemptsFullNameChange = requestedFullName is not null && requestedFullName != oldFullName;
        var attemptsEmailChange = requestedEmail is not null && requestedEmail != oldEmail;

        // A locked-down target may still be role-changed, but any real identity change is refused
        // (the request must not silently succeed by dropping the field — BR §4.13).
        if (!canEditIdentity && (attemptsFullNameChange || attemptsEmailChange))
            throw new ForbiddenException(
                "Bạn không có quyền chỉnh sửa họ tên hoặc email của tài khoản này.");

        // Resolved into locals only — nothing is written to the entity before every check passes.
        var resolvedFullName = oldFullName;
        var resolvedEmail = oldEmail;
        if (canEditIdentity)
        {
            if (requestedFullName is not null)
            {
                if (requestedFullName.Length == 0)
                    throw new ValidationException("Vui lòng nhập họ và tên.");
                if (requestedFullName.Length > 150)
                    throw new ValidationException("Họ và tên không được vượt quá 150 ký tự.");
                resolvedFullName = requestedFullName;
            }

            if (requestedEmail is not null && attemptsEmailChange)
            {
                if (requestedEmail.Length == 0)
                    throw new ValidationException("Vui lòng nhập địa chỉ email.");
                if (requestedEmail.Length > 150)
                    throw new ValidationException("Email không được vượt quá 150 ký tự.");
                resolvedEmail = requestedEmail;
            }
        }

        var shape = isStaffLeaderCaller
            ? await AccountProvisioningRules.ResolveStaffLeaderTargetAsync(
                _db, request.NewRoleCode, request.DepartmentId, actorCampus!.Value, cancellationToken)
            : await AccountProvisioningRules.ResolveAsync(
                _db, request.NewRoleCode, request.SubRole, request.PrimaryCampusId, request.DepartmentId,
                privileged, actorCampus, cancellationToken);

        // ── student_code (MSSV), Staff Leader flow only ──────────────────────────────────────
        // STUDENT requires a trimmed, ≤30-char code; any other role clears the code so a promoted
        // STUDENT never keeps a hidden MSSV. Privileged (ADMIN/HO) edits leave student_code
        // untouched (out of scope for this flow). Uniqueness is verified further down, with the
        // other cross-row checks, so nothing queries the DB on behalf of a request that a blocker
        // is about to refuse.
        var resolvedStudentCode = oldStudentCode;
        if (isStaffLeaderCaller)
        {
            if (shape.RoleCode == RoleCodes.Student)
            {
                var code = (request.StudentCode ?? string.Empty).Trim();
                if (code.Length == 0)
                    throw new ValidationException("Vui lòng nhập mã số sinh viên.");
                if (code.Length > 30)
                    throw new ValidationException("Mã số sinh viên không được vượt quá 30 ký tự.");
                resolvedStudentCode = code;
            }
            else
            {
                resolvedStudentCode = null;
            }
        }

        // ── Change classification (spec §6). Only a STRUCTURAL change can invalidate a running
        //    responsibility, so only that runs the dependency checker; renaming an account or
        //    fixing its MSSV must never be blocked by a delegation. ──
        var hasStructuralChange =
            oldRoleCode != shape.RoleCode
            || oldSubRole != shape.SubRole
            || oldDepartmentId != shape.DepartmentId
            || oldPrimaryCampusId != shape.PrimaryCampusId;
        var hasIdentityChange = resolvedFullName != oldFullName || resolvedEmail != oldEmail;
        var hasStudentCodeChange = resolvedStudentCode != oldStudentCode;
        var hasAnyChange = hasStructuralChange || hasIdentityChange || hasStudentCodeChange;

        // Pure no-op: no audit, no UpdatedAt, no session revoke, no email, no department write.
        if (!hasAnyChange)
        {
            await transaction.CommitAsync(cancellationToken);
            return new UpdateAccountRoleResponse
            {
                UserId = user.UserId,
                RoleCode = oldRoleCode,
                PrimaryCampusId = oldPrimaryCampusId,
                RevokedSessions = 0,
                Message = "Không có thay đổi nào được gửi lên; tài khoản giữ nguyên.",
            };
        }

        if (hasStructuralChange)
        {
            // Departments are locked in the same ascending order everywhere, and always AFTER users,
            // so no two flows can hold these two resources in opposite order (spec §13.4).
            var departmentIds = new[] { oldDepartmentId, shape.DepartmentId }
                .Where(id => id is not null).Select(id => id!.Value).Distinct().ToArray();
            await _lockService.LockDepartmentsAsync(departmentIds, cancellationToken);

            // ── Department-head handover, when the caller supplied a successor. It runs BEFORE the
            //    dependency check on purpose: the check then reads a department whose head_user_id
            //    has already moved, so DEPARTMENT_HEAD_ASSIGNMENT simply does not fire and the two
            //    steps need no special-case coordination. Should any other blocker refuse the change
            //    a moment later, the throw rolls this transaction back and the handover goes with it
            //    — the department keeps its original head (spec §8.6/§15). ──
            if (request.ReplacementDepartmentHeadUserId is not null)
                await HandOverDepartmentHeadAsync(
                    user, oldRoleCode, oldSubRole, oldDepartmentId, shape,
                    request.ReplacementDepartmentHeadUserId.Value, actorId, cancellationToken);

            var impact = await AccountRoleChangeDependencyChecker.CheckAsync(
                _db, user.UserId, oldRoleCode, oldSubRole, oldDepartmentId,
                shape.RoleCode, shape.SubRole, shape.DepartmentId, cancellationToken);

            if (!impact.CanChangeRole)
                throw new ConflictException(
                    AccountRoleChangeDependencyRule.BuildSummaryMessage(impact),
                    AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities,
                    new
                    {
                        affectedVisitCount = impact.AffectedVisitCount,
                        blockers = impact.Blockers,
                    });
        }

        // A successor sent with a change that does not vacate a department head seat is a
        // misunderstanding on the caller's side, not something to quietly drop.
        if (request.ReplacementDepartmentHeadUserId is not null && !hasStructuralChange)
            throw new BusinessRuleException(
                "Thay đổi này không làm tài khoản rời ghế Trưởng phòng ban nên không cần chỉ định người thay thế.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);

        // ── Cross-row validation. Runs only once the change is known to be permitted. ──
        if (hasIdentityChange && resolvedEmail != oldEmail)
        {
            var emailTaken = await _db.Users.AnyAsync(
                u => u.Email == resolvedEmail && u.UserId != user.UserId, cancellationToken);
            if (emailTaken)
                throw new ConflictException(
                    "Email này đã được sử dụng bởi tài khoản khác.", AccountErrorCodes.EmailAlreadyExists);
        }

        if (hasStudentCodeChange && resolvedStudentCode is not null)
        {
            // Re-checked at submit time (the frontend's options may be stale); the DB also has
            // uq_users_student_code as a last line of defence.
            var duplicate = await _db.Users.AnyAsync(
                u => u.StudentCode == resolvedStudentCode && u.UserId != user.UserId, cancellationToken);
            if (duplicate)
                throw new ConflictException("Mã số sinh viên này đã được sử dụng bởi tài khoản khác.");
        }

        var now = _clock.VietnamNow;

        // ── Department head (head_user_id) — BR-100SL-06/06b/06c, tightened by spec §12.3/§12.4.
        //    The old department is NEVER auto-cleared: if the target still heads it, the
        //    DEPARTMENT_HEAD_ASSIGNMENT blocker above already refused the request, so reaching this
        //    point means the handover through /departments/reassign-lead has happened. And with no
        //    structural change the department row is not touched at all (an email edit must not
        //    bump departments.updated_at). ──
        if (hasStructuralChange
            && shape.RoleCode == RoleCodes.Department
            && shape.SubRole == UserSubRoles.Leader
            && shape.DepartmentId is not null)
        {
            var newDept = await _db.Departments.FirstOrDefaultAsync(
                d => d.DepartmentId == shape.DepartmentId, cancellationToken);
            if (newDept is not null)
            {
                if (newDept.HeadUserId is not null && newDept.HeadUserId != user.UserId)
                    throw new ConflictException(
                        "Phòng ban này đã có trưởng phòng. Vui lòng bỏ gán trưởng phòng hiện tại trước khi chỉ định người mới.");
                newDept.HeadUserId = user.UserId;
                newDept.UpdatedBy = actorId;
                newDept.UpdatedAt = now;
            }
        }

        // ── Everything validated: only now does the entity change. ──
        user.FullName = resolvedFullName;
        user.Email = resolvedEmail;
        user.RoleId = shape.RoleId;
        user.SubRole = shape.SubRole;
        user.DepartmentId = shape.DepartmentId;
        user.PrimaryCampusId = shape.PrimaryCampusId;
        user.StudentCode = resolvedStudentCode;
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
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "Role",
                    OldValueText = oldValues,
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        fullName = user.FullName,
                        email = user.Email,
                        roleCode = shape.RoleCode,
                        subRole = shape.SubRole,
                        campusId = shape.PrimaryCampusId,
                        departmentId = shape.DepartmentId,
                        studentCode = resolvedStudentCode,
                    })
                }
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // ── Post-commit side effects, in this order. Neither may run for a refused request, and a
        //    failure here must not undo a committed role change (spec §12.5). ──
        var revoked = await _sessionService.RevokeAllActiveSessionsAsync(
            user.UserId, SessionRevokeReasons.RoleChanged, actorId, cancellationToken);

        // ── UC-100-SL BR-100SL-08: notify the user their role changed. Non-fatal. ──
        await SendRoleChangedNotificationAsync(user, shape, cancellationToken);

        return new UpdateAccountRoleResponse
        {
            UserId = user.UserId,
            RoleCode = shape.RoleCode,
            PrimaryCampusId = shape.PrimaryCampusId,
            RevokedSessions = revoked,
        };
    }

    /// <summary>
    /// Hands the target's GENERAL department to <paramref name="replacementUserId"/> as part of the
    /// same transaction as the role change (spec §8.6). Doing it here rather than through a separate
    /// /departments/reassign-lead call is what keeps the flow reachable at all: that command demotes
    /// the outgoing head to DEPARTMENT/STAFF, a shape a Staff Leader is not allowed to manage
    /// (spec §3.3), so the role change it was supposed to unblock would become impossible.
    ///
    /// Every refusal here is a 422 rather than a silent skip — a successor the caller chose must
    /// either be used or reported. Both accounts are already row-locked by the caller.
    /// </summary>
    private async Task HandOverDepartmentHeadAsync(
        User target,
        string oldRoleCode,
        string? oldSubRole,
        ulong? oldDepartmentId,
        AccountProvisioningRules.ResolvedShape shape,
        ulong replacementUserId,
        ulong? actorId,
        CancellationToken cancellationToken)
    {
        var wasDepartmentLeader = oldRoleCode == RoleCodes.Department
            && oldSubRole == UserSubRoles.Leader
            && oldDepartmentId is not null;
        if (!wasDepartmentLeader)
            throw new BusinessRuleException(
                "Tài khoản này không phải Trưởng phòng ban nên không cần chỉ định người thay thế.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);

        var staysLeaderOfSameDepartment = shape.RoleCode == RoleCodes.Department
            && shape.SubRole == UserSubRoles.Leader
            && shape.DepartmentId == oldDepartmentId;
        if (staysLeaderOfSameDepartment)
            throw new BusinessRuleException(
                "Tài khoản vẫn là Trưởng phòng của phòng ban này nên không cần người thay thế.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);

        if (replacementUserId == target.UserId)
            throw new BusinessRuleException(
                "Không thể chọn chính tài khoản đang được thay đổi vai trò làm Trưởng phòng thay thế.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);

        var department = await _db.Departments.FirstOrDefaultAsync(
            d => d.DepartmentId == oldDepartmentId, cancellationToken)
            ?? throw new BusinessRuleException(
                "Không tìm thấy phòng ban cần bàn giao.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);

        // Read under the department lock: if somebody else moved the seat first, the caller is
        // acting on a stale screen and must reload rather than have us overwrite their handover.
        if (department.HeadUserId != target.UserId)
            throw new ConflictException(
                "Trưởng phòng của phòng ban này vừa thay đổi. Vui lòng tải lại và thử lại.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);

        var replacement = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == replacementUserId, cancellationToken)
            ?? throw new NotFoundException("Account", replacementUserId);

        // Same membership rules the reassign-lead command applies: an active member of THIS
        // department, in its campus, who is not already leading something else.
        if (replacement.Role?.RoleCode != RoleCodes.Department || replacement.SubRole != UserSubRoles.Staff)
            throw new BusinessRuleException(
                "Trưởng phòng thay thế phải là nhân viên phòng ban.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);
        if (replacement.DepartmentId != department.DepartmentId)
            throw new BusinessRuleException(
                "Trưởng phòng thay thế phải thuộc đúng phòng ban đang bàn giao.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);
        if (replacement.Status != UserStatuses.Active)
            throw new BusinessRuleException(
                "Trưởng phòng thay thế phải là tài khoản đang hoạt động.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);
        if (replacement.PrimaryCampusId != department.CampusId)
            throw new BusinessRuleException(
                "Trưởng phòng thay thế không thuộc cơ sở của phòng ban này.",
                AccountErrorCodes.InvalidDepartmentHeadReplacement);

        var now = _clock.VietnamNow;
        replacement.SubRole = UserSubRoles.Leader;
        replacement.UpdatedAt = now;
        replacement.UpdatedBy = actorId;

        department.HeadUserId = replacement.UserId;
        department.UpdatedAt = now;
        department.UpdatedBy = actorId;

        // Flushed inside the transaction so the dependency check below reads the NEW head. Still
        // atomic: a later refusal rolls the whole transaction back, handover included.
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Spec §3.1/§3.3: a Staff Leader manages exactly three account shapes. Everything else —
    /// ADMIN, HO, VISITOR, another STAFF/LEADER, a DEPARTMENT/STAFF — is refused here rather than
    /// only being hidden in the UI, so a direct API call cannot reach the role change.
    /// </summary>
    private static void EnsureStaffLeaderManageableTarget(string roleCode, string? subRole)
    {
        var manageable =
            (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
            || (roleCode == RoleCodes.Department && subRole == UserSubRoles.Leader)
            || (roleCode == RoleCodes.Student && string.IsNullOrEmpty(subRole));

        if (!manageable)
            throw new AuthBusinessException(
                AccountErrorCodes.AccountRoleTargetNotManageable,
                "Tài khoản này nằm ngoài phạm vi quản lý vai trò của bạn.");
    }

    /// <summary>
    /// Spec §3.2: the requested role must land on one of the same three shapes. Turning an internal
    /// account into a VISITOR (or the reverse) needs its own flow — it touches contact ownership,
    /// visitor_user_id and the Visitor DB triggers, none of which this command handles.
    /// </summary>
    private static void EnsureStaffLeaderManageableNewRole(string? newRoleCode)
    {
        var normalized = (newRoleCode ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized is not (RoleCodes.Staff or RoleCodes.Department or RoleCodes.Student))
            throw new AuthBusinessException(
                AccountErrorCodes.AccountRoleTargetNotManageable,
                "Bạn chỉ được chuyển tài khoản sang Staff, Trưởng phòng ban hoặc Sinh viên.");
    }

    private async Task SendRoleChangedNotificationAsync(
        User user, AccountProvisioningRules.ResolvedShape shape, CancellationToken cancellationToken)
    {
        var campusName = shape.PrimaryCampusId is null
            ? null
            : await _db.Campuses.AsNoTracking()
                .Where(c => c.CampusId == shape.PrimaryCampusId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var departmentName = shape.DepartmentId is null
            ? null
            : await _db.Departments.AsNoTracking()
                .Where(d => d.DepartmentId == shape.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var name = System.Net.WebUtility.HtmlEncode(user.FullName);
        var emailEnc = System.Net.WebUtility.HtmlEncode(user.Email);
        var roleEnc = System.Net.WebUtility.HtmlEncode(ResolveRoleDisplayName(shape.RoleCode, shape.SubRole));
        var campusEnc = System.Net.WebUtility.HtmlEncode(campusName ?? "—");

        var html =
            $"<p>Xin chào {name},</p>" +
            "<p>Vai trò tài khoản PEMS của bạn đã được cập nhật.</p>" +
            "<p><strong>Thông tin mới:</strong></p>" +
            "<ul>" +
            $"<li>Email đăng nhập: <strong>{emailEnc}</strong></li>" +
            $"<li>Vai trò mới: <strong>{roleEnc}</strong></li>" +
            $"<li>Cơ sở: <strong>{campusEnc}</strong></li>" +
            (departmentName is null ? "" : $"<li>Phòng ban: <strong>{System.Net.WebUtility.HtmlEncode(departmentName)}</strong></li>") +
            (shape.RoleCode == RoleCodes.Student && !string.IsNullOrWhiteSpace(user.StudentCode)
                ? $"<li>Mã số sinh viên: <strong>{System.Net.WebUtility.HtmlEncode(user.StudentCode)}</strong></li>"
                : "") +
            "</ul>" +
            "<p>Thay đổi này có thể yêu cầu bạn đăng nhập lại để hệ thống áp dụng quyền truy cập mới.</p>" +
            "<p>Nếu bạn cho rằng thông tin này chưa chính xác, vui lòng liên hệ Staff Leader hoặc quản trị hệ thống để được hỗ trợ.</p>" +
            "<p>Trân trọng,<br/>PEMS System</p>";

        try
        {
            await _emailService.SendAsync(
                user.Email, "Vai trò tài khoản của bạn đã được cập nhật", html, cancellationToken);
        }
        catch
        {
            // Role change is already committed; a failed notification must not fail the request.
        }
    }

    /// <summary>Human-readable role label shown in the role-changed email.</summary>
    private static string ResolveRoleDisplayName(string roleCode, string? subRole)
        => AccountRoleDisplayNames.Resolve(roleCode, subRole);
}
