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
using PEMS.Shared;

namespace PEMS.Application.Reports.Commands.SendStaffLeaderDepartmentReport;

/// <summary>
/// Gửi email báo cáo hiệu suất phối hợp cho 1 PHÒNG BAN từ bảng "Báo cáo phòng ban khác"
/// trên trang báo cáo của Staff Leader. Người nhận: trưởng phòng (Department Leader) của
/// phòng ban đó — mỗi người một thư riêng. Nội dung thư đến từ <c>email_templates</c>
/// (REPORT_DEPARTMENT_COLLABORATION); số liệu và danh sách nhiệm vụ đi kèm trong tệp PDF.
/// </summary>
public sealed class SendStaffLeaderDepartmentReportCommand : IRequest<SendStaffLeaderDepartmentReportResult>
{
    public ulong DepartmentId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>Ghi chú của Staff Leader nhập trên bảng (đưa vào báo cáo).</summary>
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
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReportEmailSender _reportEmail;

    public SendStaffLeaderDepartmentReportCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IReportEmailSender reportEmail)
    {
        _db = db;
        _currentUser = currentUser;
        _reportEmail = reportEmail;
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
                    // Instance-scoped row → THIS campus instance's own detail name.
                    DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
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
                    // Instance-scoped row → THIS campus instance's own detail name.
                    DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
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

        // ── Danh sách nhiệm vụ (đơn + thư mời) đưa vào báo cáo ──
        var taskRows = logisticsRows
            .Select(l => (Kind: "Đơn hậu cần", Content: l.Title, l.DelegationName, Time: l.StartAt, Status: LogisticsStatusLabel(l.Status)))
            .Concat(invitationGroups.Select(g => (Kind: "Thư mời hỗ trợ", Content: "Tham gia hỗ trợ tiếp khách",
                g.DelegationName, Time: g.PlannedStartAt, Status: InvitationStatusLabel(g.Status))))
            .OrderBy(t => t.Time)
            .ToList();

        var (periodFrom, periodTo) = ReportPeriod.Labels(fromVn, toVnExclusive);

        // Cùng một phòng ban → cùng một báo cáo; dựng PDF một lần và gửi cho từng trưởng phòng.
        var pdf = ReportPdf.Render(BuildDocument(
            dept.Name, campusName, leaderName, periodFrom, periodTo, nowVn,
            totalRequests, completedCount, rejectedCount, avgFeedback, ratings.Count,
            taskRows, request.Note));
        var fileName = ReportAttachmentName.Build("BaoCao_PhoiHop_PhongBan", nowVn);

        // Mỗi trưởng phòng một thư riêng — không ai nhìn thấy địa chỉ của người còn lại.
        foreach (var leader in leaders)
        {
            await _reportEmail.SendAsync(
                new ReportEmailMessage(
                    SystemEmailTemplates.ReportDepartmentCollaboration,
                    new EmailRecipient(leader.Email!, leader.FullName),
                    new Dictionary<string, string>
                    {
                        ["recipientName"] = leader.FullName,
                        ["departmentName"] = dept.Name,
                        ["periodFrom"] = periodFrom,
                        ["periodTo"] = periodTo,
                    },
                    fileName,
                    pdf,
                    _currentUser.UserId,
                    ReportEmailRelatedTypes.Department,
                    dept.DepartmentId),
                cancellationToken);
        }

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

    private static ReportPdfModel BuildDocument(
        string deptName, string campusName, string leaderName,
        string periodFrom, string periodTo, DateTime nowVn,
        int totalRequests, int completedCount, int rejectedCount, double? avgFeedback, int feedbackCount,
        List<(string Kind, string Content, string DelegationName, DateTime Time, string Status)> tasks,
        string? note)
    {
        var blocks = new List<ReportPdfBlock>();

        if (avgFeedback != null && avgFeedback < 2)
        {
            blocks.Add(new ReportPdfBlock.Warning(
                $"Cảnh báo chất lượng: Điểm feedback trung bình của phòng ban trong kỳ là "
                + $"{avgFeedback.Value.ToString("0.0", Vi)}★ (dưới 2★). Đề nghị phòng ban rà soát chất lượng "
                + "hỗ trợ tiếp khách và trao đổi với Văn phòng IC để cải thiện."));
        }

        var metrics = new List<ReportPdfMetric>
        {
            new("Tổng đơn/thư yêu cầu", totalRequests.ToString(Vi)),
            new("Hoàn thành", completedCount.ToString(Vi)),
            new("Từ chối", rejectedCount.ToString(Vi)),
            new("Feedback trung bình", avgFeedback != null
                ? $"{avgFeedback.Value.ToString("0.0", Vi)}★ ({feedbackCount} lượt đánh giá)"
                : "Chưa có đánh giá"),
        };
        if (!string.IsNullOrWhiteSpace(note))
            metrics.Add(new ReportPdfMetric("Ghi chú của Staff Leader", note.Trim()));
        blocks.Add(new ReportPdfBlock.Metrics(metrics));

        blocks.Add(new ReportPdfBlock.Table(
            "Danh sách nhiệm vụ phòng ban đã nhận trong kỳ",
            new[]
            {
                new ReportPdfColumn("STT", 28, Fixed: true),
                new ReportPdfColumn("Loại", 1.4f),
                new ReportPdfColumn("Nội dung", 2.6f),
                new ReportPdfColumn("Đoàn khách", 2.2f),
                new ReportPdfColumn("Thời gian", 1.8f),
                new ReportPdfColumn("Trạng thái", 1.4f),
            },
            tasks.Select((t, i) => (IReadOnlyList<string>)new[]
            {
                (i + 1).ToString(Vi),
                t.Kind,
                t.Content,
                t.DelegationName ?? string.Empty,
                t.Time.ToString("HH:mm dd/MM/yyyy"),
                t.Status,
            }).ToList(),
            "Phòng ban không nhận đơn yêu cầu/thư mời nào trong kỳ báo cáo."));

        blocks.Add(new ReportPdfBlock.Note("Người gửi", $"{leaderName} · Staff Leader · {campusName}"));

        return new ReportPdfModel(
            "BÁO CÁO PHỐI HỢP TIẾP KHÁCH CỦA PHÒNG BAN",
            $"{deptName} · {campusName} · Kỳ {periodFrom} – {periodTo} · Lập lúc {nowVn:HH:mm dd/MM/yyyy}",
            blocks);
    }
}
