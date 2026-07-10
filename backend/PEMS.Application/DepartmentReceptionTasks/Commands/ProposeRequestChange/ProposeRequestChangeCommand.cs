using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Notifications;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.ProposeRequestChange
{
    public class ProposeRequestChangeCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
        /// <summary>Proposed quantity (optional). When set, must be >= 1. Stored on proposed_quantity —
        /// the original quantity (PLANNED figure) is never overwritten.</summary>
        public int? ProposedQuantity { get; set; }
        public string? ProposedUsageStartAt { get; set; } // YYYY-MM-DDTHH:mm:ss
        public string? ProposedUsageEndAt { get; set; } // YYYY-MM-DDTHH:mm:ss
        public string? ProposedDescription { get; set; }
        /// <summary>Reason/note for the proposal — REQUIRED.</summary>
        public string? ProposalNote { get; set; }
    }

    public class ProposeRequestChangeCommandHandler : IRequestHandler<ProposeRequestChangeCommand, bool>
    {
        private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(14);

        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmailService _email;
        private readonly IEmailActionTokenService _tokens;
        private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

        public ProposeRequestChangeCommandHandler(
            IApplicationDbContext context, ICurrentUserService currentUserService,
            IEmailService email, IEmailActionTokenService tokens,
            PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
            PEMS.Application.Notifications.Common.INotificationService notificationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _email = email;
            _tokens = tokens;
            _normalizer = normalizer;
            _notificationService = notificationService;
        }

        public async Task<bool> Handle(ProposeRequestChangeCommand request, CancellationToken cancellationToken)
        {
            // proposal_note is the mandatory rationale; proposed quantity/time/description are optional.
            var note = (request.ProposalNote ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(note)) note = (request.ProposedDescription ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(note)) throw new Exception("Vui lòng nhập lý do/ghi chú đề xuất.");
            if (request.ProposedQuantity is { } pq && pq < 1) throw new Exception("Số lượng đề xuất phải là số nguyên ≥ 1.");

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new Exception("Không tìm thấy đơn yêu cầu");

            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || l.RequestedToDepartmentId != user.DepartmentId)
                throw new Exception("Không có quyền đề xuất thay đổi đơn yêu cầu của phòng ban khác");

            var isDepartmentStaff = string.Equals(_currentUserService.RoleCode, RoleCodes.Department, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_currentUserService.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase);
            if (isDepartmentStaff && l.AssignedToUserId != userId)
                throw new Exception("Ban chi co the de xuat thay doi don yeu cau duoc giao cho minh.");

            DateTime? ps = null, pe = null;
            if (!string.IsNullOrEmpty(request.ProposedUsageStartAt) && DateTime.TryParse(request.ProposedUsageStartAt, out var s))
                ps = DateTime.SpecifyKind(s, DateTimeKind.Unspecified);
            if (!string.IsNullOrEmpty(request.ProposedUsageEndAt) && DateTime.TryParse(request.ProposedUsageEndAt, out var e))
                pe = DateTime.SpecifyKind(e, DateTimeKind.Unspecified);
            if (ps.HasValue && pe.HasValue && pe.Value <= ps.Value)
                throw new Exception("Thời gian kết thúc đề xuất phải sau thời gian bắt đầu.");

            var now = DateTime.UtcNow;

            // Never overwrite the original quantity (the PLANNED figure) — only the proposed_* columns.
            l.ProposedQuantity = request.ProposedQuantity;
            l.ProposedDescription = string.IsNullOrWhiteSpace(request.ProposedDescription) ? null : request.ProposedDescription.Trim();
            l.ProposalNote = note;
            l.ProposedUsageStartAt = ps;
            l.ProposedUsageEndAt = pe;

            l.Status = "CHANGE_PROPOSED";
            l.ProposedBy = userId;
            l.ProposedAt = now;
            l.ProposalResponse = null;
            l.ProposalRespondedBy = null;
            l.ProposalRespondedAt = null;
            l.ProposalResponseNote = null;
            l.UpdatedBy = userId;
            l.UpdatedAt = now;

            // Invalidate any older pending tokens on this item (e.g. a prior request accept/decline) so a
            // stale email can't act on the item while it now awaits the Host's proposal decision.
            await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _context, EmailActionTargetTypes.LogisticsItem, l.LogisticsItemId,
                "Yêu cầu đang chờ Host phản hồi đề xuất thay đổi.", now, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            // Email the Host (the requester) with APPROVE/REJECT proposal buttons. The proposal is already
            // persisted; an SMTP failure never rolls it back (the Host can still respond from the portal).
            await SendProposalEmailAsync(l, userId, now, cancellationToken);
            return true;
        }

        private async Task SendProposalEmailAsync(
            Domain.Entities.Delegations.VisitLogisticsItem l, ulong proposerUserId, DateTime now, CancellationToken cancellationToken)
        {
            if (!l.RequestedBy.HasValue) return; // no Host on record → portal-only proposal
            var host = await _context.Users.FirstOrDefaultAsync(u => u.UserId == l.RequestedBy.Value, cancellationToken);
            if (host == null || string.IsNullOrWhiteSpace(host.Email)) return;

            var delegationName = await (
                from c in _context.VisitRequestCampuses
                join v in _context.VisitRequests on c.VisitRequestId equals v.VisitRequestId
                where c.VisitInstanceId == l.VisitInstanceId
                select v.DelegationName).FirstOrDefaultAsync(cancellationToken) ?? "FPT University";

            ulong sentEmailId, sentEmailRecipientId;
            string finalSubject, finalBody;

            await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
            {
                var approveRaw = _tokens.GenerateRawToken();
                var rejectRaw = _tokens.GenerateRawToken();
                var groupKey = Guid.NewGuid().ToString("N");
                var approveUrl = _tokens.BuildPublicActionUrl(approveRaw);
                var rejectUrl = _tokens.BuildPublicActionUrl(rejectRaw);
                var detailUrl = _tokens.BuildLogisticsDetailUrl(l.LogisticsItemId);

                finalSubject = LogisticsPriorityText.ApplySubjectPrefix(l.Priority, $"[PEMS] Phòng ban đề xuất thay đổi hậu cần — {l.Title}");
                finalBody = EmailComposition.BrandedShell(
                    ProposalContentHtml(host.FullName, delegationName, l)
                    + EmailComposition.LogisticsProposalActionBlock(approveUrl, rejectUrl, detailUrl));
                finalBody = await _normalizer.NormalizeHtmlAsync(finalBody, cancellationToken);

                var sentEmail = new SentEmail
                {
                    EmailTemplateId = null,
                    RelatedType = EmailActionTargetTypes.LogisticsItem,
                    RelatedId = l.LogisticsItemId,
                    Subject = finalSubject,
                    BodySnapshot = finalBody,
                    Status = "QUEUED",
                    SentBy = proposerUserId,
                    CreatedAt = now,
                };
                _context.SentEmails.Add(sentEmail);
                await _context.SaveChangesAsync(cancellationToken);

                var sentRecipient = new SentEmailRecipient
                {
                    SentEmailId = sentEmail.SentEmailId,
                    RecipientEmail = host.Email,
                    RecipientName = host.FullName,
                    RecipientType = "TO",
                    DeliveryStatus = "QUEUED",
                    CreatedAt = now,
                };
                _context.SentEmailRecipients.Add(sentRecipient);
                await _context.SaveChangesAsync(cancellationToken);

                _context.EmailActionTokens.Add(NewProposalToken(_tokens.Hash(approveRaw), EmailIntendedActions.ApproveProposal, groupKey, l.LogisticsItemId, host.UserId, host.Email, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));
                _context.EmailActionTokens.Add(NewProposalToken(_tokens.Hash(rejectRaw), EmailIntendedActions.RejectProposal, groupKey, l.LogisticsItemId, host.UserId, host.Email, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));

                await _notificationService.CreateAsync(
                    new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: host.UserId,
                        Title: LogisticsPriorityText.SubjectPrefix(l.Priority) + "Phòng ban đề xuất thay đổi hậu cần",
                        Message: $"Phòng ban đề xuất thay đổi cho yêu cầu \"{l.Title}\" (ưu tiên {LogisticsPriorityText.LabelVi(l.Priority)}) của đoàn {delegationName}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsProposalCreated,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                        RelatedId: l.LogisticsItemId,
                        ActorUserId: proposerUserId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                        IsActionRequired: true,
                        VisitInstanceId: l.VisitInstanceId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                        ActionUrl: $"/dashboard/visit/process/{l.VisitInstanceId}"),
                    cancellationToken
                );
                await _context.SaveChangesAsync(cancellationToken);

                sentEmailId = sentEmail.SentEmailId;
                sentEmailRecipientId = sentRecipient.SentEmailRecipientId;
                await transaction.CommitAsync(cancellationToken);
            }

            try
            {
                await _email.SendAsync(new OutboundEmail
                {
                    ToEmail = host.Email,
                    Subject = finalSubject,
                    Body = finalBody,
                    IsHtml = true,
                }, cancellationToken);
                await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "SENT", proposerUserId, now, null, cancellationToken);
            }
            catch (Exception ex)
            {
                await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "FAILED", proposerUserId, now, ex.Message, cancellationToken);
            }
        }

        private static string ProposalContentHtml(string hostName, string delegationName, Domain.Entities.Delegations.VisitLogisticsItem l)
        {
            string HE(string? s) => EmailComposition.HE(s);
            string Fmt(DateTime? d) => d.HasValue ? HE(d.Value.ToString("HH:mm dd/MM/yyyy")) : "—";
            var rows = $"<li><strong>Mức ưu tiên:</strong> {HE(LogisticsPriorityText.LabelVi(l.Priority))}</li>";
            if (l.DueAt.HasValue) rows += $"<li><strong>Hạn phản hồi:</strong> {Fmt(l.DueAt)}</li>";
            if (l.ProposedQuantity.HasValue)
                rows += $"<li><strong>Số lượng đề xuất:</strong> {l.ProposedQuantity} (dự kiến: {(l.Quantity?.ToString() ?? "—")})</li>";
            if (l.ProposedUsageStartAt.HasValue || l.ProposedUsageEndAt.HasValue)
                rows += $"<li><strong>Thời gian đề xuất:</strong> {Fmt(l.ProposedUsageStartAt)} – {Fmt(l.ProposedUsageEndAt)}</li>";
            if (!string.IsNullOrWhiteSpace(l.ProposedDescription))
                rows += $"<li><strong>Nội dung đề xuất:</strong> {HE(l.ProposedDescription)}</li>";
            if (!string.IsNullOrWhiteSpace(l.ProposalNote))
                rows += $"<li><strong>Lý do:</strong> {HE(l.ProposalNote)}</li>";

            return $@"<p>Xin chào <strong>{HE(hostName)}</strong>,</p>
<p>Phòng ban xử lý đã đề xuất thay đổi cho hạng mục hậu cần <strong>{HE(l.Title)}</strong> của đoàn <strong>{HE(delegationName)}</strong>.</p>
<div style=""background:#f5f3ff;border-left:4px solid #7c3aed;border-radius:8px;padding:16px 20px;margin:20px 0"">
  <ul style=""margin:0;padding-left:20px;line-height:1.7"">
    {rows}
  </ul>
</div>
<p>Vui lòng chấp nhận hoặc từ chối đề xuất bằng một trong các nút dưới đây.</p>";
        }

        private static EmailActionToken NewProposalToken(
            string tokenHash, string intendedAction, string groupKey, ulong logisticsItemId,
            ulong recipientUserId, string recipientEmail, ulong sentEmailId, ulong sentEmailRecipientId, DateTime now)
            => new()
            {
                TokenHash = tokenHash,
                ActionGroupKey = groupKey,
                ActionContext = EmailActionContexts.LogisticsProposalResponse,
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
