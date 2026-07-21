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
using PEMS.Shared;

namespace PEMS.Application.Reports.Commands.SendStaffLeaderPersonnelReport;

/// <summary>
/// Gửi email báo cáo hiệu suất cá nhân (dạng chuyên nghiệp) cho 1 nhân sự/student
/// từ bảng nhân sự trên trang báo cáo của Staff Leader. Nội dung: các thông số của
/// người đó trong kỳ + danh sách toàn bộ đoàn đã phụ trách/tham gia; nếu feedback
/// trung bình dưới 2★ thì kèm khối cảnh báo.
/// </summary>
public sealed class SendStaffLeaderPersonnelReportCommand : IRequest<SendStaffLeaderPersonnelReportResult>
{
    public ulong UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>Ghi chú của Staff Leader nhập trên bảng (đưa vào email).</summary>
    public string? Note { get; set; }
}

public sealed class SendStaffLeaderPersonnelReportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SendStaffLeaderPersonnelReportCommandHandler
    : IRequestHandler<SendStaffLeaderPersonnelReportCommand, SendStaffLeaderPersonnelReportResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;

    public SendStaffLeaderPersonnelReportCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IEmailService email)
    {
        _db = db;
        _currentUser = currentUser;
        _email = email;
    }

    public async Task<SendStaffLeaderPersonnelReportResult> Handle(
        SendStaffLeaderPersonnelReportCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderReportV2Guard.RequireStaffLeaderCampus(_currentUser);
        var nowVn = VietnamTime.Now();
        var (fromVn, toVnExclusive) = StaffLeaderReportV2Guard.ResolvePeriodVn(
            "CUSTOM", request.FromDate, request.ToDate ?? nowVn, nowVn);

        var person = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == request.UserId && u.PrimaryCampusId == campusId
                        && (u.Role.RoleCode == "STAFF" || u.Role.RoleCode == "STUDENT"))
            .Select(u => new { u.UserId, u.FullName, u.Email, RoleCode = u.Role.RoleCode, u.SubRole })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhân sự trong campus của bạn.");
        if (string.IsNullOrWhiteSpace(person.Email))
            throw new ValidationException("Nhân sự này chưa có địa chỉ email.");

        var isStudent = string.Equals(person.RoleCode, "STUDENT", StringComparison.OrdinalIgnoreCase);
        var roleLabel = isStudent ? "Sinh viên hỗ trợ"
            : string.Equals(person.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase) ? "Staff Leader" : "IC Staff";

        var instances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.CampusId == campusId && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive);

        // Danh sách đoàn + thông số của người này trong kỳ.
        List<(string Code, string Delegation, DateTime Start, DateTime End, string Status)> visitRows;
        List<int> ratings;
        if (isStudent)
        {
            visitRows = (await (
                    from p in _db.VisitParticipants.AsNoTracking()
                    join ci in instances on p.VisitInstanceId equals ci.VisitInstanceId
                    where p.UserId == person.UserId && p.Status == "ACCEPTED"
                    select new
                    {
                        ci.VisitInstanceId,
                        ci.VisitRequest.RequestCode,
                        DelegationName = ci.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                            ? (ci.FormDetail != null ? ci.FormDetail.DelegationName : null)
                            : ci.VisitRequest.DelegationName,
                        ci.PlannedStartAt,
                        ci.PlannedEndAt,
                        ci.Status,
                    })
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.VisitInstanceId).Select(g => g.First())
                .Select(x => (x.RequestCode ?? "", x.DelegationName ?? "", x.PlannedStartAt, x.PlannedEndAt, x.Status))
                .ToList();
            ratings = await (
                    from f in _db.Feedbacks.AsNoTracking()
                    where f.FeedbackType == "HOST_PARTICIPANT" && f.TargetUserId == person.UserId && f.VisitInstanceId != null
                    join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                    select (int)f.Rating)
                .ToListAsync(cancellationToken);
        }
        else
        {
            visitRows = (await instances
                    .Where(ci => ci.CurrentHostUserId == person.UserId)
                    .Select(ci => new
                    {
                        ci.VisitRequest.RequestCode,
                        DelegationName = ci.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                            ? (ci.FormDetail != null ? ci.FormDetail.DelegationName : null)
                            : ci.VisitRequest.DelegationName,
                        ci.PlannedStartAt,
                        ci.PlannedEndAt,
                        ci.Status,
                    })
                    .ToListAsync(cancellationToken))
                .Select(x => (x.RequestCode ?? "", x.DelegationName ?? "", x.PlannedStartAt, x.PlannedEndAt, x.Status))
                .ToList();
            ratings = await (
                    from f in _db.Feedbacks.AsNoTracking()
                    where f.FeedbackType == "VISITOR_OVERALL" && f.VisitInstanceId != null
                    join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                    where ci.CurrentHostUserId == person.UserId
                    select (int)f.Rating)
                .ToListAsync(cancellationToken);
        }

        var declinedCount = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join ci in instances on p.VisitInstanceId equals ci.VisitInstanceId
                where p.UserId == person.UserId && p.Status == "DECLINED"
                select p.ParticipantId)
            .CountAsync(cancellationToken);

        var totalHours = visitRows
            .Where(v => v.Status != VisitInstanceStatus.Cancelled && v.Status != VisitInstanceStatus.Rejected)
            .Sum(v => Math.Max(0, (v.End - v.Start).TotalHours));
        var avg = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : (double?)null;

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == campusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Campus #{campusId}";
        var leaderName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Staff Leader";

        var html = BuildEmailHtml(
            person.FullName, roleLabel, campusName, leaderName,
            fromVn, toVnExclusive.AddDays(-1),
            visitRows, isStudent, totalHours, avg, ratings.Count, declinedCount,
            request.Note);

        var subject = $"[PEMS] Báo cáo hiệu suất {(isStudent ? "tham gia tiếp khách" : "phụ trách đoàn khách")} — {person.FullName} ({fromVn:dd/MM/yyyy} – {toVnExclusive.AddDays(-1):dd/MM/yyyy})";
        await _email.SendAsync(person.Email, subject, html, cancellationToken);

        return new SendStaffLeaderPersonnelReportResult
        {
            Success = true,
            Message = $"Đã gửi báo cáo tới {person.Email}.",
        };
    }

    private static string StatusLabel(string status) => status switch
    {
        VisitInstanceStatus.Closed => "Hoàn thành",
        VisitInstanceStatus.Cancelled => "Đã hủy",
        VisitInstanceStatus.Rejected => "Từ chối",
        VisitInstanceStatus.DuringVisit => "Đang diễn ra",
        VisitInstanceStatus.AfterVisit => "Sau chuyến",
        VisitInstanceStatus.BeforeVisit => "Trước chuyến",
        VisitInstanceStatus.Assigned => "Đã gán host",
        VisitInstanceStatus.WaitingRequestApproval => "Chờ duyệt",
        _ => status,
    };

    private static string BuildEmailHtml(
        string fullName, string roleLabel, string campusName, string leaderName,
        DateTime fromVn, DateTime toVn,
        List<(string Code, string Delegation, DateTime Start, DateTime End, string Status)> visits,
        bool isStudent, double totalHours, double? avgFeedback, int feedbackCount, int declinedCount,
        string? note)
    {
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:720px;margin:0 auto;color:#1f2937\">");
        sb.Append("<div style=\"background:#004c91;color:#fff;padding:20px 24px;border-radius:12px 12px 0 0\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:2px;opacity:.85\">FPT UNIVERSITY • PEMS</div>");
        sb.Append($"<div style=\"font-size:20px;font-weight:700;margin-top:4px\">Báo cáo hiệu suất {(isStudent ? "tham gia tiếp khách" : "phụ trách đoàn khách")}</div>");
        sb.Append($"<div style=\"font-size:13px;opacity:.9;margin-top:2px\">Kỳ báo cáo: {fromVn:dd/MM/yyyy} – {toVn:dd/MM/yyyy} · {E(campusName)}</div>");
        sb.Append("</div>");
        sb.Append("<div style=\"border:1px solid #e5e7eb;border-top:0;padding:24px;border-radius:0 0 12px 12px\">");

        sb.Append($"<p>Xin chào <b>{E(fullName)}</b> ({E(roleLabel)}),</p>");
        sb.Append("<p>Dưới đây là báo cáo tổng hợp hiệu suất của bạn trong kỳ, được gửi bởi Staff Leader phụ trách campus.</p>");

        // Khối cảnh báo khi feedback trung bình dưới 2★.
        if (avgFeedback != null && avgFeedback < 2)
        {
            sb.Append("<div style=\"background:#fef2f2;border:1px solid #fecaca;border-radius:10px;padding:14px 16px;margin:14px 0\">");
            sb.Append($"<b style=\"color:#b91c1c\">⚠ Cảnh báo chất lượng:</b> <span style=\"color:#7f1d1d\">Điểm feedback trung bình của bạn trong kỳ là <b>{avgFeedback.Value.ToString("0.0", vi)}★</b> (dưới 2★). Vui lòng chủ động trao đổi với Staff Leader để cải thiện chất lượng đón tiếp.</span>");
            sb.Append("</div>");
        }

        // Bảng thông số.
        sb.Append("<table style=\"width:100%;border-collapse:collapse;margin:14px 0\">");
        void Metric(string label, string value)
            => sb.Append($"<tr><td style=\"padding:8px 12px;border:1px solid #e5e7eb;background:#f8fafc;font-weight:600;width:45%\">{label}</td><td style=\"padding:8px 12px;border:1px solid #e5e7eb\">{value}</td></tr>");
        Metric(isStudent ? "Số đoàn đã tham gia" : "Số đoàn phụ trách (host)", visits.Count.ToString(vi));
        Metric("Tổng giờ làm việc", $"{totalHours.ToString("0.#", vi)} giờ");
        Metric("Feedback trung bình", avgFeedback != null ? $"{avgFeedback.Value.ToString("0.0", vi)}★ ({feedbackCount} lượt đánh giá)" : "Chưa có đánh giá");
        Metric("Số lần từ chối", declinedCount.ToString(vi));
        if (!string.IsNullOrWhiteSpace(note)) Metric("Ghi chú của Staff Leader", E(note.Trim()));
        sb.Append("</table>");

        // Danh sách đoàn.
        sb.Append($"<div style=\"font-weight:700;color:#004c91;margin:18px 0 8px\">{(isStudent ? "Danh sách đoàn đã tham gia" : "Danh sách đoàn đã phụ trách")} ({visits.Count})</div>");
        if (visits.Count == 0)
        {
            sb.Append("<p style=\"color:#6b7280\">Không có đoàn nào trong kỳ báo cáo.</p>");
        }
        else
        {
            sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\">");
            sb.Append("<tr style=\"background:#004c91;color:#fff\">"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">STT</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Mã đơn</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Đoàn khách</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Thời gian</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Số giờ</th>"
                + "<th style=\"padding:8px;border:1px solid #e5e7eb;text-align:left\">Trạng thái</th></tr>");
            var i = 0;
            foreach (var v in visits.OrderBy(v => v.Start))
            {
                i++;
                var hours = Math.Max(0, (v.End - v.Start).TotalHours);
                sb.Append($"<tr{(i % 2 == 0 ? " style=\"background:#f8fafc\"" : "")}>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{i}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(v.Code)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(v.Delegation)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{v.Start:HH:mm dd/MM/yyyy} – {v.End:HH:mm dd/MM/yyyy}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{hours.ToString("0.#", vi)}</td>"
                    + $"<td style=\"padding:7px 8px;border:1px solid #e5e7eb\">{E(StatusLabel(v.Status))}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append($"<p style=\"margin-top:20px\">Trân trọng,<br/><b>{E(leaderName)}</b><br/>Staff Leader · {E(campusName)}</p>");
        sb.Append("<p style=\"font-size:11px;color:#9ca3af;border-top:1px solid #e5e7eb;padding-top:10px\">Email được tạo tự động từ hệ thống PEMS — Partnership Engagement Management System.</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }
}
