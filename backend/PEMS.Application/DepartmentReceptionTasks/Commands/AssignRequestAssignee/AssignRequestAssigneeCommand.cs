using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Users;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.AssignRequestAssignee
{
    /// <summary>
    /// Department Leader assigns a staff member to a logistics request. Sets the item ASSIGNED, then
    /// notifies + emails the assignee with one-time ACCEPT/DECLINE tokens (LOGISTICS_ASSIGNEE_RESPONSE).
    /// The assignment is committed before the email is sent so an SMTP failure never loses it.
    /// </summary>
    public class AssignRequestAssigneeCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
        public ulong AssigneeUserId { get; set; }

        /// <summary>Optional Department-Leader-edited subject/body from the "Xem trước email" modal.</summary>
        public EmailOverride? EmailOverride { get; set; }
    }

    public class AssignRequestAssigneeCommandHandler : IRequestHandler<AssignRequestAssigneeCommand, bool>
    {
        private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(14);

        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTimeService _clock;
        private readonly IEmailService _email;
        private readonly IEmailActionTokenService _tokens;
        private readonly IHtmlSanitizerService _sanitizer;
        private readonly IFileStorageService _storage;
        private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

        public AssignRequestAssigneeCommandHandler(
            IApplicationDbContext context, ICurrentUserService currentUserService, IDateTimeService clock,
            IEmailService email, IEmailActionTokenService tokens, IHtmlSanitizerService sanitizer,
            IFileStorageService storage, PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
            PEMS.Application.Notifications.Common.INotificationService notificationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _clock = clock;
            _email = email;
            _tokens = tokens;
            _sanitizer = sanitizer;
            _storage = storage;
            _normalizer = normalizer;
            _notificationService = notificationService;
        }

        public async Task<bool> Handle(AssignRequestAssigneeCommand request, CancellationToken cancellationToken)
        {
            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null) throw new Exception("Không xác định được người dùng hiện tại");

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);
            if (l == null) throw new Exception("Không tìm thấy đơn yêu cầu");

            // Check department scope
            if (l.RequestedToDepartmentId != user.DepartmentId)
                throw new Exception("Không có quyền phân công đơn yêu cầu của phòng ban khác");

            // Block assignment in terminal/in-flight statuses.
            var blockedStatuses = new[] { "ASSIGNED", "ACCEPTED", "CHANGE_PROPOSED", "IN_PROGRESS", "DONE", "CANCELLED", "REJECTED", "DECLINED" };
            if (blockedStatuses.Contains(l.Status))
                throw new Exception("Không thể phân công khi nhiệm vụ đang ở trạng thái: " + l.Status);

            bool hasPendingAttempt = await _context.VisitLogisticsAssignmentAttempts
                .AnyAsync(a => a.LogisticsItemId == request.LogisticsItemId && a.Status == "PENDING", cancellationToken);
            if (hasPendingAttempt)
                throw new ConflictException("Nhiệm vụ đã được phân công và đang chờ phản hồi hoặc đã được nhận.");

            bool hasSigned = await _context.VisitLogisticsItemHandovers
                .AnyAsync(h => h.LogisticsItemId == request.LogisticsItemId &&
                               (h.BorrowerSignedAt != null || h.ProviderSignedAt != null), cancellationToken);
            if (hasSigned)
                throw new Exception("Nhiệm vụ đã được xử lý hoặc đã có ký biên bản, không thể đổi người phụ trách.");

            var assignee = await _context.Users.FirstOrDefaultAsync(
                u => u.UserId == request.AssigneeUserId
                     && u.DepartmentId == user.DepartmentId
                     && u.Status == "ACTIVE",
                cancellationToken);
            if (assignee == null)
                throw new Exception("Người phụ trách không hợp lệ hoặc không thuộc phòng ban");

            var editedContent = ValidateAndSanitizeOverride(request.EmailOverride);
            var attachInputs = OutboundEmailAttachments.From(request.EmailOverride);
            await OutboundEmailAttachments.ValidateAsync(_context, userId, attachInputs, cancellationToken);
            var now = _clock.VietnamNow;
            // Mixed per-campus v2: the email uses THIS instance's detail name.
            var delegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_context, new[] { l.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(l.VisitInstanceId) ?? "FPT University";
            var templateId = await _context.EmailTemplates
                .Where(t => t.TemplateCode == EmailActionTemplates.LogisticsAssigneeAssignment)
                .Select(t => (ulong?)t.EmailTemplateId)
                .FirstOrDefaultAsync(cancellationToken);

            ulong sentEmailId, sentEmailRecipientId;
            string finalSubject, finalBody;

            await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
            {
                _context.VisitLogisticsAssignmentAttempts.Add(new VisitLogisticsAssignmentAttempt
                {
                    LogisticsItemId = request.LogisticsItemId,
                    AssigneeUserId = request.AssigneeUserId,
                    AssignedBy = userId,
                    AssignedAt = now,
                    Status = "PENDING",
                    CreatedAt = now,
                });

                l.AssignedToUserId = request.AssigneeUserId;
                l.AssignedBy = userId;
                l.AssignedAt = now;
                l.Status = "ASSIGNED";
                l.UpdatedBy = userId;
                l.UpdatedAt = now;

                await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                    _context, EmailActionTargetTypes.LogisticsItem, request.LogisticsItemId, "Yêu cầu đã được phân công cho nhân sự khác.", now, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                // One-time ACCEPT/DECLINE tokens for the email buttons.
                var acceptRaw = _tokens.GenerateRawToken();
                var declineRaw = _tokens.GenerateRawToken();
                var groupKey = Guid.NewGuid().ToString("N");
                var acceptUrl = _tokens.BuildPublicActionUrl(acceptRaw);
                var declineUrl = _tokens.BuildPublicActionUrl(declineRaw);

                var detailUrl = _tokens.BuildLogisticsDetailUrl(l.LogisticsItemId);

                if (editedContent != null)
                {
                    finalSubject = request.EmailOverride!.Subject!.Trim();
                    var content = EmailComposition.StripActionArtifacts(editedContent);
                    finalBody = EmailComposition.BrandedShell(content + EmailComposition.LogisticsAssigneeActionBlock(acceptUrl, declineUrl, detailUrl));
                }
                else
                {
                    finalSubject = $"[PEMS] Bạn được phân công xử lý hậu cần — {l.Title}";
                    finalBody = EmailComposition.BrandedShell(
                        DefaultContentHtml(assignee.FullName, delegationName, l) + EmailComposition.LogisticsAssigneeActionBlock(acceptUrl, declineUrl, detailUrl));
                }

                finalSubject = LogisticsPriorityText.ApplySubjectPrefix(l.Priority, finalSubject);
                finalBody = await _normalizer.NormalizeHtmlAsync(finalBody, cancellationToken);

                var sentEmail = new SentEmail
                {
                    EmailTemplateId = templateId,
                    RelatedType = EmailActionTargetTypes.LogisticsItem,
                    RelatedId = l.LogisticsItemId,
                    Subject = finalSubject,
                    BodySnapshot = finalBody,
                    Status = "QUEUED",
                    SentBy = userId,
                    CreatedAt = now,
                };
                // Inline images + file attachments (cascade-insert with the sent_emails row).
                OutboundEmailAttachments.Attach(sentEmail, attachInputs, now);
                _context.SentEmails.Add(sentEmail);
                await _context.SaveChangesAsync(cancellationToken);

                var sentRecipient = new SentEmailRecipient
                {
                    SentEmailId = sentEmail.SentEmailId,
                    RecipientEmail = assignee.Email,
                    RecipientName = assignee.FullName,
                    RecipientType = "TO",
                    DeliveryStatus = "QUEUED",
                    CreatedAt = now,
                };
                _context.SentEmailRecipients.Add(sentRecipient);
                await _context.SaveChangesAsync(cancellationToken);

                _context.EmailActionTokens.Add(NewToken(_tokens.Hash(acceptRaw), EmailIntendedActions.Accept, groupKey, l.LogisticsItemId, assignee.UserId, assignee.Email, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));
                _context.EmailActionTokens.Add(NewToken(_tokens.Hash(declineRaw), EmailIntendedActions.Decline, groupKey, l.LogisticsItemId, assignee.UserId, assignee.Email, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));

                await _notificationService.CreateAsync(
                    new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: assignee.UserId,
                        Title: LogisticsPriorityText.SubjectPrefix(l.Priority) + "Bạn được phân công hậu cần",
                        Message: $"Bạn được phân công xử lý hạng mục \"{l.Title}\" (ưu tiên {LogisticsPriorityText.LabelVi(l.Priority)}) cho đoàn {delegationName}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigned,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                        RelatedId: l.LogisticsItemId,
                        ActorUserId: userId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                        IsActionRequired: true,
                        VisitInstanceId: l.VisitInstanceId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                        ActionUrl: l.RequestedToDepartmentId.HasValue
                            ? $"/dashboard/departments/{l.RequestedToDepartmentId.Value}/tasks/{l.LogisticsItemId}"
                            : $"/dashboard/visit/process/{l.VisitInstanceId}"),
                    cancellationToken
                );
                await _context.SaveChangesAsync(cancellationToken);

                sentEmailId = sentEmail.SentEmailId;
                sentEmailRecipientId = sentRecipient.SentEmailRecipientId;
                await transaction.CommitAsync(cancellationToken);
            }

            try
            {
                // Real MIME path with the leader's inline images (cid) + file attachments.
                var outboundAttachments = await OutboundEmailAttachments.LoadAsync(_context, _storage, attachInputs, cancellationToken);
                await _email.SendAsync(new OutboundEmail
                {
                    ToEmail = assignee.Email,
                    Subject = finalSubject,
                    Body = finalBody,
                    IsHtml = true,
                    Attachments = outboundAttachments,
                }, cancellationToken);
                await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "SENT", userId, now, null, cancellationToken);
            }
            catch (Exception ex)
            {
                await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "FAILED", userId, now, ex.Message, cancellationToken);
            }

            return true;
        }

        private string? ValidateAndSanitizeOverride(EmailOverride? ov)
        {
            if (ov is null || !ov.UseEditedContent) return null;
            if (string.IsNullOrWhiteSpace(ov.Subject))
                throw new ValidationException("Tiêu đề email không được để trống.");
            if (ov.Subject.Trim().Length > EmailOverrideLimits.SubjectMax)
                throw new ValidationException($"Tiêu đề email tối đa {EmailOverrideLimits.SubjectMax} ký tự.");
            if (string.IsNullOrWhiteSpace(ov.BodyHtml))
                throw new ValidationException("Nội dung email không được để trống.");
            if (ov.BodyHtml.Length > EmailOverrideLimits.BodyMax)
                throw new ValidationException($"Nội dung email vượt quá {EmailOverrideLimits.BodyMax} ký tự.");
            // Email-profile sanitize so inline-image <img src="cid:..."> + data-* refs survive.
            var sanitized = _sanitizer.SanitizeEmailHtml(ov.BodyHtml);
            if (string.IsNullOrWhiteSpace(sanitized))
                throw new ValidationException("Nội dung email không hợp lệ sau khi lọc.");
            return sanitized;
        }

        private static string DefaultContentHtml(string assigneeName, string delegationName, VisitLogisticsItem l)
        {
            string HE(string? s) => EmailComposition.HE(s);
            var prio = $"<li><strong>Mức ưu tiên:</strong> {HE(LogisticsPriorityText.LabelVi(l.Priority))}</li>";
            var due = l.DueAt.HasValue ? $"<li><strong>Hạn xử lý:</strong> {HE(l.DueAt.Value.ToString("HH:mm dd/MM/yyyy"))}</li>" : string.Empty;
            return $@"<p>Xin chào <strong>{HE(assigneeName)}</strong>,</p>
<p>Bạn được phân công xử lý hạng mục hậu cần <strong>{HE(l.Title)}</strong> cho đoàn <strong>{HE(delegationName)}</strong>.</p>
<div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
  <ul style=""margin:0;padding-left:20px;line-height:1.7"">
    <li><strong>Hạng mục:</strong> {HE(l.Title)} ({HE(l.ItemType)})</li>
    {prio}
    {due}
  </ul>
</div>
<p>Vui lòng phản hồi bằng một trong các nút dưới đây.</p>";
        }

        private static EmailActionToken NewToken(
            string tokenHash, string intendedAction, string groupKey, ulong logisticsItemId,
            ulong recipientUserId, string recipientEmail, ulong sentEmailId, ulong sentEmailRecipientId, DateTime now)
            => new()
            {
                TokenHash = tokenHash,
                ActionGroupKey = groupKey,
                ActionContext = EmailActionContexts.LogisticsAssigneeResponse,
                TargetType = EmailActionTargetTypes.LogisticsItem,
                TargetId = logisticsItemId,
                IntendedAction = intendedAction,
                RecipientUserId = recipientUserId,
                RecipientEmail = recipientEmail,
                SentEmailId = sentEmailId,
                SentEmailRecipientId = sentEmailRecipientId,
                ExpiresAt = now.Add(TokenTtl),
                ResultStatus = EmailActionResultStatuses.Pending,
                CreatedAt = now,
            };

        private async Task UpdateEmailStatusAsync(
            ulong sentEmailId, ulong sentEmailRecipientId, string status, ulong actorId, DateTime now,
            string? error, CancellationToken ct)
        {
            var sentEmail = await _context.SentEmails.FirstOrDefaultAsync(e => e.SentEmailId == sentEmailId, ct);
            if (sentEmail != null)
            {
                sentEmail.Status = status;
                sentEmail.LastAttemptAt = now;
                sentEmail.RetryCount += 1;
                if (status == "SENT") { sentEmail.SentBy = actorId; sentEmail.SentAt = now; }
                else { sentEmail.ErrorMessage = Truncate(error, 1000); }
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
}
