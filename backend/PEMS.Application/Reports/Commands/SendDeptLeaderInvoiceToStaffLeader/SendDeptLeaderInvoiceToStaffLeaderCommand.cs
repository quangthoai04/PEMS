using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Idempotency;
using PEMS.Application.Reports.Common;
using PEMS.Application.Reports.Queries.GetDeptLeaderReportV2;

namespace PEMS.Application.Reports.Commands.SendDeptLeaderInvoiceToStaffLeader;

/// <summary>
/// Gửi hóa đơn tổng hợp các đơn yêu cầu hậu cần ĐÃ HOÀN THÀNH (kèm đơn giá Department
/// Leader nhập) qua email cho Staff Leader của campus phòng ban trực thuộc. Số lượng
/// luôn đọc lại từ DB; đơn giá do Department Leader nhập. Nội dung thư đến từ
/// <c>email_templates</c> (REPORT_DEPARTMENT_INVOICE — dùng chung với chiều ngược lại);
/// bảng kê đi kèm trong tệp PDF.
/// </summary>
public sealed class SendDeptLeaderInvoiceToStaffLeaderCommand : IRequest<SendDeptLeaderInvoiceToStaffLeaderResult>, IIdempotentEmailSend
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Note { get; set; }
    public List<SendDeptLeaderInvoiceLineItem> Items { get; set; } = new();

    /// <inheritdoc />
    public string OperationCode => EmailSendOperations.DeptLeaderInvoiceToStaffLeader;

    /// <inheritdoc />
    public void DescribeRequest(EmailSendFingerprintBuilder builder) =>
        builder.Date("from", FromDate)
               .Date("to", ToDate)
               .Text("note", Note)
               .Lines("items", Items.Select(i => (i.LogisticsItemId, i.UnitPrice)));
}

public sealed class SendDeptLeaderInvoiceLineItem
{
    public ulong LogisticsItemId { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class SendDeptLeaderInvoiceToStaffLeaderResult : IEmailSendResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SendDeptLeaderInvoiceToStaffLeaderCommandHandler
    : IRequestHandler<SendDeptLeaderInvoiceToStaffLeaderCommand, SendDeptLeaderInvoiceToStaffLeaderResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReportEmailSender _reportEmail;

    public SendDeptLeaderInvoiceToStaffLeaderCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IReportEmailSender reportEmail)
    {
        _db = db;
        _currentUser = currentUser;
        _reportEmail = reportEmail;
    }

    public async Task<SendDeptLeaderInvoiceToStaffLeaderResult> Handle(
        SendDeptLeaderInvoiceToStaffLeaderCommand request, CancellationToken cancellationToken)
    {
        var deptId = DeptLeaderReportV2Guard.RequireDepartmentLeader(_currentUser);

        if (request.Items.Count == 0)
            throw new ValidationException("Chọn ít nhất một đơn yêu cầu để gửi hóa đơn.");

        var dept = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == deptId)
            .Select(d => new { d.Name, d.CampusId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phòng ban.");

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == dept.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Campus #{dept.CampusId}";

        var staffLeader = await _db.Users.AsNoTracking()
            .Where(u => u.PrimaryCampusId == dept.CampusId
                        && u.Role.RoleCode == "STAFF" && u.SubRole == "LEADER" && u.Status == "ACTIVE")
            .Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (staffLeader == null || string.IsNullOrWhiteSpace(staffLeader.Email))
            throw new ValidationException("Campus của phòng ban chưa có Staff Leader đang hoạt động để nhận hóa đơn.");

        // Đọc lại từ DB — đảm bảo đúng phòng ban + đã hoàn thành; số lượng lấy từ DB.
        var requestedIds = request.Items.Select(i => i.LogisticsItemId).Distinct().ToList();
        var dbItems = await (
                from li in _db.VisitLogisticsItems.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on li.VisitInstanceId equals ci.VisitInstanceId
                where li.RequestedToDepartmentId == deptId && requestedIds.Contains(li.LogisticsItemId)
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
        var grandTotal = 0m;
        foreach (var input in request.Items)
        {
            // Scope first: the price is only ever applied to a line already proven to belong here.
            var db = dbItems.FirstOrDefault(x => x.LogisticsItemId == input.LogisticsItemId)
                ?? throw new ValidationException($"Đơn #{input.LogisticsItemId} không thuộc phòng ban của bạn.");
            InvoiceMoney.ValidateUnitPrice(input.UnitPrice, db.Title);
            var qty = db.Quantity ?? 1;
            var amount = InvoiceMoney.Multiply(qty, input.UnitPrice, $"Thành tiền của '{db.Title}'");
            lines.Add(new InvoiceLine(db.Title, db.DelegationName ?? "", db.StartAt, qty, input.UnitPrice, amount));
            grandTotal = InvoiceMoney.Add(grandTotal, amount, "Tổng tiền hóa đơn");
        }

        var leaderName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Department Leader";

        var nowVn = VietnamTime.Now();
        var (periodFrom, periodTo) = ReportPeriod.InvoiceLabels(request.FromDate, request.ToDate, nowVn);

        var pdf = ReportPdf.Render(InvoicePdf.Build(
            new InvoiceDocument(
                Title: "HÓA ĐƠN HẬU CẦN TIẾP KHÁCH (ĐÃ HOÀN THÀNH)",
                DepartmentName: dept.Name,
                CampusName: campusName,
                PeriodFrom: periodFrom,
                PeriodTo: periodTo,
                IssuedAt: nowVn,
                Lines: lines,
                GrandTotal: grandTotal,
                Note: request.Note,
                SenderLabel: $"{leaderName} · Department Leader · {dept.Name}")));

        await _reportEmail.SendAsync(
            new ReportEmailMessage(
                SystemEmailTemplates.ReportDepartmentInvoice,
                new EmailRecipient(staffLeader.Email!, staffLeader.FullName),
                new Dictionary<string, string>
                {
                    ["recipientName"] = staffLeader.FullName,
                    ["departmentName"] = dept.Name,
                    ["periodFrom"] = periodFrom,
                    ["periodTo"] = periodTo,
                },
                ReportAttachmentName.Build("Department_Invoice", nowVn),
                pdf,
                _currentUser.UserId,
                ReportEmailRelatedTypes.Department,
                deptId),
            cancellationToken);

        return new SendDeptLeaderInvoiceToStaffLeaderResult
        {
            Success = true,
            Message = $"Đã gửi hóa đơn tới Staff Leader ({staffLeader.Email}).",
        };
    }
}
