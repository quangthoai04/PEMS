using System.Linq;
using System.Text.Json;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
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
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly IAccountEmailConfirmationService _confirmations;

    public CreateAccountCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IDateTimeService clock,
        AuthOptions options,
        ISystemEmailDispatcher dispatcher,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        IAccountEmailConfirmationService confirmations)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _options = options;
        _dispatcher = dispatcher;
        _notificationService = notificationService;
        _confirmations = confirmations;
    }

    public async Task<CreateAccountResponse> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        // ── Caller authorization runs FIRST, before anything is normalized, read or written.
        //    Account provisioning belongs to HO and Staff Leader; ADMIN is Global Read + Security
        //    Control only (ADMIN_ACCOUNT_MANAGEMENT spec §11). A refused request must leave the
        //    database untouched and must not normalize an email, reserve a slot, issue a
        //    confirmation token, send a mail, write an audit row or raise a notification.
        //    Fail-closed: every role other than the two intended creators is refused here rather
        //    than relying on the dashboard route to hide the screen. ──
        EnsureCallerMayCreateAccounts();

        // Identity is normalized once, then used for every comparison, the insert, the audit entry
        // and the notification email — a direct API call never bypasses the shared rules.
        var email = AccountIdentityRules.NormalizeEmail(request.Email);
        var fullName = AccountIdentityRules.NormalizeFullName(request.FullName);
        if (AccountIdentityRules.ValidateFullName(fullName) is { } nameError)
            throw new ValidationException(nameError);
        if (AccountIdentityRules.ValidateEmail(email) is { } emailError)
            throw new ValidationException(emailError);

        var actorId = _currentUser.UserId;
        var actorCampus = _currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUser.RoleCode);

        // P0 #1 email reuse: if the address belongs to an existing account, only a never-confirmed
        // pending-flow SHELL — created via this flow (has confirmation rows), never activated, currently not
        // ACTIVE, and holding no live token (i.e. CANCELLED / EXPIRED) — may be recycled IN PLACE. Everything
        // (role/campus/department/head-slot) is re-validated below. Any account that was ever ACTIVE/CONFIRMED,
        // or is still awaiting a live confirmation, conflicts exactly as before (no silent reactivation).
        var existingByEmail = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        User? recycleUser = null;
        if (existingByEmail is not null)
        {
            var priorConfirmations = await _db.AccountEmailConfirmations
                .Where(c => c.UserId == existingByEmail.UserId).ToListAsync(cancellationToken);
            var nowForRecycle = _clock.VietnamNow;
            var isRecyclablePendingShell =
                priorConfirmations.Count > 0
                && existingByEmail.Status != UserStatuses.Active
                && !priorConfirmations.Any(c => c.Status == AccountEmailConfirmationStatuses.Confirmed)
                && !priorConfirmations.Any(c =>
                    c.Status == AccountEmailConfirmationStatuses.Pending && c.ExpiresAt >= nowForRecycle);

            if (isRecyclablePendingShell)
                recycleUser = existingByEmail;
            else
                throw new ConflictException(
                    AccountIdentityRules.EmailAlreadyUsedMessage, AccountErrorCodes.EmailAlreadyExists);
        }

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
        else
        {
            // ── UC-96-SL Staff Leader scope: campus is forced to the leader's own campus,
            //    and only STAFF/STAFF, DEPARTMENT/LEADER or STUDENT may be created. This is the only
            //    other caller EnsureCallerMayCreateAccounts lets through. ──
            if (actorCampus is null)
                throw new ForbiddenException(
                    "Tài khoản của bạn chưa được gán cơ sở nên không thể tạo tài khoản.");

            shape = await AccountProvisioningRules.ResolveStaffLeaderTargetAsync(
                _db, request.RoleCode, request.DepartmentId, actorCampus.Value, cancellationToken);
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

        // ── student_code (MSSV): required, trimmed, ≤30 and unique for a STUDENT; ignored for any
        //    other role. Early-checked so the caller gets a friendly message instead of a raw
        //    uq_users_student_code violation. ──
        string? resolvedStudentCode = null;
        if (shape.RoleCode == RoleCodes.Student)
        {
            var code = (request.StudentCode ?? string.Empty).Trim();
            if (code.Length == 0)
                throw new ValidationException("Vui lòng nhập mã số sinh viên.");
            if (code.Length > 30)
                throw new ValidationException("Mã số sinh viên không được vượt quá 30 ký tự.");
            // Exclude the recycled shell itself so its own previous student_code is not a false conflict.
            var studentCodeTaken = recycleUser is null
                ? await _db.Users.AsNoTracking().AnyAsync(u => u.StudentCode == code, cancellationToken)
                : await _db.Users.AsNoTracking().AnyAsync(u => u.StudentCode == code && u.UserId != recycleUser.UserId, cancellationToken);
            if (studentCodeTaken)
                throw new ConflictException("Mã số sinh viên này đã được sử dụng bởi tài khoản khác.");
            resolvedStudentCode = code;
        }

        var now = _clock.VietnamNow;
        // New account, or the recycled pending shell — either way (re)provisioned from scratch below.
        var user = recycleUser ?? new User();
        user.FullName = fullName;
        user.Email = email;
        user.Phone = Clean(request.Phone);
        user.Gender = request.Gender;
        user.Nationality = Clean(request.Nationality);
        user.StudentCode = resolvedStudentCode;
        user.RoleId = shape.RoleId;
        user.SubRole = shape.SubRole;
        user.PrimaryCampusId = shape.PrimaryCampusId;
        user.DepartmentId = shape.DepartmentId;
        // P0 #1: (re)start UNCONFIRMED — it cannot log in (password/SSO/refresh all reject a non-active status)
        // and holds no effective authority. A Head slot may still be reserved below; the reservation grants
        // nothing until the owner confirms their email and the account activates.
        user.Status = UserStatuses.PendingEmailConfirmation;
        user.CreatedVia = CreatedViaValues.ManualCreated;
        user.CreatedAt = now;               // restart the confirmation window
        user.CreatedBy = actorId;
        if (recycleUser is not null)
        {
            user.UpdatedAt = now;
            user.UpdatedBy = actorId;
            user.PasswordHash = null;       // drop any stale credential from the previous attempt
        }

        var passwordSet = false;
        if (!string.IsNullOrEmpty(request.Password))
        {
            if (!_options.PasswordLoginEnabled)
                throw new BusinessRuleException(
                    "Temporary passwords are disabled in this environment. The user signs in via Google SSO.");
            if (!PasswordPolicy.IsStrong(request.Password))
                throw new ValidationException(PasswordPolicy.RequirementsMessage);

            user.PasswordHash = _passwordHasher.Hash(request.Password);
            user.AuthProviders.Add(new UserAuthProvider
            {
                // AuthProviderId is DB-generated; UserId is set via the navigation on save.
                ProviderType = ProviderTypes.LocalPassword,
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

        // The raw confirmation token — kept in memory only, embedded into the emailed link after commit.
        var confirmationToken = string.Empty;
        try
        {
            // Re-check the campus has no HO right before inserting (concurrency guard).
            if (enforcesUniqueHoPerCampus
                && await _db.Users.AnyAsync(u =>
                    u.PrimaryCampusId == hoCampusId && u.Role.RoleCode == RoleCodes.Ho, cancellationToken))
                throw new ConflictException(
                    "Cơ sở này đã có tài khoản Head Office. Vui lòng chọn cơ sở khác hoặc quản lý tài khoản HO hiện có.",
                    AccountErrorCodes.CampusHoAlreadyActive);

            if (recycleUser is null)
            {
                _db.Users.Add(user);
            }
            else
            {
                // Clear any stale Head slot the shell still held so the re-validation below starts clean.
                foreach (var camp in await _db.Campuses.Where(c => c.IcHeadUserId == user.UserId).ToListAsync(cancellationToken))
                {
                    camp.IcHeadUserId = null;
                    camp.UpdatedBy = actorId;
                    camp.UpdatedAt = now;
                }
                foreach (var dep in await _db.Departments.Where(d => d.HeadUserId == user.UserId).ToListAsync(cancellationToken))
                {
                    dep.HeadUserId = null;
                    dep.UpdatedBy = actorId;
                    dep.UpdatedAt = now;
                }
            }
            await _db.SaveChangesAsync(cancellationToken);

            // P0 #1: persist the email-ownership proof atomically with the account. Only the hash is stored;
            // the raw token is returned for the confirmation link and never written to the DB or the log.
            confirmationToken = await _confirmations.IssuePendingAsync(
                user.UserId, email, isResend: false, cancellationToken);

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
                Action = recycleUser is null ? "CREATE_ACCOUNT" : "RECREATE_PENDING_ACCOUNT",
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

            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: user.UserId,
                    Title: "Tài khoản được tạo",
                    Message: $"Tài khoản PEMS của bạn đã được khởi tạo thành công với vai trò {shape.RoleCode}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.AccountCreated,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.Account,
                    RelatedId: user.UserId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Account,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenAccountDetail,
                    ActionUrl: "/dashboard/accounts"),
                cancellationToken
            );

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── P0 #1: email the confirmation link (NOT a welcome — welcome is sent only after the owner
        //    confirms). Failure/skip does not roll back the already-committed pending account; we report
        //    it truthfully via EmailNotificationStatus so the operator can resend. ──
        var emailStatus = await SendConfirmationEmailAsync(
            user, shape, _confirmations.BuildConfirmUrl(confirmationToken), actorId, cancellationToken);

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

    private async Task<string> SendConfirmationEmailAsync(
        User user, AccountProvisioningRules.ResolvedShape shape, string confirmUrl,
        ulong? actorId, CancellationToken cancellationToken)
    {
        // This handler used to compose its own version of the confirmation mail — a second copy of the
        // message the resend/edit paths sent, which had already drifted from it. There is now one
        // template in the database and one code that names it.
        try
        {
            // TRUTHFUL status: only report SENT when the provider accepted the message. A disabled SMTP in
            // dev/testing is SKIPPED (not "sent"); a provider/prod failure is FAILED. The account is already
            // committed (pending), so a non-Sent outcome never rolls it back — the operator can resend.
            var result = await _dispatcher.SendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountEmailConfirmation,
                new EmailRecipient(user.Email, user.FullName),
                await AccountEmailVariables.ForConfirmationAsync(
                    _db, user.FullName, shape.RoleCode, shape.SubRole,
                    shape.PrimaryCampusId, _confirmations.ExpiryHours, cancellationToken),
                TrustedBlocks: AccountEmailVariables.ConfirmationBlocks(confirmUrl),
                RelatedType: "User",
                RelatedId: user.UserId,
                SentBy: actorId), cancellationToken);

            return result.NotificationStatus;
        }
        catch
        {
            return "FAILED";
        }
    }

    /// <summary>
    /// The two roles that provision accounts: HO and a Staff Leader (STAFF/LEADER). ADMIN is refused
    /// with its own code so the client can say WHY rather than showing a generic "no permission";
    /// every other role gets the generic refusal. Runs before any read or write, so a refused caller
    /// leaves no trace beyond the 403 itself.
    /// </summary>
    private void EnsureCallerMayCreateAccounts()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new AuthBusinessException(
                AccountErrorCodes.AccountListForbidden,
                "Bạn cần đăng nhập để thực hiện thao tác này.", 401);

        if (_currentUser.RoleCode == RoleCodes.Admin)
            throw new AuthBusinessException(
                AccountErrorCodes.AdminAccountCreationNotAllowed,
                "ADMIN chỉ được xem tài khoản và xử lý khóa bảo mật; không được tạo tài khoản.", 403);

        if (_currentUser.RoleCode == RoleCodes.Ho)
            return;

        if (_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader)
            return;

        throw new ForbiddenException("Bạn không có quyền tạo tài khoản.");
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
