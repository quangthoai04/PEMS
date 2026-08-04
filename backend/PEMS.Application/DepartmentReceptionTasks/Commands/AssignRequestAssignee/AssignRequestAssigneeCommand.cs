using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentReceptionTasks.Common;
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

        /// <summary>
        /// The message the Department Leader edited and approved in the FINAL preview, or null to send
        /// the template.
        /// </summary>
        public PEMS.Application.Emails.Preview.ApprovedEmailContent? ApprovedContent { get; set; }
    }

    public class AssignRequestAssigneeCommandHandler : IRequestHandler<AssignRequestAssigneeCommand, bool>
    {
        private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(14);

        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTimeService _clock;
        private readonly ISystemEmailDispatcher _dispatcher;
        private readonly IEmailActionTokenService _tokens;
        private readonly IHtmlSanitizerService _sanitizer;
        private readonly IFileStorageService _storage;
        private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
        private readonly IUserMutationLockService _lockService;
        private readonly PEMS.Application.Emails.Preview.IApprovedEmailContentResolver _approvedContent;

        public AssignRequestAssigneeCommandHandler(
            IApplicationDbContext context, ICurrentUserService currentUserService, IDateTimeService clock,
            ISystemEmailDispatcher dispatcher, IEmailActionTokenService tokens, IHtmlSanitizerService sanitizer,
            IFileStorageService storage, PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
            PEMS.Application.Notifications.Common.INotificationService notificationService,
            IUserMutationLockService lockService,
            PEMS.Application.Emails.Preview.IApprovedEmailContentResolver approvedContent)
        {
            _approvedContent = approvedContent;
            _lockService = lockService;
            _context = context;
            _currentUserService = currentUserService;
            _clock = clock;
            _dispatcher = dispatcher;
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
            if (user == null) throw new ForbiddenException("Không xác định được người dùng hiện tại.");

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);
            if (l == null)
                throw new NotFoundException(
                    "Không tìm thấy yêu cầu hậu cần.", LogisticsTaskErrorCodes.RequestNotFound);

            // Check department scope
            if (l.RequestedToDepartmentId != user.DepartmentId)
                throw new AuthBusinessException(
                    LogisticsTaskErrorCodes.AssignmentOutOfDepartmentScope,
                    "Không có quyền phân công đơn yêu cầu của phòng ban khác.");

            // Block assignment in terminal/in-flight statuses.
            var blockedStatuses = new[] { "ASSIGNED", "ACCEPTED", "CHANGE_PROPOSED", "IN_PROGRESS", "DONE", "CANCELLED", "REJECTED", "DECLINED" };
            if (blockedStatuses.Contains(l.Status))
                throw new ConflictException(
                    "Không thể phân công khi nhiệm vụ đang ở trạng thái: " + l.Status,
                    LogisticsTaskErrorCodes.AssignmentStatusNotAssignable);

            bool hasPendingAttempt = await _context.VisitLogisticsAssignmentAttempts
                .AnyAsync(a => a.LogisticsItemId == request.LogisticsItemId && a.Status == "PENDING", cancellationToken);
            if (hasPendingAttempt)
                throw new ConflictException(
                    "Nhiệm vụ đã được phân công và đang chờ phản hồi hoặc đã được nhận.",
                    LogisticsTaskErrorCodes.AssignmentAlreadyPending);

            bool hasSigned = await _context.VisitLogisticsItemHandovers
                .AnyAsync(h => h.LogisticsItemId == request.LogisticsItemId &&
                               (h.BorrowerSignedAt != null || h.ProviderSignedAt != null), cancellationToken);
            if (hasSigned)
                throw new ConflictException(
                    "Nhiệm vụ đã được xử lý hoặc đã có ký biên bản, không thể đổi người phụ trách.",
                    LogisticsTaskErrorCodes.AssignmentHandoverSigned);

            var assignee = await _context.Users.FirstOrDefaultAsync(
                u => u.UserId == request.AssigneeUserId
                     && u.DepartmentId == user.DepartmentId
                     && u.Status == "ACTIVE",
                cancellationToken);
            if (assignee == null)
                throw new ConflictException(
                    "Người phụ trách không hợp lệ hoặc không thuộc phòng ban.",
                    LogisticsTaskErrorCodes.AssigneeNotEligible);

            var campus = await _context.VisitRequestCampuses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.VisitInstanceId == l.VisitInstanceId, cancellationToken);
            // Đơn yêu cầu không bị chặn bởi các đơn yêu cầu hoặc thư mời khác trùng thời gian

            // The scope names the logistics item and the assignee, so an approval prepared for one staff
            // member cannot be replayed to hand the same wording to another.
            var scopeKey = PEMS.Application.Emails.Preview.EmailPreviewFingerprint.Scope(
                ("logisticsItem", l.LogisticsItemId),
                ("assignee", request.AssigneeUserId));

            var content = await _approvedContent.ResolveAsync(
                request.ApprovedContent,
                SystemEmailTemplates.LogisticsAssigneeAssignment,
                scopeKey,
                cancellationToken);

            var attachInputs = _approvedContent.AttachmentsOf(request.ApprovedContent);
            await OutboundEmailAttachments.ValidateAsync(_context, userId, attachInputs, cancellationToken);
            var now = _clock.VietnamNow;
            // Mixed per-campus v2: the email uses THIS instance's detail name.
            var delegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_context, new[] { l.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(l.VisitInstanceId) ?? "FPT University";
            // The assignee is told WHERE the visit is, which the previous hard-coded body never said.
            var campusName = campus is null ? "FPT University" : await _context.Campuses
                .Where(c => c.CampusId == campus.CampusId).Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "FPT University";

            PreparedSystemEmail prepared;

            await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
            {
                // Shared lock protocol (see IUserMutationLockService): an assignment hands this
                // account a live logistics responsibility, so it must serialize against a role change
                // on the same account. The eligibility read above was unlocked — re-read the
                // department/status under the lock before writing.
                await _lockService.LockUsersAsync(new[] { request.AssigneeUserId }, cancellationToken);

                var stillEligible = await _context.Users.AsNoTracking().AnyAsync(
                    u => u.UserId == request.AssigneeUserId
                         && u.DepartmentId == user.DepartmentId
                         && u.Status == "ACTIVE",
                    cancellationToken);
                if (!stillEligible)
                    throw new ConflictException(
                        "Vai trò hoặc phòng ban của nhân sự này vừa thay đổi. Vui lòng tải lại và phân công lại.");

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

                prepared = await _dispatcher.PrepareAsync(
                    new SystemEmailRequest(
                        SystemEmailTemplates.LogisticsAssigneeAssignment,
                        new EmailRecipient(assignee.Email, assignee.FullName),
                        new System.Collections.Generic.Dictionary<string, string>
                        {
                            ["assigneeName"] = assignee.FullName,
                            ["logisticsTitle"] = l.Title,
                            ["dueAt"] = l.DueAt?.ToString("HH:mm dd/MM/yyyy") ?? "Chưa đặt hạn",
                            ["campusName"] = campusName,
                            ["delegationName"] = delegationName,
                        },
                        TrustedBlocks: new System.Collections.Generic.Dictionary<string, string>
                        {
                            [EmailTrustedBlocks.ActionBlock] =
                                EmailComposition.LogisticsAssigneeActionBlock(acceptUrl, declineUrl, detailUrl),
                        },
                        RelatedType: EmailActionTargetTypes.LogisticsItem,
                        RelatedId: l.LogisticsItemId,
                        SentBy: userId)
                    {
                        Content = content,
                    },
                    cancellationToken);

                AttachTo(prepared.SentEmailId, attachInputs, now);

                _context.EmailActionTokens.Add(NewToken(_tokens.Hash(acceptRaw), EmailIntendedActions.Accept, groupKey, l.LogisticsItemId, assignee.UserId, assignee.Email, prepared.SentEmailId, prepared.SentEmailRecipientId, now));
                _context.EmailActionTokens.Add(NewToken(_tokens.Hash(declineRaw), EmailIntendedActions.Decline, groupKey, l.LogisticsItemId, assignee.UserId, assignee.Email, prepared.SentEmailId, prepared.SentEmailRecipientId, now));

                await _notificationService.CreateAsync(
                    new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: assignee.UserId,
                        Title: "Bạn được phân công hậu cần",
                        Message: $"Bạn được phân công xử lý hạng mục \"{l.Title}\" cho đoàn {delegationName}.",
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

                await transaction.CommitAsync(cancellationToken);
            }

            try
            {
                // Real MIME path with the leader's inline images (cid) + file attachments.
                var outboundAttachments = await OutboundEmailAttachments.LoadAsync(_context, _storage, attachInputs, cancellationToken);
                await _dispatcher.DeliverAsync(
                    prepared with { Attachments = outboundAttachments }, cancellationToken);
            }
            catch (Exception)
            {
                // The exception text is not recorded: a storage failure can name a path or a signed URL.
                await MarkDeliveryFailedAsync(
                    prepared, "Không đọc được tệp đính kèm khi gửi email.", now, cancellationToken);
            }

            return true;
        }


        /// <summary>Adds sent_email_attachments rows for a message the dispatcher has already written.</summary>
        private void AttachTo(ulong sentEmailId, System.Collections.Generic.IReadOnlyList<EmailComposeAttachmentInput> inputs, DateTime now)
        {
            var order = 0;
            foreach (var a in inputs)
            {
                _context.SentEmailAttachments.Add(new SentEmailAttachment
                {
                    SentEmailId = sentEmailId,
                    FileId = a.FileId,
                    AttachmentType = EmailComposeWriter.ParseAttachmentType(a.AttachmentType),
                    ContentId = string.IsNullOrWhiteSpace(a.ContentId) ? null : a.ContentId!.Trim(),
                    DisplayName = a.DisplayName,
                    DisplayOrder = a.DisplayOrder > 0 ? (uint)a.DisplayOrder : (uint)order,
                    CreatedAt = now,
                });
                order++;
            }
        }

        /// <summary>Records a failure that happened before the sender was reached at all.</summary>
        private async Task MarkDeliveryFailedAsync(
            PreparedSystemEmail prepared, string safeMessage, DateTime now, CancellationToken ct)
        {
            var sentEmail = await _context.SentEmails
                .FirstOrDefaultAsync(e => e.SentEmailId == prepared.SentEmailId, ct);
            if (sentEmail is not null)
            {
                sentEmail.Status = "FAILED";
                sentEmail.LastAttemptAt = now;
                sentEmail.ErrorMessage = safeMessage;
            }

            var rec = await _context.SentEmailRecipients
                .FirstOrDefaultAsync(r => r.SentEmailRecipientId == prepared.SentEmailRecipientId, ct);
            if (rec is not null)
            {
                rec.DeliveryStatus = "FAILED";
                rec.ErrorMessage = safeMessage;
            }

            await _context.SaveChangesAsync(ct);
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

    }
}
