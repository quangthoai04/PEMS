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
using PEMS.Application.Reports.Queries.GetDeptLeaderReportV2;
using PEMS.Shared;

namespace PEMS.Application.Reports.Commands.SendDeptLeaderPersonnelReport;

/// <summary>
/// Gửi email báo cáo hiệu suất cá nhân (dạng chuyên nghiệp) cho 1 nhân sự phòng ban
/// từ bảng nhân sự trên trang báo cáo của Department Leader. Nội dung: các thông số
/// của người đó trong kỳ + danh sách toàn bộ nhiệm vụ (thư mời/đơn) đã tham gia; nếu
/// feedback trung bình dưới 2★ thì kèm khối cảnh báo.
/// </summary>
public sealed class SendDeptLeaderPersonnelReportCommand : IRequest<SendDeptLeaderPersonnelReportResult>
{
    public ulong UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Note { get; set; }
}

public sealed class SendDeptLeaderPersonnelReportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SendDeptLeaderPersonnelReportCommandHandler
    : IRequestHandler<SendDeptLeaderPersonnelReportCommand, SendDeptLeaderPersonnelReportResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;

    public SendDeptLeaderPersonnelReportCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IEmailService email)
    {
        _db = db;
        _currentUser = currentUser;
        _email = email;
    }

    public async Task<SendDeptLeaderPersonnelReportResult> Handle(
        SendDeptLeaderPersonnelReportCommand request, CancellationToken cancellationToken)
    {
        var deptId = DeptLeaderReportV2Guard.RequireDepartmentLeader(_currentUser);
        var nowVn = VietnamTime.Now();
        var (fromVn, toVnExclusive) = DeptLeaderReportV2Guard.ResolvePeriodVn(
            "CUSTOM", request.FromDate, request.ToDate ?? nowVn, nowVn);

        var person = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == request.UserId && u.DepartmentId == deptId && u.Role.RoleCode == "DEPARTMENT")
            .Select(u => new { u.UserId, u.FullName, u.Email, u.SubRole })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhân sự trong phòng ban của bạn.");
        if (string.IsNullOrWhiteSpace(person.Email))
            throw new ValidationException("Nhân sự này chưa có địa chỉ email.");

        var roleLabel = string.Equals(person.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase) ? "Department Leader" : "Dept Staff";

        // Thư mời (đã ủy quyền) người này tham gia.
        var invitationRows = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on p.VisitInstanceId equals ci.VisitInstanceId
                where p.UserId == person.UserId && p.AssignedBy != null && p.Status != "REMOVED"
                      && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                select new
                {
                    ci.VisitRequest.RequestCode,
                    DelegationName = ci.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                                     && ci.VisitRequest.HasMixedCampusDetails
                        ? (ci.FormDetail != null ? ci.FormDetail.DelegationName : null)
                        : ci.VisitRequest.DelegationName,
                    ci.PlannedStartAt,
                    ci.PlannedEndAt,
                    p.Status,
                })
            .ToListAsync(cancellationToken);

        // Đơn hậu cần người này được gán.
        var logisticsRows = await (
                from li in _db.VisitLogisticsItems.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on li.VisitInstanceId equals ci.VisitInstanceId
                let startAt = li.UsageStartAt ?? ci.PlannedStartAt
                where li.AssignedToUserId == person.UserId
                      && startAt >= fromVn && startAt < toVnExclusive
                select new
                {
                    ci.VisitRequest.RequestCode,
                    DelegationName = ci.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                                     && ci.VisitRequest.HasMixedCampusDetails
                        ? (ci.FormDetail != null ? ci.FormDetail.DelegationName : null)
                        : ci.VisitRequest.DelegationName,
                    PlannedStartAt = startAt,
                    PlannedEndAt = li.UsageEndAt ?? ci.PlannedEndAt,
                    li.Status,
                })
            .ToListAsync(cancellationToken);

        var taskRows = invitationRows
            .Select(r => (r.RequestCode ?? "", r.DelegationName ?? "", r.PlannedStartAt, r.PlannedEndAt, r.Status, Kind: "Thư mời"))
            .Concat(logisticsRows.Select(r => (r.RequestCode ?? "", r.DelegationName ?? "", r.PlannedStartAt, r.PlannedEndAt, r.Status, Kind: "Đơn yêu cầu")))
            .OrderBy(r => r.PlannedStartAt)
            .ToList();

        var participantFbRatings = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_PARTICIPANT" && f.TargetUserId == person.UserId && f.VisitInstanceId != null
                join ci in _db.VisitRequestCampuses.AsNoTracking() on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                where ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                select (int)f.Rating)
            .ToListAsync(cancellationToken);
        var logisticsFbRatings = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_LOGISTICS" && f.TargetLogisticsItemId != null && f.VisitInstanceId != null
                join li in _db.VisitLogisticsItems.AsNoTracking() on f.TargetLogisticsItemId equals (ulong?)li.LogisticsItemId
                join ci in _db.VisitRequestCampuses.AsNoTracking() on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                where li.AssignedToUserId == person.UserId
                      && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                select (int)f.Rating)
            .ToListAsync(cancellationToken);
        var ratings = participantFbRatings.Concat(logisticsFbRatings).ToList();

        var declinedCount = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on p.VisitInstanceId equals ci.VisitInstanceId
                where p.UserId == person.UserId && p.Status == "DECLINED" && p.AssignedBy != null
                      && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                select p.ParticipantId)
            .CountAsync(cancellationToken)
            + logisticsRows.Count(r => r.Status == "REJECTED" || r.Status == "DECLINED");

        var totalHours = taskRows
            .Where(r => r.Status != "DECLINED" && r.Status != "REMOVED" && r.Status != "CANCELLED" && r.Status != "REJECTED")
            .Sum(r => Math.Max(0, (r.PlannedEndAt - r.PlannedStartAt).TotalHours));
        var avg = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : (double?)null;

        var deptName = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == deptId).Select(d => d.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Phòng ban #{deptId}";
        var leaderName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Department Leader";

        var html = BuildEmailHtml(
            person.FullName, roleLabel, deptName, leaderName,
            fromVn, toVnExclusive.AddDays(-1),
            taskRows, totalHours, avg, ratings.Count, declinedCount, request.Note);

        var subject = $"[PEMS] Báo cáo hiệu suất nhiệm vụ tiếp khách — {person.FullName} ({fromVn:dd/MM/yyyy} – {toVnExclusive.AddDays(-1):dd/MM/yyyy})";
        await _email.SendAsync(person.Email, subject, html, cancellationToken);

        return new SendDeptLeaderPersonnelReportResult { Success = true, Message = $"Đã gửi báo cáo tới {person.Email}." };
    }

    private static string BuildEmailHtml(
        string fullName, string roleLabel, string deptName, string leaderName,
        DateTime fromVn, DateTime toVn,
        List<(string Code, string Delegation, DateTime Start, DateTime End, string Status, string Kind)> tasks,
        double totalHours, double? avgFeedback, int feedbackCount, int declinedCount, string? note)
    {
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:720px;margin:0 auto;color:#1f2937\">");
        sb.Append("<div style=\"background:#004c91;color:#fff;padding:20px 24px;border-radius:12px 12px 0 0\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:2px;opacity:.85\">FPT UNIVERSITY • PEMS</div>");
        sb.Append("<div style=\"font-size:20px;font-weight:700;margin-top:4px\">Báo cáo hiệu suất nhiệm vụ tiếp khách</div>");
        sb.Append($"<div style=\"font-size:13px;opacity:.9;margin-top:2px\">Kỳ báo cáo: {fromVn:dd/MM/yyyy} – {toVn:dd/MM/yyyy} · {E(deptName)}</div>");
        sb.Append("</div>");
        sb.Append("<div style=\"border:1px solid #e5e7eb;border-top:0;padding:24px;border-radius:0 0 12px 12px\">");

        sb.Append($"<p>Xin chào <b>{E(fullName)}</b> ({E(roleLabel)}),</p>");
        sb.Append("<p>Dưới đây là báo cáo tổng hợp hiệu suất của bạn trong kỳ, được gửi bởi Trưởng phòng ban.</p>");

        if (avgFeedback != null && avgFeedback < 2)
        {
            sb.Append("<div style=\"background:#fef2f2;border:1px solid #fecaca;border-radius:10px;padding:14px 16px;margin:14px 0\">");
            sb.Append($"<b style=\"color:#b91c1c\">⚠ Cảnh báo chất lượng:</b> <span style=\"color:#7f1d1d\">Điểm feedback trung bình của bạn trong kỳ là <b>{avgFeedback.Value.ToString("0.0", vi)}★</b> (dưới 2★). Vui lòng chủ động trao đổi với Trưởng phòng ban để cải thiện chất lượng phối hợp.</span>");
            sb.Append("</div>");
        }

        sb.Append("<table style=\"width:100%;border-collapse:collapse;margin:14px 0\">");
        void Metric(string label, string value)
            => sb.Append($"<tr><td style=\"padding:8px 12px;border:1px solid #e5e7eb;background:#f8fafc;font-weight:600;width:45%\">{label}</td><td style=\"padding:8px 12px;border:1px solid #e5e7eb\">{value}</td></tr>");
        Metric("Số nhiệm vụ phụ trách", tasks.Count.ToString(vi));
        Metric("Tổng giờ làm việc", $"{totalHours.ToString("0.#", vi)} giờ");
        Metric("Feedback trung bình", avgFeedback != null ? $"{avgFeedback.Value.ToString("0.0", vi)}★ ({feedbackCount} lượt đánh giá)" : "Chưa có đánh giá");
        Metric("Số lần từ chối", declinedCount.ToString(vi));
        if (!string.IsNullOrWhiteSpace(note)) Metric("Ghi chú của Trưởng phòng ban", E(note.Trim()));
        sb.Append("</table>");

        sb.Append($"<div style=\"font-weight:700;color:#004c91;margin:18px 0 8px\">Danh sách nhiệm vụ đã tham gia ({tasks.Count})</div>");
        if (tasks.Count == 0)
        {
            sb.Append("<p style=\"color:#6b7280\">Không có nhiệm vụ nào trong kỳ báo cáo.</p>");
        }
        else
        {
            sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\">");
            sb.Append("<tr style=\"background:#004c91;color:#fff\">"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">STT</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Loại</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Mã đơn</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Đoàn khách</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Thời gian</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Số giờ</th></tr>");
            var i = 0;
            foreach (var t in tasks)
            {
                i++;
                var hours = Math.Max(0, (t.End - t.Start).TotalHours);
                sb.Append($"<tr{(i % 2 == 0 ? " style=\"background:#f8fafc\"" : "")}>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{i}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(t.Kind)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(t.Code)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(t.Delegation)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{t.Start:HH:mm dd/MM/yyyy} – {t.End:HH:mm dd/MM/yyyy}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{hours.ToString("0.#", vi)}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append($"<p style=\"margin-top:20px\">Trân trọng,<br/><b>{E(leaderName)}</b><br/>Department Leader · {E(deptName)}</p>");
        sb.Append("<p style=\"font-size:11px;color:#9ca3af;border-top:1px solid #e5e7eb;padding-top:10px\">Email được tạo tự động từ hệ thống PEMS — Partnership Engagement Management System.</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }
}
