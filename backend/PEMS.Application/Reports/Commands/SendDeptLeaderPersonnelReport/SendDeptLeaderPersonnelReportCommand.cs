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
using PEMS.Application.Reports.Queries.GetDeptLeaderReportV2;

namespace PEMS.Application.Reports.Commands.SendDeptLeaderPersonnelReport;

/// <summary>
/// Gửi email báo cáo hiệu suất cá nhân cho 1 nhân sự phòng ban từ bảng nhân sự trên trang
/// báo cáo của Department Leader. Nội dung thư đến từ <c>email_templates</c>
/// (REPORT_PERSONNEL_PERFORMANCE); số liệu và danh sách nhiệm vụ đi kèm trong tệp PDF.
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
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReportEmailSender _reportEmail;

    public SendDeptLeaderPersonnelReportCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IReportEmailSender reportEmail)
    {
        _db = db;
        _currentUser = currentUser;
        _reportEmail = reportEmail;
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
                    DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
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
                    DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
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

        var scopeLabel = PersonnelReportScopes.Label(PersonnelReportScope.VisitAssignments, EmailLanguages.Vi);
        var (periodFrom, periodTo) = ReportPeriod.Labels(fromVn, toVnExclusive);

        var pdf = ReportPdf.Render(BuildDocument(
            person.FullName, roleLabel, deptName, leaderName, scopeLabel,
            periodFrom, periodTo, nowVn,
            taskRows, totalHours, avg, ratings.Count, declinedCount, request.Note));

        await _reportEmail.SendAsync(
            new ReportEmailMessage(
                SystemEmailTemplates.ReportPersonnelPerformance,
                new EmailRecipient(person.Email!, person.FullName),
                new Dictionary<string, string>
                {
                    ["personName"] = person.FullName,
                    ["scopeLabel"] = scopeLabel,
                    ["periodFrom"] = periodFrom,
                    ["periodTo"] = periodTo,
                },
                ReportAttachmentName.Build("BaoCao_HieuSuat_CaNhan", nowVn),
                pdf,
                _currentUser.UserId,
                ReportEmailRelatedTypes.User,
                person.UserId),
            cancellationToken);

        return new SendDeptLeaderPersonnelReportResult { Success = true, Message = $"Đã gửi báo cáo tới {person.Email}." };
    }

    private static ReportPdfModel BuildDocument(
        string fullName, string roleLabel, string deptName, string leaderName, string scopeLabel,
        string periodFrom, string periodTo, DateTime nowVn,
        List<(string Code, string Delegation, DateTime Start, DateTime End, string Status, string Kind)> tasks,
        double totalHours, double? avgFeedback, int feedbackCount, int declinedCount, string? note)
    {
        var blocks = new List<ReportPdfBlock>();

        if (avgFeedback != null && avgFeedback < 2)
        {
            blocks.Add(new ReportPdfBlock.Warning(
                $"Cảnh báo chất lượng: Điểm feedback trung bình của bạn trong kỳ là "
                + $"{avgFeedback.Value.ToString("0.0", Vi)}★ (dưới 2★). Vui lòng chủ động trao đổi với "
                + "Trưởng phòng ban để cải thiện chất lượng phối hợp."));
        }

        var metrics = new List<ReportPdfMetric>
        {
            new("Số nhiệm vụ phụ trách", tasks.Count.ToString(Vi)),
            new("Tổng giờ làm việc", $"{totalHours.ToString("0.#", Vi)} giờ"),
            new("Feedback trung bình", avgFeedback != null
                ? $"{avgFeedback.Value.ToString("0.0", Vi)}★ ({feedbackCount} lượt đánh giá)"
                : "Chưa có đánh giá"),
            new("Số lần từ chối", declinedCount.ToString(Vi)),
        };
        if (!string.IsNullOrWhiteSpace(note))
            metrics.Add(new ReportPdfMetric("Ghi chú của Trưởng phòng ban", note.Trim()));
        blocks.Add(new ReportPdfBlock.Metrics(metrics));

        blocks.Add(new ReportPdfBlock.Table(
            "Danh sách nhiệm vụ đã tham gia",
            new[]
            {
                new ReportPdfColumn("STT", 28, Fixed: true),
                new ReportPdfColumn("Loại", 1.2f),
                new ReportPdfColumn("Mã đơn", 1.4f),
                new ReportPdfColumn("Đoàn khách", 2.4f),
                new ReportPdfColumn("Thời gian", 2.6f),
                new ReportPdfColumn("Số giờ", 0.9f, AlignRight: true),
            },
            tasks.Select((t, i) => (IReadOnlyList<string>)new[]
            {
                (i + 1).ToString(Vi),
                t.Kind,
                t.Code,
                t.Delegation,
                $"{t.Start:HH:mm dd/MM/yyyy} – {t.End:HH:mm dd/MM/yyyy}",
                Math.Max(0, (t.End - t.Start).TotalHours).ToString("0.#", Vi),
            }).ToList(),
            "Không có nhiệm vụ nào trong kỳ báo cáo."));

        blocks.Add(new ReportPdfBlock.Note("Người gửi", $"{leaderName} · Department Leader · {deptName}"));

        return new ReportPdfModel(
            $"BÁO CÁO HIỆU SUẤT {scopeLabel.ToUpperInvariant()}",
            $"{fullName} · {roleLabel} · {deptName} · Kỳ {periodFrom} – {periodTo} "
            + $"· Lập lúc {nowVn:HH:mm dd/MM/yyyy}",
            blocks);
    }
}
