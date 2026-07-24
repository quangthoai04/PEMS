using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitExpenses;

/// <summary>
/// Read authorization for a visit instance's expense reports. The expense read endpoints are
/// <c>[Authorize]</c>-only, so without this any authenticated user could pull the itemized costs of any
/// campus by instance id. Reading is scoped to people with a relation to THIS instance's campus:
///   • HO / Admin                                   — cross-campus oversight;
///   • the instance's current Host                  — owns the general report;
///   • a Staff Leader of the instance's own campus  — "Staff Leader chỉ xem campus của mình";
///   • a Department member whose department holds a LOGISTICS report on this instance.
/// A Staff Leader of a DIFFERENT campus is refused — "Staff Leader A không xem expense B".
/// The instance's campus is resolved server-side; nothing the caller sends is trusted.
/// </summary>
public static class VisitExpenseAccessScope
{
    public static async Task EnsureCanViewAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            throw new ForbiddenException("Bạn chưa đăng nhập.");

        var instance = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == visitInstanceId)
            .Select(c => new { c.CampusId, c.CurrentHostUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(VisitRequestCampus), visitInstanceId);

        var roleCode = currentUser.RoleCode ?? string.Empty;

        // HO / Admin oversee every campus.
        if (roleCode == RoleCodes.Ho || roleCode == RoleCodes.Admin)
            return;

        // The instance's own Host.
        if (instance.CurrentHostUserId == userId)
            return;

        // A Staff Leader of the instance's OWN campus (a different campus's Leader is refused).
        if (roleCode == RoleCodes.Staff
            && currentUser.SubRole == UserSubRoles.Leader
            && currentUser.PrimaryCampusId == instance.CampusId)
            return;

        // A Department member whose department holds a logistics expense report on this instance.
        if (currentUser.DepartmentId is { } departmentId)
        {
            var hasDeptReport = await db.VisitExpenseReports.AsNoTracking()
                .AnyAsync(r => r.VisitInstanceId == visitInstanceId
                               && r.ReportScope == "LOGISTICS"
                               && r.DepartmentId == departmentId,
                    cancellationToken);
            if (hasDeptReport)
                return;
        }

        throw new ForbiddenException("Bạn không có quyền xem chi phí của chuyến tiếp khách này.");
    }
}
