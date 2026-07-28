using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

using PEMS.Application.Common;
namespace PEMS.Application.Departments.Commands.ReassignDepartmentLead;

/// <summary>
/// The one sanctioned way to hand a GENERAL department over to a new head. UpdateAccountRole
/// deliberately refuses to clear <c>departments.head_user_id</c> on its own (spec §8.6/§15), so a
/// Department Leader who is still the head must be replaced here first — after which the role change
/// re-runs its remaining blockers (host / coordinator / participant / logistics) as usual.
///
/// Everything happens inside one transaction that locks the department and both users up front
/// (see <see cref="IUserMutationLockService"/>), always users-before-departments and ascending by id,
/// so the handover can neither interleave with a concurrent role change nor leave the department
/// with two heads or none.
/// </summary>
public sealed class ReassignDepartmentLeadCommandHandler : IRequestHandler<ReassignDepartmentLeadCommand, ReassignDepartmentLeadResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserMutationLockService _lockService;

    public ReassignDepartmentLeadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IUserMutationLockService lockService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _lockService = lockService;
    }

    public async Task<ReassignDepartmentLeadResponse> Handle(ReassignDepartmentLeadCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            // Lock the incoming head before reading anything about them; the outgoing head is only
            // known after the department row is read, so it is locked as soon as it is.
            await _lockService.LockUsersAsync(new[] { request.NewLeaderUserId }, cancellationToken);
            await _lockService.LockDepartmentsAsync(new[] { request.DepartmentId }, cancellationToken);

            var dept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken);
            if (dept == null) return new ReassignDepartmentLeadResponse { Success = false, Message = "Phòng ban không tồn tại." };

            var oldLeaderId = dept.HeadUserId;
            if (oldLeaderId.HasValue && oldLeaderId.Value != request.NewLeaderUserId)
                await _lockService.LockUsersAsync(new[] { oldLeaderId.Value }, cancellationToken);

            // Re-read AFTER the lock so a role change that just committed is visible.
            var newLeader = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.NewLeaderUserId && u.DepartmentId == request.DepartmentId && u.Status == "ACTIVE", cancellationToken);
            if (newLeader == null) return new ReassignDepartmentLeadResponse { Success = false, Message = "Người được chọn không hợp lệ." };

            if (oldLeaderId != request.NewLeaderUserId)
            {
                if (oldLeaderId.HasValue)
                {
                    var oldLeader = await _context.Users.FirstOrDefaultAsync(u => u.UserId == oldLeaderId.Value, cancellationToken);
                    if (oldLeader != null && oldLeader.DepartmentId == request.DepartmentId)
                    {
                        oldLeader.SubRole = UserSubRoles.Staff;
                        oldLeader.UpdatedAt = VietnamTime.Now();
                        oldLeader.UpdatedBy = _currentUserService.UserId;
                    }
                }

                newLeader.SubRole = UserSubRoles.Leader;
                newLeader.UpdatedAt = VietnamTime.Now();
                newLeader.UpdatedBy = _currentUserService.UserId;

                // head_user_id moves in the SAME transaction as both sub-roles: the department is
                // never left headless, and never has two heads, even for an instant.
                dept.HeadUserId = newLeader.UserId;
                dept.UpdatedAt = VietnamTime.Now();
                dept.UpdatedBy = _currentUserService.UserId;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return new ReassignDepartmentLeadResponse { Success = true, Message = "Đã thay đổi trưởng phòng thành công." };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ReassignDepartmentLeadResponse { Success = false, Message = "Lỗi khi đổi trưởng phòng: " + ex.Message };
        }
    }
}
