using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.DepartmentLeaderPersonnel.Common;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.TransferDepartmentLeadership;

/// <summary>
/// Spec §16 — moves the department head from the caller to one of their staff.
///
/// The actual transfer mechanics (lock users ascending then the department, re-read under lock,
/// unconditionally re-verify the seat, validate the candidate, atomic 3-way write, audit, commit,
/// then revoke both sessions and notify both parties) live in the shared
/// <see cref="IDepartmentLeadershipTransferService"/> — also used by the legacy third-party
/// <c>ReassignDepartmentLeadCommandHandler</c> (SEC-09) so both flows share one, single-sourced
/// implementation instead of risking drift between two.
/// </summary>
public sealed class TransferDepartmentLeadershipCommandHandler
    : IRequestHandler<TransferDepartmentLeadershipCommand, TransferDepartmentLeadershipResponse>
{
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;
    private readonly IDepartmentLeadershipTransferService _transferService;

    public TransferDepartmentLeadershipCommandHandler(
        IDepartmentLeaderPersonnelScopeService scopeService,
        IDepartmentLeadershipTransferService transferService)
    {
        _scopeService = scopeService;
        _transferService = transferService;
    }

    public async Task<TransferDepartmentLeadershipResponse> Handle(
        TransferDepartmentLeadershipCommand request, CancellationToken cancellationToken)
    {
        // Authenticate + verify the caller is the seated head, before anything else. This is also
        // where expectedCurrentLeaderUserId comes from: the scope service just proved
        // scope.ActorUserId == department.HeadUserId, so no second read is needed.
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        var result = await _transferService.TransferAsync(
            scope.DepartmentId,
            expectedCurrentLeaderUserId: scope.ActorUserId,
            newLeaderUserId: request.NewLeaderUserId,
            actorUserId: scope.ActorUserId,
            actorMustBeCurrentLeader: true,
            cancellationToken);

        return new TransferDepartmentLeadershipResponse
        {
            Success = true,
            DepartmentId = result.DepartmentId,
            PreviousLeaderUserId = result.PreviousLeaderUserId,
            PreviousLeaderName = result.PreviousLeaderName,
            NewLeaderUserId = result.NewLeaderUserId,
            NewLeaderName = result.NewLeaderName,
            RevokedSessions = result.RevokedSessions,
            ActorMustSignInAgain = true,
            EmailNotificationStatus = result.EmailNotificationStatus,
            Message = $"Đã chuyển vai trò Trưởng phòng cho {result.NewLeaderName}. "
                      + "Bạn không còn quyền quản lý phòng ban và sẽ được đăng xuất.",
        };
    }
}
