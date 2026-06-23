using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Queries.ViewAccountStatistics;

/// <summary>
/// UC-95-SL handler. Applies the same role/campus scope as the account list so the
/// statistics cards always agree with the table totals.
/// </summary>
public sealed class ViewAccountStatisticsQueryHandler
    : IRequestHandler<ViewAccountStatisticsQuery, ViewAccountStatisticsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewAccountStatisticsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ViewAccountStatisticsDto> Handle(ViewAccountStatisticsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrEmpty(_currentUser.RoleCode))
            throw new AuthBusinessException(
                AccountErrorCodes.AccountListForbidden, "Bạn không có quyền xem thống kê tài khoản.", 403);

        var roleCode = _currentUser.RoleCode!;
        var campusId = _currentUser.PrimaryCampusId;

        IQueryable<User> query = _db.Users.AsNoTracking();

        if (roleCode == RoleCodes.Admin)
        {
            // System-wide.
        }
        else if (roleCode == RoleCodes.Ho)
        {
            query = query.Where(u =>
                u.Role.RoleCode == RoleCodes.Ho ||
                (u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader));
        }
        else if (roleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader)
        {
            query = campusId is null
                ? query.Where(_ => false)
                : query.Where(u => u.PrimaryCampusId == campusId &&
                    (u.Role.RoleCode == RoleCodes.Staff ||
                     (u.Role.RoleCode == RoleCodes.Department && u.SubRole == UserSubRoles.Leader) ||
                     u.Role.RoleCode == RoleCodes.Student));
        }
        else
        {
            query = campusId is null
                ? query.Where(_ => false)
                : query.Where(u => u.PrimaryCampusId == campusId);
        }

        var total = await query.CountAsync(cancellationToken);
        var active = await query.CountAsync(u => u.Status == UserStatuses.Active, cancellationToken);
        var locked = await query.CountAsync(u => u.Status == UserStatuses.Locked, cancellationToken);
        var inactive = await query.CountAsync(u => u.Status == UserStatuses.Inactive, cancellationToken);

        return new ViewAccountStatisticsDto
        {
            TotalAccounts = total,
            ActiveAccounts = active,
            LockedAccounts = locked,
            InactiveAccounts = inactive,
        };
    }
}
