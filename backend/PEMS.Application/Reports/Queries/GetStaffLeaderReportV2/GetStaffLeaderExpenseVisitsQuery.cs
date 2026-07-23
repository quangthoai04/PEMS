using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Shared;

namespace PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;

/// <summary>
/// Panel "Thống kê chi phí" trên trang báo cáo của Staff Leader: danh sách các đoàn trong
/// khoảng ngày có dữ liệu chi phí (bảng kê GENERAL của Host + bảng kê LOGISTICS của các
/// phòng ban), kèm chi tiết từng bảng kê/hạng mục để mở modal "Xem chi tiết" và dựng
/// bản in "Xuất thống kê PDF" (gộp theo đoàn, theo loại và theo phòng ban) ở frontend.
/// </summary>
public sealed class GetStaffLeaderExpenseVisitsQuery : IRequest<StaffLeaderExpenseVisitsDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class StaffLeaderExpenseVisitsDto
{
    public decimal TotalAmount { get; set; }
    public List<StaffLeaderExpenseVisitDto> Rows { get; set; } = new();
}

public sealed class StaffLeaderExpenseVisitDto
{
    public ulong VisitInstanceId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public decimal TotalExpense { get; set; }
    /// <summary>ĐÃ CHỐT / ĐÃ LƯU / BẢN NHÁP — trạng thái chốt cao nhất trong các bảng kê.</summary>
    public string Status { get; set; } = string.Empty;
    public List<StaffLeaderExpenseVisitReportDto> Reports { get; set; } = new();
}

/// <summary>1 bảng kê chi phí (GENERAL của Host hoặc LOGISTICS của 1 đơn phòng ban).</summary>
public sealed class StaffLeaderExpenseVisitReportDto
{
    public string ReportScope { get; set; } = string.Empty; // GENERAL | LOGISTICS
    public string? DepartmentName { get; set; }
    public string? LogisticsItemTitle { get; set; }
    public bool NoExpense { get; set; }
    public string? ReportNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<StaffLeaderExpenseVisitItemDto> Items { get; set; } = new();
}

public sealed class StaffLeaderExpenseVisitItemDto
{
    public string ItemOrigin { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class GetStaffLeaderExpenseVisitsQueryHandler
    : IRequestHandler<GetStaffLeaderExpenseVisitsQuery, StaffLeaderExpenseVisitsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStaffLeaderExpenseVisitsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StaffLeaderExpenseVisitsDto> Handle(
        GetStaffLeaderExpenseVisitsQuery request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderReportV2Guard.RequireStaffLeaderCampus(_currentUser);
        var nowVn = VietnamTime.Now();
        var (fromVn, toVnExclusive) = StaffLeaderReportV2Guard.ResolvePeriodVn(
            "CUSTOM", request.FromDate, request.ToDate ?? nowVn, nowVn);

        var instances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.CampusId == campusId && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive);

        // Mọi bảng kê chưa hủy của các đoàn trong khoảng ngày (kể cả bảng "Không có chi phí" — 0 ₫).
        var reports = await (
                from r in _db.VisitExpenseReports.AsNoTracking()
                join ci in instances on r.VisitInstanceId equals ci.VisitInstanceId
                where r.Status != "CANCELLED"
                select new
                {
                    r.ExpenseReportId, r.VisitInstanceId, r.ReportScope, r.Status,
                    r.NoExpense, r.ReportNote, r.DepartmentId, r.LogisticsItemId,
                })
            .ToListAsync(cancellationToken);

        if (reports.Count == 0) return new StaffLeaderExpenseVisitsDto();

        var reportIds = reports.Select(r => r.ExpenseReportId).ToList();
        var items = await _db.VisitExpenseItems.AsNoTracking()
            .Where(i => reportIds.Contains(i.ExpenseReportId))
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.ExpenseItemId)
            .Select(i => new { i.ExpenseReportId, i.ItemOrigin, i.ItemName, i.Quantity, i.UnitName, i.UnitPrice })
            .ToListAsync(cancellationToken);

        var deptIds = reports.Where(r => r.DepartmentId != null).Select(r => r.DepartmentId!.Value).Distinct().ToList();
        var deptNames = deptIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Departments.AsNoTracking()
                .Where(d => deptIds.Contains(d.DepartmentId))
                .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, cancellationToken);

        var liIds = reports.Where(r => r.LogisticsItemId != null).Select(r => r.LogisticsItemId!.Value).Distinct().ToList();
        var liTitles = liIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.VisitLogisticsItems.AsNoTracking()
                .Where(li => liIds.Contains(li.LogisticsItemId))
                .ToDictionaryAsync(li => li.LogisticsItemId, li => li.Title, cancellationToken);

        var instIds = reports.Select(r => r.VisitInstanceId).Distinct().ToList();
        var heads = await instances
            .Where(ci => instIds.Contains(ci.VisitInstanceId))
            .Select(ci => new
            {
                ci.VisitInstanceId,
                ci.VisitRequest.RequestCode,
                // Mixed per-campus v2: tên đoàn theo detail của CHÍNH instance này.
                DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
                ci.PlannedStartAt,
            })
            .ToListAsync(cancellationToken);

        var rows = new List<StaffLeaderExpenseVisitDto>();
        foreach (var head in heads.OrderByDescending(h => h.PlannedStartAt))
        {
            var instReports = reports.Where(r => r.VisitInstanceId == head.VisitInstanceId).ToList();

            var reportDtos = instReports
                // GENERAL (Host) hiện trước, sau đó các bảng kê phòng ban theo tên đơn.
                .OrderBy(r => r.ReportScope == "GENERAL" ? 0 : 1)
                .ThenBy(r => r.LogisticsItemId)
                .Select(r =>
                {
                    var reportItems = items.Where(i => i.ExpenseReportId == r.ExpenseReportId)
                        .Select(i => new StaffLeaderExpenseVisitItemDto
                        {
                            ItemOrigin = i.ItemOrigin,
                            ItemName = i.ItemName,
                            Quantity = i.Quantity,
                            UnitName = i.UnitName,
                            UnitPrice = i.UnitPrice,
                            TotalAmount = Math.Round(i.Quantity * i.UnitPrice, 2),
                        })
                        .ToList();
                    return new StaffLeaderExpenseVisitReportDto
                    {
                        ReportScope = r.ReportScope,
                        DepartmentName = r.DepartmentId != null ? deptNames.GetValueOrDefault(r.DepartmentId.Value) : null,
                        LogisticsItemTitle = r.LogisticsItemId != null ? liTitles.GetValueOrDefault(r.LogisticsItemId.Value) : null,
                        NoExpense = r.NoExpense,
                        ReportNote = r.ReportNote,
                        Status = r.Status,
                        TotalAmount = r.NoExpense ? 0 : reportItems.Sum(i => i.TotalAmount),
                        Items = r.NoExpense ? new List<StaffLeaderExpenseVisitItemDto>() : reportItems,
                    };
                })
                .ToList();

            var statusStr = "BẢN NHÁP";
            if (instReports.Any(r => r.Status == "FINALIZED")) statusStr = "ĐÃ CHỐT";
            else if (instReports.Any(r => r.Status == "SAVED")) statusStr = "ĐÃ LƯU";

            rows.Add(new StaffLeaderExpenseVisitDto
            {
                VisitInstanceId = head.VisitInstanceId,
                RequestCode = head.RequestCode ?? string.Empty,
                DelegationName = head.DelegationName ?? string.Empty,
                VisitDate = head.PlannedStartAt,
                TotalExpense = reportDtos.Sum(r => r.TotalAmount),
                Status = statusStr,
                Reports = reportDtos,
            });
        }

        return new StaffLeaderExpenseVisitsDto
        {
            TotalAmount = rows.Sum(r => r.TotalExpense),
            Rows = rows,
        };
    }
}
