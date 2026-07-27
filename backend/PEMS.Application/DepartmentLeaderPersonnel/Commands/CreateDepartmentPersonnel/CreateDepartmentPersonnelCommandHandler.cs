using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.CreateDepartmentPersonnel;

/// <summary>
/// Spec §10 — creates a <c>DEPARTMENT + STAFF</c> account in the caller's own department.
///
/// Two invariants drive the shape of this handler:
///  • the server owns the structural fields. Role, sub-role, department, campus, status and
///    created_via come from the verified scope, so the client cannot promote the account to Leader,
///    place it in another department, or have it born ACTIVE (spec §10);
///  • the account is created PENDING_EMAIL_CONFIRMATION and holds no authority until its owner proves
///    ownership of the address. A failed confirmation email therefore does NOT roll the account back —
///    the operator resends instead (spec §10 "Email").
/// </summary>
public sealed class CreateDepartmentPersonnelCommandHandler
    : IRequestHandler<CreateDepartmentPersonnelCommand, CreateDepartmentPersonnelResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;
    private readonly IUserMutationLockService _lockService;
    private readonly IAccountEmailConfirmationService _confirmations;
    private readonly IEmailService _emailService;
    private readonly IDateTimeService _clock;

    public CreateDepartmentPersonnelCommandHandler(
        IApplicationDbContext db,
        IDepartmentLeaderPersonnelScopeService scopeService,
        IUserMutationLockService lockService,
        IAccountEmailConfirmationService confirmations,
        IEmailService emailService,
        IDateTimeService clock)
    {
        _db = db;
        _scopeService = scopeService;
        _lockService = lockService;
        _confirmations = confirmations;
        _emailService = emailService;
        _clock = clock;
    }

    public async Task<CreateDepartmentPersonnelResponse> Handle(
        CreateDepartmentPersonnelCommand request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        // Re-validate on the NORMALIZED values. The validator already ran, but a handler that trusts
        // it would be one MediatR wiring change away from accepting anything.
        var fullName = AccountIdentityRules.NormalizeFullName(request.FullName);
        if (AccountIdentityRules.ValidateFullName(fullName) is { } nameError)
            throw new ValidationException(nameError);

        var email = AccountIdentityRules.NormalizeEmail(request.Email);
        if (AccountIdentityRules.ValidateEmail(email) is { } emailError)
            throw new ValidationException(emailError);

        var phone = DepartmentPersonnelPhoneRules.Normalize(request.Phone);
        if (DepartmentPersonnelPhoneRules.Validate(phone) is { } phoneError)
            throw new ValidationException(phoneError);

        var gender = DepartmentPersonnelGenders.Parse(request.Gender);

        var departmentRoleId = await _db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department)
            .Select(r => r.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (departmentRoleId == 0)
            throw new BusinessRuleException(
                "Hệ thống chưa cấu hình vai trò phòng ban.", DepartmentLeaderErrorCodes.DepartmentContextMissing);

        var now = _clock.VietnamNow;
        User user;
        var confirmationToken = string.Empty;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize against a concurrent leadership transfer / department disable: both take the
            // same department lock, so this insert either sees the department still ACTIVE and headed
            // by the caller, or blocks until it can observe the committed change.
            await _lockService.LockDepartmentsAsync(new[] { scope.DepartmentId }, cancellationToken);

            var department = await _db.Departments
                .Include(d => d.Campus)
                .FirstAsync(d => d.DepartmentId == scope.DepartmentId, cancellationToken);

            // Re-read under the lock — the state the gate saw may be a few milliseconds stale.
            if (department.Status != EntityStatuses.Active)
                throw new BusinessRuleException(
                    "Phòng ban đã ngừng hoạt động nên không thể thêm nhân sự.",
                    DepartmentLeaderErrorCodes.DepartmentNotActive);

            if (department.Campus.Status != EntityStatuses.Active)
                throw new BusinessRuleException(
                    "Cơ sở đã ngừng hoạt động nên không thể thêm nhân sự.",
                    DepartmentLeaderErrorCodes.DepartmentNotActive);

            if (department.HeadUserId != scope.ActorUserId)
                throw new AuthBusinessException(
                    DepartmentLeaderErrorCodes.DepartmentScopeForbidden,
                    "Bạn không còn là Trưởng phòng của phòng ban này.", 403);

            // Application-level uniqueness check; the unique index on users.email remains the final
            // guard for a race that slips between this read and the insert.
            if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
                throw new ConflictException(
                    AccountIdentityRules.EmailAlreadyUsedMessage,
                    DepartmentLeaderErrorCodes.AccountEmailAlreadyExists);

            user = new User
            {
                FullName = fullName,
                Email = email,
                Phone = phone,
                Gender = gender,
                // ── Server-assigned. None of these come from the request (spec §10). ──
                RoleId = departmentRoleId,
                SubRole = UserSubRoles.Staff,           // a Leader is never created here
                DepartmentId = department.DepartmentId,
                PrimaryCampusId = department.CampusId,
                Status = UserStatuses.PendingEmailConfirmation,
                CreatedVia = CreatedViaValues.ManualCreated,
                CreatedAt = now,
                CreatedBy = scope.ActorUserId,
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);   // materializes user_id for the token row

            // Only the token HASH is persisted; the raw value exists solely inside the emailed link.
            confirmationToken = await _confirmations.IssuePendingAsync(
                user.UserId, email, isResend: false, cancellationToken);

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = scope.ActorUserId,
                CampusId = department.CampusId,
                Action = DepartmentPersonnelAuditActions.AddPersonnel,
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "Personnel",
                        OldValueText = null,
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            fullName,
                            email = DepartmentPersonnelAudit.MaskEmail(email),
                            phone,
                            gender = DepartmentPersonnelGenders.ToWire(gender),
                            roleCode = RoleCodes.Department,
                            subRole = UserSubRoles.Staff,
                            departmentId = department.DepartmentId,
                            campusId = department.CampusId,
                            status = UserStatuses.PendingEmailConfirmation,
                        }),
                    },
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── After commit. A delivery failure leaves the PENDING account in place on purpose: the
        //    operator resends rather than losing the record (spec §10). ──
        var delivery = await _emailService.TrySendAsync(
            email,
            AccountConfirmationEmail.Subject,
            AccountConfirmationEmail.BuildHtml(fullName, _confirmations.BuildConfirmUrl(confirmationToken)),
            cancellationToken);

        var deliveryStatus = AccountConfirmationEmail.MapStatus(delivery.Status);

        return new CreateDepartmentPersonnelResponse
        {
            Success = true,
            UserId = user.UserId,
            FullName = fullName,
            Email = email,
            Phone = phone,
            Gender = DepartmentPersonnelGenders.ToWire(gender),
            Status = UserStatuses.PendingEmailConfirmation,
            SubRole = UserSubRoles.Staff,
            EmailNotificationStatus = deliveryStatus,
            Message = deliveryStatus == DepartmentPersonnelEmails.StatusSent
                ? "Đã tạo nhân sự và gửi email xác nhận. Tài khoản sẽ hoạt động sau khi nhân sự xác nhận email."
                : "Đã tạo nhân sự nhưng chưa gửi được email xác nhận. Vui lòng dùng chức năng gửi lại email xác nhận.",
        };
    }
}
