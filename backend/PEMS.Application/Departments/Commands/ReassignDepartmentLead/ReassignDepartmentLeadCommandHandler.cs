using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Common;
using PEMS.Application.DepartmentLeaderPersonnel.Common;

namespace PEMS.Application.Departments.Commands.ReassignDepartmentLead;

/// <summary>
/// SEC-09 remediation. This legacy action used to trust a client-supplied <c>departmentId</c> with no
/// scope check at all, and its own thinner transfer logic (no candidate role/campus validation, no
/// audit, no session revocation, no notification, and a try/catch that swallowed every unexpected
/// exception into a <c>Success=false</c> 200 — SEC-20). It now shares its authorization (StaffLeader
/// own-campus / HO global — Department Lead is out-of-scope here, see
/// <see cref="DepartmentPersonnelManagementScope.EnsureDepartmentInScopeForReassignmentAsync"/>) and its
/// entire transfer mechanics with the canonical self-service flow via
/// <see cref="IDepartmentLeadershipTransferService"/>, so a third-party reassignment now gets the exact
/// same atomicity, candidate validation, audit trail, session revocation and notification.
/// </summary>
public sealed class ReassignDepartmentLeadCommandHandler : IRequestHandler<ReassignDepartmentLeadCommand, ReassignDepartmentLeadResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDepartmentLeadershipTransferService _transferService;

    public ReassignDepartmentLeadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDepartmentLeadershipTransferService transferService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _transferService = transferService;
    }

    public async Task<ReassignDepartmentLeadResponse> Handle(ReassignDepartmentLeadCommand request, CancellationToken cancellationToken)
    {
        var department = await DepartmentPersonnelManagementScope.EnsureDepartmentInScopeForReassignmentAsync(
            _context, _currentUserService, request.DepartmentId, cancellationToken);

        // A distinct, earlier business error from "the seat moved under you" (ConflictException,
        // thrown by the transfer service under lock): a department with nobody currently seated has
        // nothing to hand over from at all.
        if (department.HeadUserId is not { } expectedCurrentLeaderUserId)
            throw new BusinessRuleException("Phòng ban chưa có Trưởng phòng để chuyển giao.");

        var actorUserId = _currentUserService.UserId
            ?? throw new ForbiddenException();

        var result = await _transferService.TransferAsync(
            request.DepartmentId,
            expectedCurrentLeaderUserId,
            request.NewLeaderUserId,
            actorUserId,
            actorMustBeCurrentLeader: false,
            cancellationToken);

        return new ReassignDepartmentLeadResponse
        {
            Success = true,
            Status = "Completed",
            Message = $"Đã chuyển vai trò Trưởng phòng cho {result.NewLeaderName}.",
        };
    }
}
