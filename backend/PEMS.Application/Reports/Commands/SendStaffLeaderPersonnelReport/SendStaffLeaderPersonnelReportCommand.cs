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

namespace PEMS.Application.Reports.Commands.SendStaffLeaderPersonnelReport;

/// <summary>
/// Gửi email báo cáo hiệu suất cá nhân cho 1 nhân sự/student từ bảng nhân sự trên trang
/// báo cáo của Staff Leader. Nội dung thư đến từ <c>email_templates</c>
/// (REPORT_PERSONNEL_PERFORMANCE); số liệu và danh sách đoàn đi kèm trong tệp PDF.
/// </summary>
public sealed class SendStaffLeaderPersonnelReportCommand : IRequest<SendStaffLeaderPersonnelReportResult>
{
    public ulong UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>Ghi chú của Staff Leader nhập trên bảng (đưa vào báo cáo).</summary>
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
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReportEmailSender _reportEmail;

    public SendStaffLeaderPersonnelReportCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IReportEmailSender reportEmail)
    {
        _db = db;
        _currentUser = currentUser;
        _reportEmail = reportEmail;
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
                        DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
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
                        DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
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

        // Phạm vi thống kê — cùng một cụm từ cho tiêu đề thư và trang bìa PDF.
        var scopeLabel = PersonnelReportScopes.Label(
            isStudent ? PersonnelReportScope.VisitSupport : PersonnelReportScope.DelegationHosting,
            EmailLanguages.Vi);
        var (periodFrom, periodTo) = ReportPeriod.Labels(fromVn, toVnExclusive);

        var pdf = ReportPdf.Render(BuildDocument(
            person.FullName, roleLabel, campusName, leaderName, scopeLabel,
            periodFrom, periodTo, nowVn,
            visitRows, isStudent, totalHours, avg, ratings.Count, declinedCount, request.Note));

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

    private static ReportPdfModel BuildDocument(
        string fullName, string roleLabel, string campusName, string leaderName, string scopeLabel,
        string periodFrom, string periodTo, DateTime nowVn,
        List<(string Code, string Delegation, DateTime Start, DateTime End, string Status)> visits,
        bool isStudent, double totalHours, double? avgFeedback, int feedbackCount, int declinedCount,
        string? note)
    {
        var blocks = new List<ReportPdfBlock>();

        if (avgFeedback != null && avgFeedback < 2)
        {
            blocks.Add(new ReportPdfBlock.Warning(
                $"Cảnh báo chất lượng: Điểm feedback trung bình của bạn trong kỳ là "
                + $"{avgFeedback.Value.ToString("0.0", Vi)}★ (dưới 2★). Vui lòng chủ động trao đổi với "
                + "Staff Leader để cải thiện chất lượng đón tiếp."));
        }

        var metrics = new List<ReportPdfMetric>
        {
            new(isStudent ? "Số đoàn đã tham gia" : "Số đoàn phụ trách (host)", visits.Count.ToString(Vi)),
            new("Tổng giờ làm việc", $"{totalHours.ToString("0.#", Vi)} giờ"),
            new("Feedback trung bình", avgFeedback != null
                ? $"{avgFeedback.Value.ToString("0.0", Vi)}★ ({feedbackCount} lượt đánh giá)"
                : "Chưa có đánh giá"),
            new("Số lần từ chối", declinedCount.ToString(Vi)),
        };
        if (!string.IsNullOrWhiteSpace(note))
            metrics.Add(new ReportPdfMetric("Ghi chú của Staff Leader", note.Trim()));
        blocks.Add(new ReportPdfBlock.Metrics(metrics));

        blocks.Add(new ReportPdfBlock.Table(
            isStudent ? "Danh sách đoàn đã tham gia" : "Danh sách đoàn đã phụ trách",
            new[]
            {
                new ReportPdfColumn("STT", 28, Fixed: true),
                new ReportPdfColumn("Mã đơn", 1.4f),
                new ReportPdfColumn("Đoàn khách", 2.6f),
                new ReportPdfColumn("Thời gian", 2.6f),
                new ReportPdfColumn("Số giờ", 0.9f, AlignRight: true),
                new ReportPdfColumn("Trạng thái", 1.4f),
            },
            visits.OrderBy(v => v.Start).Select((v, i) => (IReadOnlyList<string>)new[]
            {
                (i + 1).ToString(Vi),
                v.Code,
                v.Delegation,
                $"{v.Start:HH:mm dd/MM/yyyy} – {v.End:HH:mm dd/MM/yyyy}",
                Math.Max(0, (v.End - v.Start).TotalHours).ToString("0.#", Vi),
                StatusLabel(v.Status),
            }).ToList(),
            "Không có đoàn nào trong kỳ báo cáo."));

        blocks.Add(new ReportPdfBlock.Note("Người gửi", $"{leaderName} · Staff Leader · {campusName}"));

        return new ReportPdfModel(
            $"BÁO CÁO HIỆU SUẤT {scopeLabel.ToUpperInvariant()}",
            $"{fullName} · {roleLabel} · {campusName} · Kỳ {periodFrom} – {periodTo} "
            + $"· Lập lúc {nowVn:HH:mm dd/MM/yyyy}",
            blocks);
    }
}
