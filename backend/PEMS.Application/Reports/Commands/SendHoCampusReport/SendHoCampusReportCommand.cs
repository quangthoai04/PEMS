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
using PEMS.Application.Emails.Idempotency;
using PEMS.Application.Reports.Common;
using PEMS.Application.Reports.Queries.GetHoReportV2;
using PEMS.Shared;

namespace PEMS.Application.Reports.Commands.SendHoCampusReport;

/// <summary>
/// HO gửi email báo cáo vận hành của MỘT campus (số liệu trong kỳ đang lọc)
/// cho Staff Leader của campus đó. Nội dung thư đến từ <c>email_templates</c>
/// (REPORT_CAMPUS_OPERATION); các con số đi kèm trong tệp PDF.
/// </summary>
public sealed class SendHoCampusReportCommand : IRequest<SendHoCampusReportResult>, IIdempotentEmailSend
{
    public ulong CampusId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>Ghi chú HO nhập trên bảng campus (đưa vào báo cáo).</summary>
    public string? Note { get; set; }

    /// <inheritdoc />
    public string OperationCode => EmailSendOperations.HoCampusReport;

    /// <inheritdoc />
    public void DescribeRequest(EmailSendFingerprintBuilder builder) =>
        builder.Id("campus", CampusId)
               .Date("from", FromDate)
               .Date("to", ToDate)
               .Text("note", Note);
}

public sealed class SendHoCampusReportResult : IEmailSendResult
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
    private readonly IReportEmailSender _reportEmail;

    public SendHoCampusReportCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IReportEmailSender reportEmail)
    {
        _db = db;
        _currentUser = currentUser;
        _reportEmail = reportEmail;
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

        // Kỳ báo cáo: cùng một cặp nhãn cho email và PDF.
        var (periodFrom, periodTo) = ReportPeriod.Labels(fromVn, toVnExclusive);

        var pdf = ReportPdf.Render(BuildDocument(
            campusName, leader.FullName, senderName, periodFrom, periodTo, nowVn,
            statusRows, totalGuests, totalPartners, avg, ratings.Count, request.Note));

        await _reportEmail.SendAsync(
            new ReportEmailMessage(
                SystemEmailTemplates.ReportCampusOperation,
                new EmailRecipient(leader.Email!, leader.FullName),
                new Dictionary<string, string>
                {
                    ["recipientName"] = leader.FullName,
                    ["campusName"] = campusName,
                    ["periodFrom"] = periodFrom,
                    ["periodTo"] = periodTo,
                },
                ReportAttachmentName.Build("BaoCao_VanHanh_Campus", nowVn),
                pdf,
                _currentUser.UserId,
                ReportEmailRelatedTypes.Campus,
                request.CampusId),
            cancellationToken);

        return new SendHoCampusReportResult { Success = true, Message = $"Đã gửi báo cáo tới {leader.Email}." };
    }

    private static ReportPdfModel BuildDocument(
        string campusName, string leaderName, string senderName,
        string periodFrom, string periodTo, DateTime nowVn,
        List<string> statusRows, int totalGuests, int totalPartners,
        double? avg, int feedbackCount, string? note)
    {
        int Count(string s) => statusRows.Count(x => x == s);

        var blocks = new List<ReportPdfBlock>();

        if (avg != null && avg < 2)
        {
            blocks.Add(new ReportPdfBlock.Warning(
                $"Cảnh báo chất lượng: Feedback trung bình của campus trong kỳ là {avg.Value.ToString("0.0", Vi)}★ "
                + "(dưới 2★). Đề nghị campus rà soát quy trình đón tiếp và phản hồi Head Office."));
        }

        var metrics = new List<ReportPdfMetric>
        {
            new("Tổng đoàn khách", statusRows.Count.ToString(Vi)),
            new("Tổng khách", totalGuests.ToString(Vi)),
            new("Đã hoàn thành (đóng đoàn)", Count(VisitInstanceStatus.Closed).ToString(Vi)),
            new("Bị hủy", Count(VisitInstanceStatus.Cancelled).ToString(Vi)),
            new("Từ chối", Count(VisitInstanceStatus.Rejected).ToString(Vi)),
            new("Tổng đối tác", totalPartners.ToString(Vi)),
            new("Feedback trung bình", avg != null
                ? $"{avg.Value.ToString("0.0", Vi)}★ ({feedbackCount} lượt đánh giá)"
                : "Chưa có đánh giá"),
        };
        if (!string.IsNullOrWhiteSpace(note))
            metrics.Add(new ReportPdfMetric("Ghi chú của Head Office", note.Trim()));
        blocks.Add(new ReportPdfBlock.Metrics(metrics));

        blocks.Add(new ReportPdfBlock.Note("Người nhận", $"{leaderName} · Staff Leader · {campusName}"));
        blocks.Add(new ReportPdfBlock.Note("Người gửi", $"{senderName} · Head Office · FPT University"));

        return new ReportPdfModel(
            "BÁO CÁO VẬN HÀNH CAMPUS",
            $"{campusName} · Kỳ {periodFrom} – {periodTo} · Lập lúc {nowVn:HH:mm dd/MM/yyyy}",
            blocks);
    }
}
