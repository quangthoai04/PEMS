using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Emails;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.RemindExpenseReports;

public class RemindExpenseReportsCommandHandler : IRequestHandler<RemindExpenseReportsCommand, RemindExpenseReportsResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _clock;
    private readonly IEmailService _email;
    private readonly IEmailActionTokenService _tokens;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public RemindExpenseReportsCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService clock,
        IEmailService email,
        IEmailActionTokenService tokens,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _clock = clock;
        _email = email;
        _tokens = tokens;
        _notificationService = notificationService;
    }

    public async Task<RemindExpenseReportsResultDto> Handle(RemindExpenseReportsCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var instance = await _context.VisitRequestCampuses
            .FirstOrDefaultAsync(v => v.VisitInstanceId == request.VisitInstanceId, cancellationToken);
        if (instance == null)
            throw new NotFoundException(nameof(VisitRequestCampus), request.VisitInstanceId);

        if (instance.CurrentHostUserId != currentUserId)
            throw new ForbiddenException("Only the Host can send expense reminders.");

        if (instance.Status != PEMS.Shared.VisitInstanceStatus.AfterVisit)
            throw new ForbiddenException("Expense reminders are only available in AFTER_VISIT state.");

        var activeStatuses = new[] { "ACCEPTED", "IN_PROGRESS", "DONE" };
        var items = await _context.VisitLogisticsItems
            .Include(l => l.Handovers)
            .Where(l => l.VisitInstanceId == request.VisitInstanceId
                        && l.CoordinationMode == "SYSTEM_REQUEST"
                        && activeStatuses.Contains(l.Status))
            .ToListAsync(cancellationToken);

        var reports = await _context.VisitExpenseReports
            .Where(r => r.VisitInstanceId == request.VisitInstanceId && r.ReportScope == "LOGISTICS")
            .ToListAsync(cancellationToken);
        var reportByItem = reports
            .Where(r => r.LogisticsItemId.HasValue)
            .ToDictionary(r => r.LogisticsItemId!.Value);

        // Đơn có thể kê khai chi phí (đã ký trả xong nếu là tài sản mượn, hoặc DONE nếu không có
        // bước trả — cùng gate với GetOrCreateLogisticsExpenseReport) mà chưa lưu bảng kê.
        var pendingItems = items.Where(l =>
        {
            bool hasBorrow = l.Handovers.Any(h => h.HandoverType == "BORROW");
            bool entryOpen = hasBorrow
                ? l.Handovers.Any(h => h.HandoverType == "RETURN" && h.ProviderSignedBy != null)
                : l.Status == "DONE";
            if (!entryOpen) return false;

            return !reportByItem.TryGetValue(l.LogisticsItemId, out var report) || report.Status == "DRAFT";
        }).ToList();

        var result = new RemindExpenseReportsResultDto();
        if (pendingItems.Count == 0) return result;

        var delegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_context, new[] { request.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(request.VisitInstanceId) ?? "FPT University";

        var now = _clock.VietnamNow;
        var emailsToSend = new List<(ulong SentEmailId, ulong RecipientRowId, string Email, string Subject, string Body)>();

        await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
        {
            foreach (var item in pendingItems)
            {
                // Người phụ trách đơn; chưa gán thì nhắc trưởng phòng của phòng ban được yêu cầu.
                var recipients = new List<Domain.Entities.Users.User>();
                if (item.AssignedToUserId.HasValue)
                {
                    var assignee = await _context.Users
                        .FirstOrDefaultAsync(u => u.UserId == item.AssignedToUserId.Value && u.Status == "ACTIVE", cancellationToken);
                    if (assignee != null) recipients.Add(assignee);
                }
                if (recipients.Count == 0 && item.RequestedToDepartmentId.HasValue)
                {
                    recipients = await _context.Users
                        .Where(u => u.DepartmentId == item.RequestedToDepartmentId.Value
                                    && u.Role.RoleCode == RoleCodes.Department
                                    && u.SubRole == UserSubRoles.Leader
                                    && u.Status == "ACTIVE")
                        .ToListAsync(cancellationToken);
                }
                if (recipients.Count == 0) continue;

                var actionUrl = item.RequestedToDepartmentId.HasValue
                    ? $"/dashboard/departments/{item.RequestedToDepartmentId.Value}/tasks/{item.LogisticsItemId}"
                    : $"/dashboard/visit/process/{item.VisitInstanceId}";
                var detailUrl = _tokens.BuildLogisticsDetailUrl(item.LogisticsItemId);
                var subject = $"[PEMS] Nhắc nhở kê khai chi phí — {item.Title}";

                foreach (var recipient in recipients)
                {
                    await _notificationService.CreateAsync(
                        new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                            RecipientUserId: recipient.UserId,
                            Title: "Nhắc nhở kê khai chi phí",
                            Message: $"Host nhắc bạn nhập chi phí hoặc xác nhận \"Không có chi phí\" cho hạng mục \"{item.Title}\" của đoàn {delegationName}.",
                            NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.ExpenseReportReminder,
                            RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                            RelatedId: item.LogisticsItemId,
                            ActorUserId: currentUserId,
                            Category: PEMS.Application.Notifications.Common.NotificationCategories.Reminder,
                            IsActionRequired: true,
                            VisitInstanceId: item.VisitInstanceId,
                            ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                            ActionUrl: actionUrl),
                        cancellationToken);

                    var body = EmailComposition.BrandedShell(
                        ReminderContentHtml(recipient.FullName, delegationName, item.Title)
                        + EmailComposition.DetailLinkBlock(detailUrl, "Mở biên bản để kê khai chi phí"));

                    var sentEmail = new SentEmail
                    {
                        RelatedType = EmailActionTargetTypes.LogisticsItem,
                        RelatedId = item.LogisticsItemId,
                        Subject = subject,
                        BodySnapshot = body,
                        Status = "QUEUED",
                        SentBy = currentUserId,
                        CreatedAt = now,
                    };
                    _context.SentEmails.Add(sentEmail);
                    await _context.SaveChangesAsync(cancellationToken);

                    var sentRecipient = new SentEmailRecipient
                    {
                        SentEmailId = sentEmail.SentEmailId,
                        RecipientEmail = recipient.Email,
                        RecipientName = recipient.FullName,
                        RecipientType = "TO",
                        DeliveryStatus = "QUEUED",
                        CreatedAt = now,
                    };
                    _context.SentEmailRecipients.Add(sentRecipient);
                    await _context.SaveChangesAsync(cancellationToken);

                    emailsToSend.Add((sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, recipient.Email, subject, body));
                    if (!result.Recipients.Contains(recipient.FullName))
                        result.Recipients.Add(recipient.FullName);
                }

                result.RemindedCount++;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        // Gửi mail sau khi đã commit — lỗi SMTP không làm mất thông báo hệ thống.
        foreach (var mail in emailsToSend)
        {
            try
            {
                await _email.SendAsync(mail.Email, mail.Subject, mail.Body, cancellationToken);
                await UpdateEmailStatusAsync(mail.SentEmailId, mail.RecipientRowId, "SENT", now, null, cancellationToken);
            }
            catch (Exception ex)
            {
                await UpdateEmailStatusAsync(mail.SentEmailId, mail.RecipientRowId, "FAILED", now, ex.Message, cancellationToken);
            }
        }

        return result;
    }

    private static string ReminderContentHtml(string recipientName, string delegationName, string itemTitle)
    {
        string HE(string? s) => EmailComposition.HE(s);
        return $@"<p>Xin chào <strong>{HE(recipientName)}</strong>,</p>
<p>Host đón tiếp nhắc bạn hoàn tất <strong>ghi chú chi phí</strong> cho hạng mục hậu cần
<strong>{HE(itemTitle)}</strong> của đoàn <strong>{HE(delegationName)}</strong>.</p>
<div style=""background:#fff7ed;border-left:4px solid #f37021;border-radius:8px;padding:16px 20px;margin:20px 0"">
  <p style=""margin:0;line-height:1.7"">Vui lòng mở biên bản bàn giao &amp; nghiệm thu của đơn yêu cầu và:</p>
  <ul style=""margin:8px 0 0;padding-left:20px;line-height:1.7"">
    <li>Nhập bảng kê chi phí thực tế; hoặc</li>
    <li>Bấm <strong>“Không có chi phí”</strong> nếu hạng mục không phát sinh chi phí.</li>
  </ul>
</div>
<p>Đoàn chỉ có thể chốt hồ sơ sau khi tất cả chi phí đã được xác nhận.</p>";
    }

    private async Task UpdateEmailStatusAsync(
        ulong sentEmailId, ulong sentEmailRecipientId, string status, DateTime now, string? error, CancellationToken ct)
    {
        var sentEmail = await _context.SentEmails.FirstOrDefaultAsync(e => e.SentEmailId == sentEmailId, ct);
        if (sentEmail != null)
        {
            sentEmail.Status = status;
            sentEmail.LastAttemptAt = now;
            sentEmail.RetryCount += 1;
            if (status == "SENT") sentEmail.SentAt = now;
            else sentEmail.ErrorMessage = Truncate(error, 1000);
        }
        var rec = await _context.SentEmailRecipients.FirstOrDefaultAsync(r => r.SentEmailRecipientId == sentEmailRecipientId, ct);
        if (rec != null)
        {
            rec.DeliveryStatus = status;
            if (status == "SENT") rec.SentAt = now;
            else rec.ErrorMessage = Truncate(error, 1000);
        }
        await _context.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
