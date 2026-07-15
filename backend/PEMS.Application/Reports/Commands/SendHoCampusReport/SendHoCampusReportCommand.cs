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
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Reports.Queries.GetHoReportV2;
using PEMS.Shared;

namespace PEMS.Application.Reports.Commands.SendHoCampusReport;

/// <summary>
/// HO gửi email báo cáo vận hành của MỘT campus (số liệu trong kỳ đang lọc)
/// cho Staff Leader của campus đó — dạng báo cáo chuyên nghiệp; feedback trung bình
/// dưới 2★ kèm khối cảnh báo.
/// </summary>
public sealed class SendHoCampusReportCommand : IRequest<SendHoCampusReportResult>
{
    public ulong CampusId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>Ghi chú HO nhập trên bảng campus (đưa vào email).</summary>
    public string? Note { get; set; }
}

public sealed class SendHoCampusReportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SendHoCampusReportCommandHandler
    : IRequestHandler<SendHoCampusReportCommand, SendHoCampusReportResult>
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;

    public SendHoCampusReportCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IEmailService email)
    {
        _db = db;
        _currentUser = currentUser;
        _email = email;
    }

    public async Task<SendHoCampusReportResult> Handle(SendHoCampusReportCommand request, CancellationToken cancellationToken)
    {
        HoReportV2Guard.RequireHo(_currentUser);
        var nowVn = VietnamTime.Now();
        var (fromVn, toVnExclusive) = HoReportV2Guard.ResolvePeriodVn(
            "CUSTOM", request.FromDate, request.ToDate ?? nowVn, nowVn);

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == request.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy campus.");

        // Người nhận: Staff Leader đang hoạt động của campus.
        var leader = await _db.Users.AsNoTracking()
            .Where(u => u.PrimaryCampusId == request.CampusId
                        && u.Role.RoleCode == "STAFF" && u.SubRole == "LEADER" && u.Status == "ACTIVE")
            .Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (leader == null || string.IsNullOrWhiteSpace(leader.Email))
            throw new ValidationException("Campus này chưa có Staff Leader đang hoạt động để nhận báo cáo.");

        // Số liệu campus trong kỳ.
        var instances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.CampusId == request.CampusId
                         && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive);
        var statusRows = await instances.Select(ci => ci.Status).ToListAsync(cancellationToken);
        var totalGuests = await instances.SelectMany(ci => ci.VisitRequest.GuestMembers).CountAsync(cancellationToken);
        var ratings = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "VISITOR_OVERALL" && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select (int)f.Rating)
            .ToListAsync(cancellationToken);
        var totalPartners = await _db.Partners.AsNoTracking()
            .CountAsync(p => p.OwnerCampusId == request.CampusId && p.ProfileStatus == "APPROVED", cancellationToken);

        var avg = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : (double?)null;
        var senderName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Head Office";

        var html = BuildEmailHtml(
            campusName, leader.FullName, senderName, fromVn, toVnExclusive.AddDays(-1),
            statusRows, totalGuests, totalPartners, avg, ratings.Count, request.Note);
        var subject = $"[PEMS] Báo cáo vận hành campus — {campusName} ({fromVn:dd/MM/yyyy} – {toVnExclusive.AddDays(-1):dd/MM/yyyy})";
        await _email.SendAsync(leader.Email, subject, html, cancellationToken);

        return new SendHoCampusReportResult { Success = true, Message = $"Đã gửi báo cáo tới {leader.Email}." };
    }

    private static string BuildEmailHtml(
        string campusName, string leaderName, string senderName,
        DateTime fromVn, DateTime toVn,
        List<string> statusRows, int totalGuests, int totalPartners,
        double? avg, int feedbackCount, string? note)
    {
        string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        int Count(string s) => statusRows.Count(x => x == s);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:720px;margin:0 auto;color:#1f2937\">");
        sb.Append("<div style=\"background:#004c91;color:#fff;padding:20px 24px;border-radius:12px 12px 0 0\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:2px;opacity:.85\">FPT UNIVERSITY • PEMS • HEAD OFFICE</div>");
        sb.Append("<div style=\"font-size:20px;font-weight:700;margin-top:4px\">Báo cáo vận hành campus</div>");
        sb.Append($"<div style=\"font-size:13px;opacity:.9;margin-top:2px\">{E(campusName)} · Kỳ {fromVn:dd/MM/yyyy} – {toVn:dd/MM/yyyy}</div>");
        sb.Append("</div>");
        sb.Append("<div style=\"border:1px solid #e5e7eb;border-top:0;padding:24px;border-radius:0 0 12px 12px\">");
        sb.Append($"<p>Kính gửi <b>{E(leaderName)}</b> (Staff Leader · {E(campusName)}),</p>");
        sb.Append("<p>Head Office gửi anh/chị số liệu vận hành tiếp khách của campus trong kỳ:</p>");

        if (avg != null && avg < 2)
        {
            sb.Append("<div style=\"background:#fef2f2;border:1px solid #fecaca;border-radius:10px;padding:14px 16px;margin:14px 0\">");
            sb.Append($"<b style=\"color:#b91c1c\">⚠ Cảnh báo chất lượng:</b> <span style=\"color:#7f1d1d\">Feedback trung bình của campus trong kỳ là <b>{avg.Value.ToString("0.0", Vi)}★</b> (dưới 2★). Đề nghị campus rà soát quy trình đón tiếp và phản hồi Head Office.</span>");
            sb.Append("</div>");
        }

        sb.Append("<table style=\"width:100%;border-collapse:collapse;margin:14px 0\">");
        void Metric(string label, string value)
            => sb.Append($"<tr><td style=\"padding:8px 12px;border:1px solid #e5e7eb;background:#f8fafc;font-weight:600;width:45%\">{label}</td><td style=\"padding:8px 12px;border:1px solid #e5e7eb\">{value}</td></tr>");
        Metric("Tổng đoàn khách", statusRows.Count.ToString(Vi));
        Metric("Tổng khách", totalGuests.ToString(Vi));
        Metric("Đã hoàn thành (đóng đoàn)", Count(VisitInstanceStatus.Closed).ToString(Vi));
        Metric("Bị hủy", Count(VisitInstanceStatus.Cancelled).ToString(Vi));
        Metric("Từ chối", Count(VisitInstanceStatus.Rejected).ToString(Vi));
        Metric("Tổng đối tác", totalPartners.ToString(Vi));
        Metric("Feedback trung bình", avg != null ? $"{avg.Value.ToString("0.0", Vi)}★ ({feedbackCount} lượt đánh giá)" : "Chưa có đánh giá");
        if (!string.IsNullOrWhiteSpace(note)) Metric("Ghi chú của Head Office", E(note.Trim()));
        sb.Append("</table>");

        sb.Append($"<p style=\"margin-top:20px\">Trân trọng,<br/><b>{E(senderName)}</b><br/>Head Office · FPT University</p>");
        sb.Append("<p style=\"font-size:11px;color:#9ca3af;border-top:1px solid #e5e7eb;padding-top:10px\">Email được tạo tự động từ hệ thống PEMS — Partnership Engagement Management System.</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }
}
