using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;

namespace PEMS.Application.Departments.Commands.AddDepartmentPersonnel;

/// <summary>
/// P0 #2 CONTAINMENT. The legacy handler was a parallel account-creation path that (a) performed NO
/// actor authorization, (b) created the user directly as <c>ACTIVE</c> — bypassing the shared account
/// rules and the email-ownership proof — and (c) mailed a hardcoded production login
/// link. All three are closed here:
///   1. The actor is authorized against the target department's campus/department scope (403 otherwise).
///   2. Creating a NEW active account directly is refused (422) until the shared confirmation-based
///      provisioning (P0 #1) is wired in; there is no direct-ACTIVE bypass in the meantime.
/// The full re-implementation routes new personnel through the shared pending/confirmation provisioning.
/// </summary>
public sealed class AddDepartmentPersonnelCommandHandler : IRequestHandler<AddDepartmentPersonnelCommand, AddDepartmentPersonnelResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;

    public AddDepartmentPersonnelCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, IEmailService emailService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
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
        {
            return new AddDepartmentPersonnelResponse { Success = false, Message = "Phòng ban không tồn tại hoặc không hoạt động." };
        }

        // Authorize the actor against the department's campus/department scope (403 when out of scope).
        EnsureCanManageDepartmentPersonnel(actorId.Value, department);

        // P0 #2: no direct-ACTIVE creation. New personnel must go through the shared confirmation-based
        // provisioning (P0 #1) — created as PENDING_EMAIL_CONFIRMATION, with a one-time email-ownership
        // token and no effective authority until confirmed. Until that path is wired in for this command,
        // refuse rather than fall back to the unsafe direct-ACTIVE insert.
        throw new BusinessRuleException(
            "Tạo nhân sự phòng ban mới phải đi qua luồng xác nhận email an toàn. " +
            "Tính năng đang được nâng cấp — vui lòng thử lại sau khi luồng xác nhận email được kích hoạt.",
            AddDepartmentPersonnelErrorCodes.RequiresConfirmationProvisioning);
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
}

/// <summary>Stable, machine-readable error codes for the department-personnel provisioning path.</summary>
public static class AddDepartmentPersonnelErrorCodes
{
    public const string RequiresConfirmationProvisioning = "DEPT_PERSONNEL_REQUIRES_CONFIRMATION";
}
