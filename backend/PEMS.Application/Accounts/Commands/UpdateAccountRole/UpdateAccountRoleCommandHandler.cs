using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

public sealed class UpdateAccountRoleCommandHandler : IRequestHandler<UpdateAccountRoleCommand, UpdateAccountRoleResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessionService;
    private readonly IDateTimeService _clock;
    private readonly IEmailService _emailService;

    public UpdateAccountRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        IDateTimeService clock,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _clock = clock;
        _emailService = emailService;
    }

    public async Task<UpdateAccountRoleResponse> Handle(UpdateAccountRoleCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUser.UserId;
        var actorCampus = _currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUser.RoleCode);
        var isStaffLeaderCaller = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader;

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        // Staff Leaders may only manage users already within their own campus.
        // (Visitors have no campus, so they can be promoted into the leader's campus.)
        if (!privileged)
        {
            if (actorCampus is null)
                throw new ForbiddenException("Your account is not assigned to a campus and cannot manage accounts.");
            if (user.PrimaryCampusId is not null && user.PrimaryCampusId != actorCampus)
                throw new ForbiddenException("You can only manage accounts within your own campus.");
        }

        // ── UC-100-SL: a Staff Leader cannot change their own role and cannot touch a
        //    LOCKED account; allowed targets are constrained by ResolveStaffLeaderTargetAsync. ──
        if (isStaffLeaderCaller)
        {
            if (actorId is not null && user.UserId == actorId.Value)
                throw new ForbiddenException("Bạn không thể thay đổi vai trò của chính mình.");
            if (user.Status == UserStatuses.Locked)
                throw new BusinessRuleException(
                    "Tài khoản đang bị khóa vì lý do bảo mật và không thể cập nhật vai trò tại đây.");
        }

        var oldRoleCode = user.Role?.RoleCode ?? "UNKNOWN";
        var oldSubRole = user.SubRole;
        var oldDepartmentId = user.DepartmentId;
        var oldStudentCode = user.StudentCode;
        var oldFullName = user.FullName;
        var oldEmail = user.Email;
        var oldValues = JsonSerializer.Serialize(new
        {
            fullName = oldFullName,
            email = oldEmail,
            roleCode = oldRoleCode,
            subRole = oldSubRole,
            campusId = user.PrimaryCampusId,
            departmentId = user.DepartmentId,
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

        if (canEditIdentity)
        {
            if (requestedFullName is not null)
            {
                if (requestedFullName.Length == 0)
                    throw new ValidationException("Vui lòng nhập họ và tên.");
                if (requestedFullName.Length > 150)
                    throw new ValidationException("Họ và tên không được vượt quá 150 ký tự.");
                user.FullName = requestedFullName;
            }

            if (requestedEmail is not null && attemptsEmailChange)
            {
                if (requestedEmail.Length == 0)
                    throw new ValidationException("Vui lòng nhập địa chỉ email.");
                if (requestedEmail.Length > 150)
                    throw new ValidationException("Email không được vượt quá 150 ký tự.");

                // Uniqueness excludes the target itself (so re-sending the current email is a no-op).
                var emailTaken = await _db.Users.AnyAsync(
                    u => u.Email == requestedEmail && u.UserId != user.UserId, cancellationToken);
                if (emailTaken)
                    throw new ConflictException(
                        "Email này đã được sử dụng bởi tài khoản khác.", AccountErrorCodes.EmailAlreadyExists);

                user.Email = requestedEmail;
            }
        }

        var shape = isStaffLeaderCaller
            ? await AccountProvisioningRules.ResolveStaffLeaderTargetAsync(
                _db, request.NewRoleCode, request.DepartmentId, actorCampus!.Value, cancellationToken)
            : await AccountProvisioningRules.ResolveAsync(
                _db, request.NewRoleCode, request.SubRole, request.PrimaryCampusId, request.DepartmentId,
                privileged, actorCampus, cancellationToken);

        // ── student_code (MSSV), Staff Leader flow only ──────────────────────────────────────
        // STUDENT requires a trimmed, ≤30-char code that is unique across other accounts; any
        // other role clears the code so a promoted STUDENT never keeps a hidden MSSV. Privileged
        // (ADMIN/HO) edits leave student_code untouched (out of scope for this flow).
        var resolvedStudentCode = user.StudentCode;
        if (isStaffLeaderCaller)
        {
            if (shape.RoleCode == RoleCodes.Student)
            {
                var code = (request.StudentCode ?? string.Empty).Trim();
                if (code.Length == 0)
                    throw new ValidationException("Vui lòng nhập mã số sinh viên.");
                if (code.Length > 30)
                    throw new ValidationException("Mã số sinh viên không được vượt quá 30 ký tự.");

                // Re-check uniqueness at submit time (handles the race after the frontend loaded
                // options); the DB also has uq_users_student_code as a last line of defence.
                var duplicate = await _db.Users.AnyAsync(
                    u => u.StudentCode == code && u.UserId != user.UserId, cancellationToken);
                if (duplicate)
                    throw new ConflictException("Mã số sinh viên này đã được sử dụng bởi tài khoản khác.");

                resolvedStudentCode = code;
            }
            else
            {
                resolvedStudentCode = null;
            }
        }

        var now = _clock.VietnamNow;

        // ── Department head (head_user_id) synchronisation — BR-100SL-06/06b/06c. ──
        var assignsDepartmentHead = shape.RoleCode == RoleCodes.Department
            && shape.SubRole == UserSubRoles.Leader
            && shape.DepartmentId is not null;

        // Clear the old department's head if it pointed at this user and we are leaving it.
        if (oldDepartmentId is not null && oldDepartmentId != shape.DepartmentId)
        {
            var oldDept = await _db.Departments.FirstOrDefaultAsync(
                d => d.DepartmentId == oldDepartmentId, cancellationToken);
            if (oldDept is not null && oldDept.HeadUserId == user.UserId)
            {
                oldDept.HeadUserId = null;
                oldDept.UpdatedBy = actorId;
                oldDept.UpdatedAt = now;
            }
        }

        if (assignsDepartmentHead)
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

        // Revoke active sessions so the user must re-authenticate with the new role.
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
