using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Departments.Commands.AddDepartmentPersonnel;

/// <summary>
/// P0 #2 — provisions a new DEPARTMENT personnel account through the SHARED confirmation-based flow (no
/// second confirmation flow of its own):
///   1. authorizes the actor against the department's campus/department scope (403 otherwise);
///   2. validates the identity and shape via the shared <see cref="AccountProvisioningRules"/>;
///   3. creates the account <c>PENDING_EMAIL_CONFIRMATION</c> (never a direct ACTIVE bypass), reserving the
///      department-head slot for a Leader;
///   4. issues a one-time confirmation token via the shared <see cref="IAccountEmailConfirmationService"/>
///      and emails the confirmation link (built from configured base URLs — no hardcoded domain), reporting
///      the delivery outcome truthfully.
/// The account holds no effective authority until its owner confirms the email and it activates.
/// </summary>
public sealed class AddDepartmentPersonnelCommandHandler : IRequestHandler<AddDepartmentPersonnelCommand, AddDepartmentPersonnelResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IAccountEmailConfirmationService _confirmations;
    private readonly IDateTimeService _clock;

    public AddDepartmentPersonnelCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISystemEmailDispatcher dispatcher,
        IAccountEmailConfirmationService confirmations,
        IDateTimeService clock)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dispatcher = dispatcher;
        _confirmations = confirmations;
        _clock = clock;
    }

    public async Task<AddDepartmentPersonnelResponse> Handle(AddDepartmentPersonnelCommand request, CancellationToken cancellationToken)
    {
        // Authenticate first, so an anonymous caller can never probe department existence or scope.
        var actorId = _currentUserService.UserId;
        if (actorId is null || !_currentUserService.IsAuthenticated)
            throw new ForbiddenException("Bạn cần đăng nhập để thực hiện thao tác này.");

        var department = await _context.Departments
            .Include(d => d.Campus)
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken);

        if (department == null || department.Status != EntityStatuses.Active)
            return new AddDepartmentPersonnelResponse { Success = false, Message = "Phòng ban không tồn tại hoặc không hoạt động." };

        // Authorize the actor against the department's campus/department scope (403 when out of scope).
        EnsureCanManageDepartmentPersonnel(actorId.Value, department);

        var email = AccountIdentityRules.NormalizeEmail(request.Email);
        var fullName = AccountIdentityRules.NormalizeFullName(request.FullName);
        if (AccountIdentityRules.ValidateFullName(fullName) is { } nameError)
            throw new ValidationException(nameError);
        if (AccountIdentityRules.ValidateEmail(email) is { } emailError)
            throw new ValidationException(emailError);

        if (await _context.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException(
                AccountIdentityRules.EmailAlreadyUsedMessage, AccountErrorCodes.EmailAlreadyExists);

        // "Trưởng phòng" → department head (LEADER); anyone else is a department staff member.
        var subRole = request.Role == "Trưởng phòng" ? UserSubRoles.Leader : UserSubRoles.Staff;

        // Shared shape validation (mirrors the DB triggers): DEPARTMENT must be a GENERAL department in scope.
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUserService.RoleCode);
        var shape = await AccountProvisioningRules.ResolveAsync(
            _context, RoleCodes.Department, subRole, department.CampusId, request.DepartmentId,
            privileged, _currentUserService.PrimaryCampusId, cancellationToken);

        // A Leader reserves the department-head slot — refuse if the department already has a different head.
        var reservesDepartmentHead = shape.SubRole == UserSubRoles.Leader;
        if (reservesDepartmentHead && department.HeadUserId is not null)
            throw new ConflictException(
                "Phòng ban này đã có trưởng phòng. Vui lòng bỏ gán trưởng phòng hiện tại trước khi chỉ định người mới.");

        var now = _clock.VietnamNow;
        var user = new User
        {
            FullName = fullName,
            Email = email,
            Phone = Clean(request.Phone),
            Gender = request.Gender,
            RoleId = shape.RoleId,
            SubRole = shape.SubRole,
            PrimaryCampusId = shape.PrimaryCampusId,
            DepartmentId = shape.DepartmentId,
            // P0 #1/#2: created UNCONFIRMED — cannot log in, no effective authority; the department-head slot
            // reserved below grants nothing until the owner confirms their email and the account activates.
            Status = UserStatuses.PendingEmailConfirmation,
            CreatedVia = CreatedViaValues.ManualCreated,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        var confirmationToken = string.Empty;
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            confirmationToken = await _confirmations.IssuePendingAsync(user.UserId, email, isResend: false, cancellationToken);

            if (reservesDepartmentHead)
            {
                department.HeadUserId = user.UserId;
                department.UpdatedBy = actorId;
                department.UpdatedAt = now;
            }

            _context.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = department.CampusId,
                Action = "ADD_DEPARTMENT_PERSONNEL",
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>(),
                CreatedAt = now,
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Send the confirmation link (truthful outcome), strictly after the commit above. A non-Sent result
        // never rolls back the pending account — the operator can resend.
        var result = await _dispatcher.SendAsync(new SystemEmailRequest(
            SystemEmailTemplates.AccountEmailConfirmation,
            new EmailRecipient(email, fullName),
            // The role is not read back from the database here because the shape resolved above IS what was
            // just written: a DEPARTMENT account with this sub-role and campus.
            await AccountEmailVariables.ForConfirmationAsync(
                _context, fullName, RoleCodes.Department, shape.SubRole,
                shape.PrimaryCampusId, _confirmations.ExpiryHours, cancellationToken),
            TrustedBlocks: AccountEmailVariables.ConfirmationBlocks(_confirmations.BuildConfirmUrl(confirmationToken)),
            RelatedType: "User",
            RelatedId: user.UserId,
            SentBy: actorId), cancellationToken);

        return new AddDepartmentPersonnelResponse
        {
            Success = true,
            Message = "Đã tạo nhân sự phòng ban; tài khoản đang chờ xác nhận email.",
            UserId = user.UserId,
            EmailNotificationStatus = result.NotificationStatus,
        };
    }

    /// <summary>
    /// Only these actors may add personnel to <paramref name="department"/>:
    ///  • HO or Staff Leader (IC head) of the SAME campus — full campus authority; or
    ///  • the Department Leader who heads THIS specific department.
    /// Anyone else — including a campus-mismatched leader or a department leader of a different
    /// department — is refused with 403.
    /// </summary>
    private void EnsureCanManageDepartmentPersonnel(ulong actorId, Department department)
    {
        var role = _currentUserService.RoleCode;
        var subRole = _currentUserService.SubRole;
        var actorCampus = _currentUserService.PrimaryCampusId;

        var isCampusAuthority =
            (role == RoleCodes.Ho || (role == RoleCodes.Staff && subRole == UserSubRoles.Leader))
            && actorCampus == department.CampusId;

        var isThisDepartmentHead =
            role == RoleCodes.Department && subRole == UserSubRoles.Leader
            && actorCampus == department.CampusId
            && department.HeadUserId == actorId;

        if (!isCampusAuthority && !isThisDepartmentHead)
            throw new ForbiddenException("Bạn không có quyền thêm nhân sự cho phòng ban này.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
