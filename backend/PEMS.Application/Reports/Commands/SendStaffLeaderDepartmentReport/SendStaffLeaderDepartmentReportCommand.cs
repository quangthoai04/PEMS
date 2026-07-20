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
using PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;
using PEMS.Shared;

namespace PEMS.Application.Reports.Commands.SendStaffLeaderDepartmentReport;

/// <summary>
/// Gửi email báo cáo hiệu suất phối hợp cho 1 PHÒNG BAN từ bảng "Báo cáo phòng ban khác"
/// trên trang báo cáo của Staff Leader. Người nhận: trưởng phòng (Department Leader) của
/// phòng ban đó. Nội dung: các thông số trong bảng (tổng đơn/thư, hoàn thành, từ chối,
/// feedback) + ghi chú của Staff Leader + danh sách đơn hậu cần và thư mời hỗ trợ mà
/// phòng ban đã nhận trong khoảng thời gian đang lọc; nếu feedback trung bình dưới 2★
/// thì kèm khối cảnh báo chất lượng.
/// </summary>
public sealed class SendStaffLeaderDepartmentReportCommand : IRequest<SendStaffLeaderDepartmentReportResult>
{
    public ulong DepartmentId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>Ghi chú của Staff Leader nhập trên bảng (đưa vào email).</summary>
    public string? Note { get; set; }
}

public sealed class SendStaffLeaderDepartmentReportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SendStaffLeaderDepartmentReportCommandHandler
    : IRequestHandler<SendStaffLeaderDepartmentReportCommand, SendStaffLeaderDepartmentReportResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;

    public SendStaffLeaderDepartmentReportCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IEmailService email)
    {
        _db = db;
        _currentUser = currentUser;
        _email = email;
    }

    public async Task<SendStaffLeaderDepartmentReportResult> Handle(
        SendStaffLeaderDepartmentReportCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderReportV2Guard.RequireStaffLeaderCampus(_currentUser);
        var nowVn = VietnamTime.Now();
        var (fromVn, toVnExclusive) = StaffLeaderReportV2Guard.ResolvePeriodVn(
            "CUSTOM", request.FromDate, request.ToDate ?? nowVn, nowVn);

        var dept = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == request.DepartmentId && d.CampusId == campusId)
            .Select(d => new { d.DepartmentId, d.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phòng ban trong campus của bạn.");

        // Người nhận: trưởng phòng ACTIVE có email của phòng ban.
        var leaders = await _db.Users.AsNoTracking()
            .Where(u => u.DepartmentId == dept.DepartmentId && u.Status == "ACTIVE"
                        && u.Role.RoleCode == "DEPARTMENT" && u.SubRole == "LEADER"
                        && u.Email != null && u.Email != "")
            .Select(u => new { u.FullName, u.Email })
            .ToListAsync(cancellationToken);
        if (leaders.Count == 0)
            throw new ValidationException("Phòng ban này chưa có trưởng phòng đang hoạt động có email để nhận báo cáo.");

        var instances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.CampusId == campusId && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive);

        // ── Đơn yêu cầu hậu cần gửi tới phòng ban trong kỳ ──
        var logisticsRows = await (
                from li in _db.VisitLogisticsItems.AsNoTracking()
                join ci in instances on li.VisitInstanceId equals ci.VisitInstanceId
                where li.RequestedToDepartmentId == dept.DepartmentId
                select new
                {
                    li.LogisticsItemId,
                    li.Title,
                    li.Status,
                    DelegationName = ci.VisitRequest.DelegationName,
                    StartAt = li.UsageStartAt ?? ci.PlannedStartAt,
                })
            .ToListAsync(cancellationToken);

        // ── Thư mời hỗ trợ gửi cho nhân sự phòng ban — gom theo đoàn ──
        var participantRows = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join ci in instances on p.VisitInstanceId equals ci.VisitInstanceId
                join u in _db.Users.AsNoTracking() on p.UserId equals u.UserId
                where u.DepartmentId == dept.DepartmentId && u.Role.RoleCode == "DEPARTMENT" && p.Status != "REMOVED"
                select new
                {
                    ci.VisitInstanceId,
                    p.Status,
                    DelegationName = ci.VisitRequest.DelegationName,
                    ci.PlannedStartAt,
                })
            .ToListAsync(cancellationToken);
        var invitationGroups = participantRows
            .GroupBy(p => p.VisitInstanceId)
            .Select(g => new
            {
                g.First().DelegationName,
                g.First().PlannedStartAt,
                Status = g.Any(p => p.Status == "ACCEPTED") ? "ACCEPTED"
                    : g.All(p => p.Status != "ACCEPTED") && g.Any(p => p.Status == "DECLINED") ? "DECLINED"
                    : "PENDING",
            })
            .ToList();

        // ── Thông số như bảng phần 3 ──
        var totalRequests = logisticsRows.Count + invitationGroups.Count;
        var completedCount = logisticsRows.Count(l => l.Status == LogisticsItemStatus.Done)
            + invitationGroups.Count(g => g.Status == "ACCEPTED");
        var rejectedCount = logisticsRows.Count(l => l.Status == LogisticsItemStatus.Rejected || l.Status == LogisticsItemStatus.Declined)
            + invitationGroups.Count(g => g.Status == "DECLINED");

        // Feedback host cho phòng ban: HOST_LOGISTICS (theo đơn/phòng ban) + HOST_PARTICIPANT tới người của phòng ban.
        var hostLogisticsFb = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_LOGISTICS" && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select new { f.TargetDepartmentId, f.TargetLogisticsItemId, Rating = (int)f.Rating })
            .ToListAsync(cancellationToken);
        var deptItemIds = logisticsRows.Select(l => l.LogisticsItemId).ToHashSet();
        var deptPartFb = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_PARTICIPANT" && f.TargetUserId != null && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                join u in _db.Users.AsNoTracking() on f.TargetUserId equals (ulong?)u.UserId
                where u.DepartmentId == dept.DepartmentId && u.Role.RoleCode == "DEPARTMENT"
                select (int)f.Rating)
            .ToListAsync(cancellationToken);
        var ratings = hostLogisticsFb
            .Where(x => x.TargetDepartmentId == dept.DepartmentId
                        || (x.TargetDepartmentId == null && x.TargetLogisticsItemId != null
                            && deptItemIds.Contains(x.TargetLogisticsItemId.Value)))
            .Select(x => x.Rating)
            .Concat(deptPartFb)
            .ToList();
        var avgFeedback = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : (double?)null;

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == campusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Campus #{campusId}";
        var leaderName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Staff Leader";

        // ── Danh sách nhiệm vụ (đơn + thư mời) đưa vào email ──
        var taskRows = logisticsRows
            .Select(l => (Kind: "Đơn hậu cần", Content: l.Title, l.DelegationName, Time: l.StartAt, Status: LogisticsStatusLabel(l.Status)))
            .Concat(invitationGroups.Select(g => (Kind: "Thư mời hỗ trợ", Content: "Tham gia hỗ trợ tiếp khách",
                g.DelegationName, Time: g.PlannedStartAt, Status: InvitationStatusLabel(g.Status))))
            .OrderBy(t => t.Time)
            .ToList();

        var html = BuildEmailHtml(
            dept.Name, campusName, leaderName,
            fromVn, toVnExclusive.AddDays(-1),
            totalRequests, completedCount, rejectedCount, avgFeedback, ratings.Count,
            taskRows, request.Note);

        var subject = $"[PEMS] Báo cáo phối hợp tiếp khách — {dept.Name} ({fromVn:dd/MM/yyyy} – {toVnExclusive.AddDays(-1):dd/MM/yyyy})";
        foreach (var leader in leaders)
            await _email.SendAsync(leader.Email!, subject, html, cancellationToken);

        return new SendStaffLeaderDepartmentReportResult
        {
            Success = true,
            Message = $"Đã gửi báo cáo tới {string.Join(", ", leaders.Select(l => l.Email))}.",
        };
    }

    private static string LogisticsStatusLabel(string status) => status switch
    {
        LogisticsItemStatus.Done => "Hoàn thành",
        LogisticsItemStatus.Rejected => "Từ chối",
        LogisticsItemStatus.Declined => "Nhân sự từ chối",
        LogisticsItemStatus.Cancelled => "Đã hủy",
        LogisticsItemStatus.InProgress => "Đang xử lý",
        LogisticsItemStatus.Accepted => "Đã nhận",
        LogisticsItemStatus.Assigned => "Đã phân công",
        LogisticsItemStatus.ChangeProposed => "Đề xuất thay đổi",
        LogisticsItemStatus.Requested => "Chờ tiếp nhận",
        _ => status,
    };

    private static string InvitationStatusLabel(string status) => status switch
    {
        "ACCEPTED" => "Đã nhận",
        "DECLINED" => "Từ chối",
        _ => "Chờ phản hồi",
    };

    private static string BuildEmailHtml(
        string deptName, string campusName, string leaderName,
        DateTime fromVn, DateTime toVn,
        int totalRequests, int completedCount, int rejectedCount, double? avgFeedback, int feedbackCount,
        List<(string Kind, string Content, string DelegationName, DateTime Time, string Status)> tasks,
        string? note)
    {
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:720px;margin:0 auto;color:#1f2937\">");
        sb.Append("<div style=\"background:#004c91;color:#fff;padding:20px 24px;border-radius:12px 12px 0 0\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:2px;opacity:.85\">FPT UNIVERSITY • PEMS</div>");
        sb.Append("<div style=\"font-size:20px;font-weight:700;margin-top:4px\">Báo cáo phối hợp tiếp khách của phòng ban</div>");
        sb.Append($"<div style=\"font-size:13px;opacity:.9;margin-top:2px\">Kỳ báo cáo: {fromVn:dd/MM/yyyy} – {toVn:dd/MM/yyyy} · {E(campusName)}</div>");
        sb.Append("</div>");
        sb.Append("<div style=\"border:1px solid #e5e7eb;border-top:0;padding:24px;border-radius:0 0 12px 12px\">");

        sb.Append($"<p>Kính gửi <b>{E(deptName)}</b>,</p>");
        sb.Append("<p>Dưới đây là báo cáo tổng hợp phối hợp tiếp khách của phòng ban trong kỳ, được gửi bởi Staff Leader phụ trách campus.</p>");

        // Khối cảnh báo khi feedback trung bình dưới 2★.
        if (avgFeedback != null && avgFeedback < 2)
        {
            sb.Append("<div style=\"background:#fef2f2;border:1px solid #fecaca;border-radius:10px;padding:14px 16px;margin:14px 0\">");
            sb.Append($"<b style=\"color:#b91c1c\">⚠ Cảnh báo chất lượng:</b> <span style=\"color:#7f1d1d\">Điểm feedback trung bình của phòng ban trong kỳ là <b>{avgFeedback.Value.ToString("0.0", vi)}★</b> (dưới 2★). Đề nghị phòng ban rà soát chất lượng hỗ trợ tiếp khách và trao đổi với Văn phòng IC để cải thiện.</span>");
            sb.Append("</div>");
        }

        // Bảng thông số (đúng các cột trong bảng phần 3).
        sb.Append("<table style=\"width:100%;border-collapse:collapse;margin:14px 0\">");
        void Metric(string label, string value)
            => sb.Append($"<tr><td style=\"padding:8px 12px;border:1px solid #e5e7eb;background:#f8fafc;font-weight:600;width:45%\">{label}</td><td style=\"padding:8px 12px;border:1px solid #e5e7eb\">{value}</td></tr>");
        Metric("Tổng đơn/thư yêu cầu", totalRequests.ToString(vi));
        Metric("Hoàn thành", completedCount.ToString(vi));
        Metric("Từ chối", rejectedCount.ToString(vi));
        Metric("Feedback trung bình", avgFeedback != null ? $"{avgFeedback.Value.ToString("0.0", vi)}★ ({feedbackCount} lượt đánh giá)" : "Chưa có đánh giá");
        if (!string.IsNullOrWhiteSpace(note)) Metric("Ghi chú của Staff Leader", E(note.Trim()));
        sb.Append("</table>");

        // Danh sách đơn/thư mời phòng ban đã nhận trong kỳ.
        sb.Append($"<div style=\"font-weight:700;color:#004c91;margin:18px 0 8px\">Danh sách nhiệm vụ phòng ban đã nhận trong kỳ ({tasks.Count})</div>");
        if (tasks.Count == 0)
        {
            sb.Append("<p style=\"color:#6b7280\">Phòng ban không nhận đơn yêu cầu/thư mời nào trong kỳ báo cáo.</p>");
        }
        else
        {
            sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\">");
            sb.Append("<tr style=\"background:#004c91;color:#fff\">"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">STT</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Loại</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Nội dung</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Đoàn khách</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Thời gian</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Trạng thái</th></tr>");
            var i = 0;
            foreach (var t in tasks)
            {
                i++;
                sb.Append($"<tr{(i % 2 == 0 ? " style=\"background:#f8fafc\"" : "")}>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{i}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(t.Kind)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(t.Content)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(t.DelegationName)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{t.Time:HH:mm dd/MM/yyyy}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(t.Status)}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append($"<p style=\"margin-top:20px\">Trân trọng,<br/><b>{E(leaderName)}</b><br/>Staff Leader · {E(campusName)}</p>");
        sb.Append("<p style=\"font-size:11px;color:#9ca3af;border-top:1px solid #e5e7eb;padding-top:10px\">Email được tạo tự động từ hệ thống PEMS — Partnership Engagement Management System.</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }
}
