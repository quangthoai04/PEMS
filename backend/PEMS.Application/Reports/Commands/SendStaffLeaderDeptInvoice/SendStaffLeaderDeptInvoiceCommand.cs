using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Reports.Common;
using PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;

namespace PEMS.Application.Reports.Commands.SendStaffLeaderDeptInvoice;

/// <summary>
/// Gửi hóa đơn tổng hợp các đơn yêu cầu hậu cần (kèm đơn giá Staff Leader nhập)
/// qua email cho phòng ban. Số lượng luôn đọc lại từ DB; giá do Staff Leader nhập.
/// Người nhận: trưởng phòng (head user) của phòng ban. Nội dung thư đến từ
/// <c>email_templates</c> (REPORT_DEPARTMENT_INVOICE); bảng kê đi kèm trong tệp PDF.
/// </summary>
public sealed class SendStaffLeaderDeptInvoiceCommand : IRequest<SendStaffLeaderDeptInvoiceResult>
{
    public ulong DepartmentId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Note { get; set; }
    public List<SendStaffLeaderDeptInvoiceItem> Items { get; set; } = new();
}

public sealed class SendStaffLeaderDeptInvoiceItem
{
    public ulong LogisticsItemId { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class SendStaffLeaderDeptInvoiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SendStaffLeaderDeptInvoiceCommandHandler
    : IRequestHandler<SendStaffLeaderDeptInvoiceCommand, SendStaffLeaderDeptInvoiceResult>
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReportEmailSender _reportEmail;

    public SendStaffLeaderDeptInvoiceCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IReportEmailSender reportEmail)
    {
        _db = db;
        _currentUser = currentUser;
        _reportEmail = reportEmail;
    }

    public async Task<SendStaffLeaderDeptInvoiceResult> Handle(
        SendStaffLeaderDeptInvoiceCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderReportV2Guard.RequireStaffLeaderCampus(_currentUser);

        if (request.Items.Count == 0)
            throw new ValidationException("Chọn ít nhất một đơn yêu cầu để gửi hóa đơn.");
        if (request.Items.Any(i => i.UnitPrice < 0))
            throw new ValidationException("Đơn giá phải lớn hơn hoặc bằng 0.");

        var dept = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == request.DepartmentId && d.CampusId == campusId)
            .Select(d => new { d.DepartmentId, d.Name, d.HeadUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phòng ban trong campus của bạn.");

        var head = dept.HeadUserId != null
            ? await _db.Users.AsNoTracking()
                .Where(u => u.UserId == dept.HeadUserId)
                .Select(u => new { u.FullName, u.Email })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        if (head == null || string.IsNullOrWhiteSpace(head.Email))
            throw new ValidationException("Phòng ban chưa có trưởng phòng/email để nhận hóa đơn.");

        // Đọc lại các đơn từ DB — đảm bảo đúng phòng ban + campus; số lượng lấy từ DB.
        var requestedIds = request.Items.Select(i => i.LogisticsItemId).Distinct().ToList();
        var dbItems = await (
                from li in _db.VisitLogisticsItems.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on li.VisitInstanceId equals ci.VisitInstanceId
                where ci.CampusId == campusId
                      && li.RequestedToDepartmentId == request.DepartmentId
                      && requestedIds.Contains(li.LogisticsItemId)
                select new
                {
                    li.LogisticsItemId,
                    li.Title,
                    li.Quantity,
                    // Instance row: mixed v2 shows THIS instance's detail name.
                    DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
                    StartAt = li.UsageStartAt ?? ci.PlannedStartAt,
                })
            .ToListAsync(cancellationToken);

        var lines = new List<InvoiceLine>();
        foreach (var input in request.Items)
        {
            var db = dbItems.FirstOrDefault(x => x.LogisticsItemId == input.LogisticsItemId)
                ?? throw new ValidationException($"Đơn #{input.LogisticsItemId} không thuộc phòng ban/campus của bạn.");
            var qty = db.Quantity ?? 1;
            lines.Add(new InvoiceLine(db.Title, db.DelegationName ?? "", db.StartAt, qty, input.UnitPrice, qty * input.UnitPrice));
        }
        var grandTotal = lines.Sum(l => l.Amount);

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == campusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Campus #{campusId}";
        var leaderName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Staff Leader";

        var nowVn = VietnamTime.Now();
        var (periodFrom, periodTo) = ReportPeriod.InvoiceLabels(request.FromDate, request.ToDate, nowVn);

        var pdf = ReportPdf.Render(InvoicePdf.Build(
            new InvoiceDocument(
                Title: "HÓA ĐƠN HẬU CẦN TIẾP KHÁCH",
                DepartmentName: dept.Name,
                CampusName: campusName,
                PeriodFrom: periodFrom,
                PeriodTo: periodTo,
                IssuedAt: nowVn,
                Lines: lines,
                GrandTotal: grandTotal,
                Note: request.Note,
                SenderLabel: $"{leaderName} · Staff Leader · {campusName}")));

        await _reportEmail.SendAsync(
            new ReportEmailMessage(
                SystemEmailTemplates.ReportDepartmentInvoice,
                new EmailRecipient(head.Email!, head.FullName),
                new Dictionary<string, string>
                {
                    ["recipientName"] = head.FullName,
                    ["departmentName"] = dept.Name,
                    ["periodFrom"] = periodFrom,
                    ["periodTo"] = periodTo,
                },
                ReportAttachmentName.Build("Department_Invoice", nowVn),
                pdf,
                _currentUser.UserId,
                ReportEmailRelatedTypes.Department,
                dept.DepartmentId),
            cancellationToken);

        return new SendStaffLeaderDeptInvoiceResult
        {
            Success = true,
            Message = $"Đã gửi hóa đơn tới {head.Email}.",
        };
    }
}
