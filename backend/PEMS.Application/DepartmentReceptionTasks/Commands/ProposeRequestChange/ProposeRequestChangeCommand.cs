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

using PEMS.Application.Common;
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
        private readonly ISystemEmailDispatcher _dispatcher;
        private readonly IEmailActionTokenService _tokens;
        private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

        public ProposeRequestChangeCommandHandler(
            IApplicationDbContext context, ICurrentUserService currentUserService,
            ISystemEmailDispatcher dispatcher, IEmailActionTokenService tokens,
            PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
            PEMS.Application.Notifications.Common.INotificationService notificationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _dispatcher = dispatcher;
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

            var now = VietnamTime.Now();

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

            // Mixed per-campus v2: the in-app notification names THIS instance's delegation.
            var delegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_context, new[] { l.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(l.VisitInstanceId) ?? "FPT University";

            // The Host is told WHICH department is asking. Without it the mail says only "the handling
            // department proposes a change", which the Host cannot act on or verify.
            var departmentName = l.RequestedToDepartmentId is null ? "Phòng ban xử lý" : await _context.Departments
                .Where(d => d.DepartmentId == l.RequestedToDepartmentId.Value).Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "Phòng ban xử lý";

            PreparedSystemEmail prepared;

            await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
            {
                var approveRaw = _tokens.GenerateRawToken();
                var rejectRaw = _tokens.GenerateRawToken();
                var groupKey = Guid.NewGuid().ToString("N");
                var approveUrl = _tokens.BuildPublicActionUrl(approveRaw);
                var rejectUrl = _tokens.BuildPublicActionUrl(rejectRaw);
                var detailUrl = _tokens.BuildLogisticsDetailUrl(l.LogisticsItemId);

                prepared = await _dispatcher.PrepareAsync(
                    new SystemEmailRequest(
                        SystemEmailTemplates.LogisticsChangeProposalToHost,
                        new EmailRecipient(host.Email, host.FullName),
                        new System.Collections.Generic.Dictionary<string, string>
                        {
                            ["hostName"] = host.FullName,
                            ["logisticsTitle"] = l.Title,
                            ["departmentName"] = departmentName,
                            // Guaranteed non-empty by the caller: the rationale is mandatory, and falls
                            // back to the proposed description before this point.
                            ["proposalNote"] = l.ProposalNote ?? string.Empty,
                        },
                        TrustedBlocks: new System.Collections.Generic.Dictionary<string, string>
                        {
                            [EmailTrustedBlocks.ActionBlock] =
                                EmailComposition.LogisticsProposalActionBlock(approveUrl, rejectUrl, detailUrl),
                        },
                        RelatedType: EmailActionTargetTypes.LogisticsItem,
                        RelatedId: l.LogisticsItemId,
                        SentBy: proposerUserId)
                    {
                        // The department never edits this message — it is a system notice, and there is no
                        // screen offering to rewrite it.
                        Content = SystemEmailContent.FromTemplate.Instance,
                    },
                    cancellationToken);

                _context.EmailActionTokens.Add(NewProposalToken(_tokens.Hash(approveRaw), EmailIntendedActions.ApproveProposal, groupKey, l.LogisticsItemId, host.UserId, host.Email, prepared.SentEmailId, prepared.SentEmailRecipientId, now));
                _context.EmailActionTokens.Add(NewProposalToken(_tokens.Hash(rejectRaw), EmailIntendedActions.RejectProposal, groupKey, l.LogisticsItemId, host.UserId, host.Email, prepared.SentEmailId, prepared.SentEmailRecipientId, now));

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

                await transaction.CommitAsync(cancellationToken);
            }

            // Best-effort, exactly as before: the proposal is already persisted and the Host can still
            // respond from the portal, so a delivery failure is recorded rather than thrown.
            await _dispatcher.DeliverAsync(prepared, cancellationToken);
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

    }
}
