using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

using PEMS.Application.Common;
namespace PEMS.Application.Reports.Queries.GetDeptLeaderInvoiceData;

public sealed class GetDeptLeaderInvoiceVisitsQueryHandler
    : IRequestHandler<GetDeptLeaderInvoiceVisitsQuery, List<DeptLeaderInvoiceVisitDto>>
{

    private const int VisitLimit = 100;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDeptLeaderInvoiceVisitsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<DeptLeaderInvoiceVisitDto>> Handle(GetDeptLeaderInvoiceVisitsQuery request, CancellationToken cancellationToken)
    {
        var deptId = DeptLeaderInvoiceGuard.RequireDepartmentLeader(_currentUser);

        var nowVn = VietnamTime.Now();
        var (fromVn, toVnExclusive) = DeptLeaderInvoiceGuard.ResolvePeriodVn(request.Preset, request.FromDate, request.ToDate, nowVn);


        return await _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                         && ci.LogisticsItems.Any(li => li.RequestedToDepartmentId == deptId))
            .OrderByDescending(ci => ci.PlannedStartAt)
            .Take(VisitLimit)
            .Select(ci => new DeptLeaderInvoiceVisitDto
            {
                VisitInstanceId = ci.VisitInstanceId,
                RequestCode = ci.VisitRequest.RequestCode,
                // Instance row: mixed v2 shows THIS instance's detail name.
                DelegationName = ci.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                    ? (ci.FormDetail != null ? ci.FormDetail.DelegationName : null)
                    : ci.VisitRequest.DelegationName,
                PlannedStartAt = ci.PlannedStartAt,
                PlannedEndAt = ci.PlannedEndAt,
            })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Shared role/scope guard + period resolution for the Department Leader invoice endpoints.</summary>
public static class DeptLeaderInvoiceGuard
{
    public static ulong RequireDepartmentLeader(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated)
            throw new ForbiddenException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
        if (!string.Equals(currentUser.RoleCode, "DEPARTMENT", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(currentUser.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền sử dụng chức năng hóa đơn phòng ban.");
        return currentUser.DepartmentId
            ?? throw new ForbiddenException("Tài khoản chưa được gán phòng ban.");
    }

    /// <summary>Returns [from, toExclusive) in Vietnam local time.</summary>
    public static (DateTime FromVn, DateTime ToVnExclusive) ResolvePeriodVn(
        string? preset, DateTime? fromDate, DateTime? toDate, DateTime nowVn)
    {
        switch (preset?.Trim().ToUpperInvariant())
        {
            case "THIS_QUARTER":
                var quarterStartMonth = ((nowVn.Month - 1) / 3) * 3 + 1;
                var quarterStart = new DateTime(nowVn.Year, quarterStartMonth, 1);
                return (quarterStart, quarterStart.AddMonths(3));
            case "THIS_YEAR":
                return (new DateTime(nowVn.Year, 1, 1), new DateTime(nowVn.Year + 1, 1, 1));
            case "CUSTOM":
                var from = (fromDate ?? new DateTime(nowVn.Year, 1, 1)).Date;
                var to = (toDate ?? nowVn).Date.AddDays(1);
                if (to <= from) to = from.AddDays(1);
                return (from, to);
            default: // THIS_MONTH
                var monthStart = new DateTime(nowVn.Year, nowVn.Month, 1);
                return (monthStart, monthStart.AddMonths(1));
        }
    }
}
