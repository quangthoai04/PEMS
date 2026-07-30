using System.Collections.Generic;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
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
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IUserMutationLockService _lockService;
    private readonly IPendingAccountEmailChangeService _pendingEmailChange;
    private readonly IAccountEmailConfirmationService _confirmations;

    public UpdateAccountRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        IDateTimeService clock,
        ISystemEmailDispatcher dispatcher,
        IUserMutationLockService lockService,
        IPendingAccountEmailChangeService pendingEmailChange,
        IAccountEmailConfirmationService confirmations)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _clock = clock;
        _dispatcher = dispatcher;
        _lockService = lockService;
        _pendingEmailChange = pendingEmailChange;
        _confirmations = confirmations;
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
        var emailChanged = resolvedEmail != oldEmail;
        var hasIdentityChange = resolvedFullName != oldFullName || emailChanged;
        var hasStudentCodeChange = resolvedStudentCode != oldStudentCode;

        // Read BEFORE anything is written, and from the database rather than the request: what the
        // account's address change means depends entirely on whether it had ever confirmed one.
        var wasPending = user.Status == UserStatuses.PendingEmailConfirmation;

        // Only a genuine role/sub-role move justifies ACCOUNT_ROLE_CHANGED — that mail says nothing
        // but "your role went from X to Y", so sending it for an MSSV correction or a department move
        // would tell the holder their role changed when it did not. Deliberately NARROWER than
        // hasStructuralChange, which also fires on a department/campus move: those resolve to the SAME
        // role label, so the mail would read "từ Trưởng phòng ban sang Trưởng phòng ban". And
        // deliberately not hasAnyChange, which would mail a role notice for a renamed account.
        //
        // The account's STATUS is not part of this: whether it has confirmed its address decides which
        // confirmation flow runs, never whether the holder is told their role moved.
        var roleChanged = oldRoleCode != shape.RoleCode || oldSubRole != shape.SubRole;
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
        //
        // The address change is applied FIRST and in this same transaction, because for a pending
        // account it is not a field assignment: it re-points the authentication bindings and issues a
        // confirmation token that supersedes the live one. Doing that through a second API call after
        // the role change — the obvious-looking alternative — is what produces the half-applied states
        // this flow must not have: a role that moved while the only live activation link still points
        // at the old address, or an address that moved while the role did not. Both commit here or
        // neither does.
        string? pendingConfirmationToken = null;
        if (emailChanged && wasPending)
        {
            // The name travels with it when it is actually being edited — the confirmation email
            // greets the holder by name, and a rename saved a moment later by a separate statement
            // could miss that mail entirely. Passing null when it is NOT being edited is deliberate:
            // an existing name that predates today's rules must not block an address correction it has
            // nothing to do with.
            var change = await _pendingEmailChange.PrepareAsync(
                user, resolvedEmail,
                resolvedFullName == oldFullName ? null : resolvedFullName,
                cancellationToken);
            pendingConfirmationToken = change.RawToken;
        }
        else
        {
            user.FullName = resolvedFullName;
            user.Email = resolvedEmail;

            // An account that HAS proven an address keeps its bindings pointed at the current one and
            // must re-verify: same policy as the HO basic-info edit, applied from the one shared place.
            if (emailChanged)
            {
                await AccountAuthProviderSync.RepointAsync(_db, user.UserId, resolvedEmail, cancellationToken);
                user.EmailVerifiedAt = null;
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
        await transaction.CommitAsync(cancellationToken);

        // ── Post-commit side effects, in this order. Neither may run for a refused request, and a
        //    failure here must not undo a committed role change (spec §12.5). ──
        //
        // Sessions go regardless of which field moved: a role change strips permissions the live
        // tokens were issued against, and an email change means the identity those tokens were issued
        // to no longer signs in here. The reason names whichever one drove it.
        var revoked = await _sessionService.RevokeAllActiveSessionsAsync(
            user.UserId,
            emailChanged && !roleChanged
                ? SessionRevokeReasons.AccountEmailChanged
                : SessionRevokeReasons.RoleChanged,
            actorId, cancellationToken);

        // Each mail is dispatched and reported on its own. They answer different questions — "can this
        // account be activated?" and "does the holder know their role moved?" — so neither may be
        // skipped because the other is due, and neither's failure may be reported as the other's.
        var confirmationEmailStatus = "NOT_REQUIRED";
        var emailNotificationStatus = "NOT_REQUIRED";
        var pendingEmailChanged = emailChanged && wasPending;

        if (pendingEmailChanged)
        {
            // The account has still never proven an address, so the NEW one gets the activation mail
            // — the same one the create flow sends — describing the account AS IT NOW IS.
            confirmationEmailStatus = await SendPendingConfirmationAsync(
                user, shape, pendingConfirmationToken!, oldEmail, actorId, cancellationToken);
            emailNotificationStatus = confirmationEmailStatus;
        }
        else if (emailChanged)
        {
            emailNotificationStatus = await SendEmailChangeNoticesAsync(
                user, oldEmail, actorId, cancellationToken);
        }

        // ── UC-100-SL BR-100SL-08: the holder is told their role moved. Non-fatal, and NOT conditional
        //    on the account's status. A pending account was skipped here before, on the theory that its
        //    activation mail already names the final role — but that mail only exists when the address
        //    moved too, so re-roling a pending account changed its permissions and told nobody. The
        //    role change is a fact about the account regardless of whether it can log in yet. ──
        var roleChangeEmailStatus = "NOT_REQUIRED";
        if (roleChanged)
            roleChangeEmailStatus = await SendRoleChangedNotificationAsync(
                user, shape, oldRoleCode, oldSubRole, actorId, cancellationToken);

        return new UpdateAccountRoleResponse
        {
            UserId = user.UserId,
            RoleCode = shape.RoleCode,
            PrimaryCampusId = shape.PrimaryCampusId,
            RevokedSessions = revoked,
            EmailChanged = emailChanged,
            RoleChanged = roleChanged,
            // The status the account was in when the request arrived — and still is: nothing in this
            // flow activates an account.
            RequiresEmailConfirmation = wasPending,
            EmailNotificationStatus = emailNotificationStatus,
            ConfirmationEmailNotificationStatus = confirmationEmailStatus,
            RoleChangeEmailNotificationStatus = roleChangeEmailStatus,
            Message = BuildMessage(emailChanged, wasPending),
        };
    }

    private static string BuildMessage(bool emailChanged, bool wasPending) => (emailChanged, wasPending) switch
    {
        (true, true) => "Đã cập nhật tài khoản. Tài khoản vẫn ở trạng thái chờ xác nhận email cho đến khi "
            + "người nhận hoàn tất xác nhận tại địa chỉ mới.",
        (true, false) => "Đã cập nhật tài khoản. Email đăng nhập đã thay đổi và các phiên hiện tại đã bị thu hồi.",
        // Still pending: telling this holder to "đăng nhập lại" would be telling them to do something
        // they have never been able to do.
        (false, true) => "Đã cập nhật tài khoản. Tài khoản vẫn ở trạng thái chờ xác nhận email.",
        _ => "Cập nhật tài khoản thành công. Người dùng cần đăng nhập lại qua cổng nội bộ.",
    };

    /// <summary>
    /// Mails the activation link to a pending account's NEW address, then a neutral notice to the old
    /// one. Returns the delivery outcome of the FIRST only: that is the mail which decides whether this
    /// account can ever be activated, and the notice to an address that may have been a stranger's typo
    /// must never be able to downgrade it. Both are best-effort — the account is already committed and
    /// correct, and "Gửi lại email xác nhận" is the way back from a failed send.
    /// </summary>
    private async Task<string> SendPendingConfirmationAsync(
        User user, AccountProvisioningRules.ResolvedShape shape, string rawToken,
        string oldEmail, ulong? actorId, CancellationToken cancellationToken)
    {
        var status = "FAILED";
        try
        {
            // Every variable is read from the RESOLVED shape, never the pre-change snapshot: when a
            // role and an email move together, the confirmation must state the role the holder is
            // actually being activated into.
            var result = await _dispatcher.SendAsync(
                await PendingAccountEmailChangeMails.BuildConfirmationAsync(
                    _db, _confirmations, user.UserId, user.FullName, shape.RoleCode, shape.SubRole,
                    shape.PrimaryCampusId, user.Email, rawToken, actorId, cancellationToken),
                cancellationToken);
            status = result.NotificationStatus;
        }
        catch
        {
            // Reported as FAILED rather than thrown: the caller must be told the account WAS updated,
            // which a 500 would hide.
        }

        try
        {
            await _dispatcher.SendAsync(
                PendingAccountEmailChangeMails.BuildOldAddressNotice(user.UserId, oldEmail, actorId),
                cancellationToken);
        }
        catch { /* notice is best-effort */ }

        return status;
    }

    /// <summary>
    /// The pair of notices an account that HAS confirmed an address gets when that address moves: a
    /// removal notice to the old one, a change notice to the new one. Both matter to the holder, so
    /// the reported status covers both — SENT / SKIPPED (mail off in this environment) / FAILED when
    /// they agree, PARTIAL when they do not. Same messages and same anonymity policy as the HO
    /// basic-info flow.
    /// </summary>
    private async Task<string> SendEmailChangeNoticesAsync(
        User user, string oldEmail, ulong? actorId, CancellationToken cancellationToken)
    {
        // Anonymous by design: the address being unlinked may belong to somebody with no connection to
        // this account, so it learns that a change happened and nothing else.
        var oldStatus = await TrySendAsync(new SystemEmailRequest(
            SystemEmailTemplates.AccountEmailChangedOldNotice,
            new EmailRecipient(oldEmail),
            new Dictionary<string, string>(),
            RelatedType: "User",
            RelatedId: user.UserId,
            SentBy: actorId), cancellationToken);

        var newStatus = await TrySendAsync(new SystemEmailRequest(
            SystemEmailTemplates.AccountEmailChangedNewNotice,
            new EmailRecipient(user.Email, user.FullName),
            new Dictionary<string, string>
            {
                ["fullName"] = user.FullName,
                // Masked, not in full: the holder is entitled to know WHICH address was unlinked, and
                // seeing part of it is enough to recognise it.
                ["oldEmailMasked"] = AccountEmailVariables.MaskEmail(oldEmail),
            },
            RelatedType: "User",
            RelatedId: user.UserId,
            SentBy: actorId), cancellationToken);

        return oldStatus == newStatus ? oldStatus : "PARTIAL";
    }

    /// <summary>
    /// Sends one message and reports what became of it — SENT / SKIPPED / FAILED — without ever
    /// throwing. SKIPPED is kept distinct from SENT on purpose: a message the environment declined to
    /// send did not reach anybody, and reporting it as delivered would send the caller away waiting on
    /// a mail that cannot arrive.
    /// </summary>
    private async Task<string> TrySendAsync(SystemEmailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dispatcher.SendAsync(request, cancellationToken);
            return result.NotificationStatus;
        }
        catch
        {
            return "FAILED";   // non-fatal: the account is already committed and must not roll back
        }
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

    /// <summary>
    /// Tells the holder their role moved, and reports what became of that message — SENT / SKIPPED /
    /// FAILED — rather than swallowing the outcome. The account is already committed, so the send is
    /// best-effort; but a caller told "đã gửi email thông báo" over a message nobody received has no
    /// way to know the holder is still working from the old role.
    ///
    /// <para>
    /// Every value is read AFTER the mutation: <c>user.Email</c> is the new address when the request
    /// moved it (a notice sent to the address the holder just left is a notice they never see), and the
    /// role labels bracket the change — the old shape from the pre-change snapshot, the new one from the
    /// resolved shape, never from whatever the caller's screen happened to be showing.
    /// </para>
    /// </summary>
    private async Task<string> SendRoleChangedNotificationAsync(
        User user, AccountProvisioningRules.ResolvedShape shape,
        string oldRoleCode, string? oldSubRole, ulong? actorId, CancellationToken cancellationToken)
    {
        var campusName = shape.PrimaryCampusId is null
            ? null
            : await _db.Campuses.AsNoTracking()
                .Where(c => c.CampusId == shape.PrimaryCampusId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);

        return await TrySendAsync(new SystemEmailRequest(
            SystemEmailTemplates.AccountRoleChanged,
            new EmailRecipient(user.Email, user.FullName),
            new Dictionary<string, string>
            {
                ["fullName"] = user.FullName,
                // Both sides are stated: "your role changed" is not actionable without saying from
                // what. The labels are the same ones the account screens show.
                ["oldRoleName"] = ResolveRoleDisplayName(oldRoleCode, oldSubRole),
                ["newRoleName"] = ResolveRoleDisplayName(shape.RoleCode, shape.SubRole),
                ["campusName"] = string.IsNullOrWhiteSpace(campusName) ? "—" : campusName,
            },
            RelatedType: "User",
            RelatedId: user.UserId,
            SentBy: actorId), cancellationToken);
    }

    /// <summary>Human-readable role label shown in the role-changed email.</summary>
    private static string ResolveRoleDisplayName(string roleCode, string? subRole)
        => AccountRoleDisplayNames.Resolve(roleCode, subRole);
}
