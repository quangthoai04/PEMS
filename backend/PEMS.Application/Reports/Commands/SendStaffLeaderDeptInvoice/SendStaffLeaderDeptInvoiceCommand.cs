using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;

namespace PEMS.Application.Reports.Commands.SendStaffLeaderDeptInvoice;

/// <summary>
/// Gửi hóa đơn tổng hợp các đơn yêu cầu hậu cần (kèm đơn giá Staff Leader nhập)
/// qua email cho phòng ban. Số lượng luôn đọc lại từ DB; giá do Staff Leader nhập.
/// Người nhận: trưởng phòng (head user) của phòng ban.
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
    private readonly IEmailService _email;

    public SendStaffLeaderDeptInvoiceCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IEmailService email)
    {
        _db = db;
        _currentUser = currentUser;
        _email = email;
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

        var recipientEmail = dept.HeadUserId != null
            ? await _db.Users.AsNoTracking()
                .Where(u => u.UserId == dept.HeadUserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        if (string.IsNullOrWhiteSpace(recipientEmail))
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

        var lines = new List<(string Title, string Delegation, DateTime StartAt, int Quantity, decimal UnitPrice, decimal Amount)>();
        foreach (var input in request.Items)
        {
            var db = dbItems.FirstOrDefault(x => x.LogisticsItemId == input.LogisticsItemId)
                ?? throw new ValidationException($"Đơn #{input.LogisticsItemId} không thuộc phòng ban/campus của bạn.");
            var qty = db.Quantity ?? 1;
            lines.Add((db.Title, db.DelegationName ?? "", db.StartAt, qty, input.UnitPrice, qty * input.UnitPrice));
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
        var fromLabel = request.FromDate?.ToString("dd/MM/yyyy") ?? "—";
        var toLabel = (request.ToDate ?? nowVn).ToString("dd/MM/yyyy");

        var html = BuildInvoiceHtml(dept.Name, campusName, leaderName, fromLabel, toLabel, lines, grandTotal, request.Note, nowVn);
        var subject = $"[PEMS] Hóa đơn hậu cần tiếp khách — {dept.Name} ({fromLabel} – {toLabel})";
        await _email.SendAsync(recipientEmail, subject, html, cancellationToken);

        return new SendStaffLeaderDeptInvoiceResult
        {
            Success = true,
            Message = $"Đã gửi hóa đơn tới {recipientEmail}.",
        };
    }

    private static string Money(decimal v) => v.ToString("#,##0", Vi) + " ₫";

    private static string BuildInvoiceHtml(
        string deptName, string campusName, string leaderName,
        string fromLabel, string toLabel,
        List<(string Title, string Delegation, DateTime StartAt, int Quantity, decimal UnitPrice, decimal Amount)> lines,
        decimal grandTotal, string? note, DateTime nowVn)
    {
        string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:760px;margin:0 auto;color:#1f2937\">");
        sb.Append("<div style=\"background:#f37021;color:#fff;padding:20px 24px;border-radius:12px 12px 0 0\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:2px;opacity:.85\">FPT UNIVERSITY • PEMS</div>");
        sb.Append("<div style=\"font-size:20px;font-weight:700;margin-top:4px\">Hóa đơn hậu cần tiếp khách</div>");
        sb.Append($"<div style=\"font-size:13px;opacity:.9;margin-top:2px\">Kỳ: {E(fromLabel)} – {E(toLabel)} · {E(campusName)}</div>");
        sb.Append("</div>");
        sb.Append("<div style=\"border:1px solid #e5e7eb;border-top:0;padding:24px;border-radius:0 0 12px 12px\">");
        sb.Append($"<p>Kính gửi <b>{E(deptName)}</b>,</p>");
        sb.Append("<p>Văn phòng IC gửi phòng ban bảng kê chi phí các hạng mục hậu cần phòng ban đã hỗ trợ chuẩn bị trong kỳ:</p>");

        sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px;margin:14px 0\">");
        sb.Append("<tr style=\"background:#004c91;color:#fff\">"
            + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">STT</th>"
            + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Hạng mục</th>"
            + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Đoàn khách</th>"
            + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Ngày</th>"
            + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:right\">SL</th>"
            + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:right\">Đơn giá</th>"
            + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:right\">Thành tiền</th></tr>");
        var i = 0;
        foreach (var l in lines.OrderBy(l => l.StartAt))
        {
            i++;
            sb.Append($"<tr{(i % 2 == 0 ? " style=\"background:#f8fafc\"" : "")}>"
                + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{i}</td>"
                + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(l.Title)}</td>"
                + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(l.Delegation)}</td>"
                + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{l.StartAt:dd/MM/yyyy}</td>"
                + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb;text-align:right\">{l.Quantity}</td>"
                + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb;text-align:right\">{Money(l.UnitPrice)}</td>"
                + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb;text-align:right\">{Money(l.Amount)}</td></tr>");
        }
        sb.Append("<tr style=\"background:#fff7ed\">"
            + "<td colspan=\"6\" style=\"padding:10px 8px;border:1px solid #e5e7eb;text-align:right;font-weight:700\">TỔNG CỘNG</td>"
            + $"<td style=\"padding:10px 8px;border:1px solid #e5e7eb;text-align:right;font-weight:700;color:#c2410c\">{Money(grandTotal)}</td></tr>");
        sb.Append("</table>");

        if (!string.IsNullOrWhiteSpace(note))
            sb.Append($"<p><b>Ghi chú:</b> {E(note.Trim())}</p>");

        sb.Append($"<p style=\"margin-top:20px\">Trân trọng,<br/><b>{E(leaderName)}</b><br/>Staff Leader · {E(campusName)}</p>");
        sb.Append($"<p style=\"font-size:11px;color:#9ca3af;border-top:1px solid #e5e7eb;padding-top:10px\">Hóa đơn lập lúc {nowVn:HH:mm dd/MM/yyyy} — tạo tự động từ hệ thống PEMS.</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }
}
