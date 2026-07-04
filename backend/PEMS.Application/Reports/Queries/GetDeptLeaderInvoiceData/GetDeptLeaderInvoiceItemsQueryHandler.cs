using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Reports.Queries.GetDeptLeaderReportOverview;

namespace PEMS.Application.Reports.Queries.GetDeptLeaderInvoiceData;

public sealed class GetDeptLeaderInvoiceItemsQueryHandler
    : IRequestHandler<GetDeptLeaderInvoiceItemsQuery, List<DeptLeaderInvoiceItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDeptLeaderInvoiceItemsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<DeptLeaderInvoiceItemDto>> Handle(GetDeptLeaderInvoiceItemsQuery request, CancellationToken cancellationToken)
    {
        var deptId = DeptLeaderInvoiceGuard.RequireDepartmentLeader(_currentUser);

        var rows = await _db.VisitLogisticsItems.AsNoTracking()
            .Where(li => li.VisitInstanceId == request.VisitInstanceId
                         && li.RequestedToDepartmentId == deptId
                         && li.Status != "CANCELLED")
            .OrderBy(li => li.ItemType).ThenBy(li => li.LogisticsItemId)
            .Select(li => new { li.LogisticsItemId, li.Title, li.ItemType, li.Quantity })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new DeptLeaderInvoiceItemDto
        {
            LogisticsItemId = r.LogisticsItemId,
            ItemName = r.Title,
            ItemType = r.ItemType,
            ItemTypeLabelVi = DeptLeaderReportLabels.ItemTypeLabelVi(r.ItemType),
            Quantity = r.Quantity ?? 1,
            Unit = null,
        }).ToList();
    }
}
