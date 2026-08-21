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
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IEmailActionTokenService _tokens;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public RemindExpenseReportsCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService clock,
        ISystemEmailDispatcher dispatcher,
        IEmailActionTokenService tokens,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _clock = clock;
        _dispatcher = dispatcher;
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
        // One message per person — the recipient list comes from the items being chased, never from the
        // template, and nobody is added as a silent copy.
        var prepared = new List<PreparedSystemEmail>();

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
                // recipients is either the one assignee (Staff) or the department's Leaders — never
                // mixed for one item — so the role that decides the detail-link shape is known here.
                var detailUrl = item.AssignedToUserId.HasValue
                    ? _tokens.BuildDepartmentStaffLogisticsTaskUrl(item.LogisticsItemId)
                    : _tokens.BuildDepartmentLeaderLogisticsTaskUrl(item.LogisticsItemId);

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
                            ActionUrl: actionUrl,
                            MetadataJson: PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                                PEMS.Application.Notifications.Common.NotificationEventKeys.LogisticsExpenseReminder,
                                new { delegationName })),
                        cancellationToken);

                    prepared.Add(await _dispatcher.PrepareAsync(
                        new SystemEmailRequest(
                            SystemEmailTemplates.LogisticsExpenseReportReminder,
                            new EmailRecipient(recipient.Email, recipient.FullName),
                            new Dictionary<string, string>
                            {
                                ["recipientName"] = recipient.FullName,
                                ["itemTitle"] = item.Title,
                                ["dueAt"] = item.DueAt?.ToString("HH:mm dd/MM/yyyy") ?? "Chưa đặt hạn",
                                ["delegationName"] = delegationName,
                            },
                            TrustedBlocks: new Dictionary<string, string>
                            {
                                // A login-required detail link, not a one-time token: this reminder grants
                                // nothing on its own, which is why its body is kept in full. The note is
                                // supplied explicitly — the block's default sentence describes the
                                // Department Leader's accept/reject/assign options, which is a different
                                // flow and would tell this recipient to do something that is not there.
                                [EmailTrustedBlocks.ActionBlock] =
                                    EmailComposition.DetailLinkBlock(
                                        detailUrl,
                                        "Mở biên bản để kê khai chi phí",
                                        "Sau khi đăng nhập, bạn có thể nhập chi phí hoặc xác nhận \"Không có chi phí\" cho hạng mục này."),
                            },
                            RelatedType: EmailActionTargetTypes.LogisticsItem,
                            RelatedId: item.LogisticsItemId,
                            SentBy: currentUserId),
                        cancellationToken));

                    if (!result.Recipients.Contains(recipient.FullName))
                        result.Recipients.Add(recipient.FullName);
                }

                result.RemindedCount++;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        // Gửi mail sau khi đã commit — lỗi SMTP không làm mất thông báo hệ thống. The dispatcher records
        // each outcome truthfully and never throws on a delivery failure, so one bad address cannot stop
        // the rest of the reminders.
        foreach (var message in prepared)
            await _dispatcher.DeliverAsync(message, cancellationToken);

        return result;
    }
}
