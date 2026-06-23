using System.Text.Json;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, CreateAccountResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeService _clock;
    private readonly AuthOptions _options;
    private readonly IEmailService _emailService;

    public CreateAccountCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IDateTimeService clock,
        AuthOptions options,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _options = options;
        _emailService = emailService;
    }

    public async Task<CreateAccountResponse> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var actorId = _currentUser.UserId;
        var actorCampus = _currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUser.RoleCode);

        if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException("Email này đã tồn tại trong hệ thống.", AccountErrorCodes.EmailAlreadyExists);

        var isStaffLeaderCaller = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader;

        var targetRole = (request.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        var subRole = request.SubRole;
        var departmentId = request.DepartmentId;

        AccountProvisioningRules.ResolvedShape shape;

        // ── Flow B (HO creates Staff Leader): the new user must become the IC head of the
        //    chosen campus and its IC department. These carry the targets to the write step. ──
        var assignsCampusIcHead = false;
        ulong icCampusId = 0;
        ulong icDepartmentId = 0;

        // ── HO-per-campus uniqueness (HO creates HO): at most one HO account per campus.
        //    The final re-check runs inside the write transaction to defeat concurrent creates. ──
        var enforcesUniqueHoPerCampus = false;
        ulong hoCampusId = 0;

        if (_currentUser.RoleCode == RoleCodes.Ho)
        {
            // ── UC-96 HO scope: HO may create only HO or Staff-Leader accounts. The sub-role
            //    and IC department are derived server-side (the HO form never sends them). ──
            if (targetRole != RoleCodes.Ho && targetRole != RoleCodes.Staff)
                throw new ForbiddenException("HO chỉ được tạo tài khoản HO hoặc Staff Leader.");

            if (targetRole == RoleCodes.Staff)
            {
                // Flow B — Create STAFF / LEADER (Trưởng phòng IC). The full case matrix
                // (campus active, exactly one IC dept, no existing/locked/mismatched leader)
                // lives in StaffLeaderAvailability so the modal pre-check and this write-side
                // check stay identical. EnsureCanCreate throws the spec 404/422/409 with the
                // error code + existing-leader data. See SPEC §5/§8/§9/§10/§11.
                if (request.PrimaryCampusId is null)
                    throw new ValidationException("Vui lòng chọn cơ sở.");

                var availability = await StaffLeaderAvailability.ResolveAsync(
                    _db, request.PrimaryCampusId.Value, cancellationToken);
                var resolvedIcDeptId = StaffLeaderAvailability.EnsureCanCreate(availability);

                subRole = UserSubRoles.Leader;
                departmentId = resolvedIcDeptId;
                assignsCampusIcHead = true;
                icCampusId = request.PrimaryCampusId.Value;
                icDepartmentId = resolvedIcDeptId;
            }
            else // HO — Create HO account (one HO per campus). See HO_CREATE_HO_ACCOUNT spec §6/§9.
            {
                // The full case matrix (campus active, no existing HO in any status, no
                // inconsistent multi-HO data) lives in HoCampusAvailability so the modal
                // pre-check and this write-side check stay identical. EnsureCanCreate throws the
                // spec 404/422/409 with the per-status error code + existing-HO data.
                if (request.PrimaryCampusId is null)
                    throw new ValidationException("Vui lòng chọn cơ sở.");

                var hoAvailability = await HoCampusAvailability.ResolveAsync(
                    _db, request.PrimaryCampusId.Value, cancellationToken);
                HoCampusAvailability.EnsureCanCreate(hoAvailability);

                subRole = null;
                departmentId = null;
                enforcesUniqueHoPerCampus = true;
                hoCampusId = request.PrimaryCampusId.Value;
            }

            shape = await AccountProvisioningRules.ResolveAsync(
                _db, request.RoleCode, subRole, request.PrimaryCampusId, departmentId,
                privileged, actorCampus, cancellationToken);
        }
        else if (isStaffLeaderCaller)
        {
            // ── UC-96-SL Staff Leader scope: campus is forced to the leader's own campus,
            //    and only STAFF/STAFF, DEPARTMENT/LEADER or STUDENT may be created. ──
            if (actorCampus is null)
                throw new ForbiddenException(
                    "Tài khoản của bạn chưa được gán cơ sở nên không thể tạo tài khoản.");

            shape = await AccountProvisioningRules.ResolveStaffLeaderTargetAsync(
                _db, request.RoleCode, request.DepartmentId, actorCampus.Value, cancellationToken);
        }
        else
        {
            shape = await AccountProvisioningRules.ResolveAsync(
                _db, request.RoleCode, subRole, request.PrimaryCampusId, departmentId,
                privileged, actorCampus, cancellationToken);
        }

        // ── UC-96-SL: a Department Leader assigned to a department becomes its head.
        //    Refuse up-front if that department already has a different head (no silent
        //    overwrite — BR-96SL-07b). ──
        var assignsDepartmentHead = shape.RoleCode == RoleCodes.Department
            && shape.SubRole == UserSubRoles.Leader
            && shape.DepartmentId is not null;
        if (assignsDepartmentHead)
        {
            var existingHead = await _db.Departments.AsNoTracking()
                .Where(d => d.DepartmentId == shape.DepartmentId)
                .Select(d => d.HeadUserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingHead is not null)
                throw new ConflictException(
                    "Phòng ban này đã có trưởng phòng. Vui lòng bỏ gán trưởng phòng hiện tại trước khi chỉ định người mới.");
        }

        var now = _clock.UtcNow;
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = Clean(request.Phone),
            Gender = request.Gender,
            Nationality = Clean(request.Nationality),
            StudentCode = shape.RoleCode == RoleCodes.Student ? Clean(request.StudentCode) : null,
            RoleId = shape.RoleId,
            SubRole = shape.SubRole,
            PrimaryCampusId = shape.PrimaryCampusId,
            DepartmentId = shape.DepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = CreatedViaValues.ManualCreated,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        var passwordSet = false;
        if (!string.IsNullOrEmpty(request.Password))
        {
            if (!_options.PasswordLoginEnabled)
                throw new BusinessRuleException(
                    "Temporary passwords are disabled in this environment. The user signs in via SSO/FEID.");
            if (!PasswordPolicy.IsStrong(request.Password))
                throw new ValidationException(PasswordPolicy.RequirementsMessage);

            user.PasswordHash = _passwordHasher.Hash(request.Password);
            user.AuthProviders.Add(new UserAuthProvider
            {
                // AuthProviderId is DB-generated; UserId is set via the navigation on save.
                ProviderType = ProviderTypes.LocalPassword,
                ProviderEmail = email,
                IsEnabled = true,
                LinkedAt = now,
            });
            passwordSet = true;
        }

        // ── Insert + head-assignment + audit must commit atomically whenever the new user
        //    becomes a campus IC head (Flow B), a department head (UC-96-SL), or enforces the
        //    one-HO-per-campus rule. An in-transaction re-check defeats the race of two
        //    concurrent creates for the same campus/department (BR-SL-13/14/18, §12). ──
        var needsTransaction = assignsCampusIcHead || assignsDepartmentHead || enforcesUniqueHoPerCampus;
        await using var transaction = needsTransaction
            ? await _db.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            // Re-check the campus has no HO right before inserting (concurrency guard).
            if (enforcesUniqueHoPerCampus
                && await _db.Users.AnyAsync(u =>
                    u.PrimaryCampusId == hoCampusId && u.Role.RoleCode == RoleCodes.Ho, cancellationToken))
                throw new ConflictException(
                    "Cơ sở này đã có tài khoản Head Office. Vui lòng chọn cơ sở khác hoặc quản lý tài khoản HO hiện có.",
                    AccountErrorCodes.CampusHoAlreadyActive);

            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);

            // user.UserId is now populated by the database (BIGINT AUTO_INCREMENT).
            // ── Flow B: make the new Staff Leader the IC head of the campus + IC department. ──
            if (assignsCampusIcHead)
            {
                var campus = await _db.Campuses.FirstOrDefaultAsync(
                    c => c.CampusId == icCampusId, cancellationToken);
                var icDept = await _db.Departments.FirstOrDefaultAsync(
                    d => d.DepartmentId == icDepartmentId, cancellationToken);
                if (campus is null || icDept is null)
                    throw new BusinessRuleException("Không tìm thấy cơ sở hoặc Phòng Hợp tác Quốc tế được chọn.");
                if (campus.IcHeadUserId is not null || icDept.HeadUserId is not null)
                    throw new ConflictException(
                        "Cơ sở này đã có Trưởng phòng Hợp tác Quốc tế. Vui lòng sử dụng chức năng thay thế Trưởng phòng IC nếu muốn thay đổi người phụ trách.",
                        AccountErrorCodes.StaffLeaderAlreadyExistsActive);

                campus.IcHeadUserId = user.UserId;
                campus.UpdatedBy = actorId;
                campus.UpdatedAt = now;

                icDept.HeadUserId = user.UserId;
                icDept.UpdatedBy = actorId;
                icDept.UpdatedAt = now;
            }

            // ── UC-96-SL: make the new Department Leader the head of the chosen department. ──
            if (assignsDepartmentHead)
            {
                var dept = await _db.Departments.FirstOrDefaultAsync(
                    d => d.DepartmentId == shape.DepartmentId, cancellationToken);
                if (dept is not null)
                {
                    dept.HeadUserId = user.UserId;
                    dept.UpdatedBy = actorId;
                    dept.UpdatedAt = now;
                }
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = shape.PrimaryCampusId,
                Action = "CREATE_ACCOUNT",
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "Account",
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            email,
                            roleCode = shape.RoleCode,
                            subRole = shape.SubRole,
                            campusId = shape.PrimaryCampusId,
                            departmentId = shape.DepartmentId,
                        })
                    }
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── UC-96 BR-96-09: notify the new account. Failure does not roll back the
        //    already-committed account; we report it via EmailNotificationStatus. ──
        var emailStatus = await SendCreatedNotificationAsync(user, shape, cancellationToken);

        return new CreateAccountResponse
        {
            UserId = user.UserId,
            Email = email,
            RoleCode = shape.RoleCode,
            PrimaryCampusId = shape.PrimaryCampusId,
            PasswordSet = passwordSet,
            EmailNotificationStatus = emailStatus,
        };
    }

    private async Task<string> SendCreatedNotificationAsync(
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
            "<p>Tài khoản nội bộ PEMS của bạn đã được khởi tạo thành công.</p>" +
            "<p><strong>Thông tin tài khoản:</strong></p>" +
            "<ul>" +
            $"<li>Email đăng nhập: <strong>{emailEnc}</strong></li>" +
            $"<li>Vai trò: <strong>{roleEnc}</strong></li>" +
            $"<li>Cơ sở: <strong>{campusEnc}</strong></li>" +
            (departmentName is null ? "" : $"<li>Phòng ban: <strong>{System.Net.WebUtility.HtmlEncode(departmentName)}</strong></li>") +
            "</ul>" +
            "<p>Bạn vui lòng truy cập Internal Portal của PEMS và đăng nhập bằng chính địa chỉ email trên thông qua SSO/Google/FEID.</p>" +
            "<p><strong>Lưu ý:</strong></p>" +
            "<ul><li>Nếu bạn không yêu cầu tài khoản này, hoặc thông tin vai trò/cơ sở chưa chính xác, " +
            "vui lòng liên hệ HO hoặc quản trị hệ thống để được hỗ trợ.</li></ul>" +
            "<p>Trân trọng,<br/>PEMS System</p>";

        try
        {
            await _emailService.SendAsync(
                user.Email, "Tài khoản nội bộ PEMS của bạn đã được khởi tạo", html, cancellationToken);
            return "SENT";
        }
        catch
        {
            // Do not fail the request: the account is already created. The caller surfaces
            // FAILED so the operator can re-notify.
            return "FAILED";
        }
    }

    /// <summary>Human-readable role label shown in the account-created email.</summary>
    private static string ResolveRoleDisplayName(string roleCode, string? subRole) => roleCode switch
    {
        RoleCodes.Ho => "Head Office",
        RoleCodes.Admin => "System Administrator",
        RoleCodes.Staff when subRole == UserSubRoles.Leader => "Staff Leader — Trưởng phòng IC",
        RoleCodes.Staff => "IC Staff",
        _ => roleCode
    };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
