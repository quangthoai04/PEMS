using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentReceptionTasks.Common;
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
        /// <summary>Proposed quantity (optional). When set, must be >= 0 (0 = department cannot fulfill
        /// any of the requested amount). Stored on proposed_quantity — the original quantity (PLANNED
        /// figure) is never overwritten.</summary>
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

        /// <summary>
        /// Shown for a proposal field the department left alone. The template renders every row, so an
        /// omitted field has to say so — a blank cell reads as "they propose nothing here", which is the
        /// opposite of "they propose no change here".
        /// </summary>
        private const string Unchanged = "Không đổi";

        private static string FormatMoment(DateTime? moment)
            => moment?.ToString("HH:mm dd/MM/yyyy") ?? Unchanged;

        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ISystemEmailDispatcher _dispatcher;
        private readonly IEmailActionTokenService _tokens;
        private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
        private readonly IUserMutationLockService _lockService;

        public ProposeRequestChangeCommandHandler(
            IApplicationDbContext context, ICurrentUserService currentUserService,
            ISystemEmailDispatcher dispatcher, IEmailActionTokenService tokens,
            PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
            PEMS.Application.Notifications.Common.INotificationService notificationService,
            IUserMutationLockService lockService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _dispatcher = dispatcher;
            _tokens = tokens;
            _normalizer = normalizer;
            _notificationService = notificationService;
            _lockService = lockService;
        }

        public async Task<bool> Handle(ProposeRequestChangeCommand request, CancellationToken cancellationToken)
        {
            // proposal_note is the mandatory rationale; proposed quantity/time/description are optional.
            var note = (request.ProposalNote ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(note)) note = (request.ProposedDescription ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(note))
                throw new ValidationException(
                    "Vui lòng nhập lý do/ghi chú đề xuất.",
                    LogisticsTaskErrorCodes.ProposalNoteRequired);
            if (request.ProposedQuantity is { } pq && pq < 1)
                throw new ValidationException(
                    "Số lượng đề xuất phải là số nguyên ≥ 1.",
                    LogisticsTaskErrorCodes.ProposalQuantityInvalid);

            var visitInstanceId = await _context.VisitLogisticsItems
                .Where(x => x.LogisticsItemId == request.LogisticsItemId)
                .Select(x => (ulong?)x.VisitInstanceId)
                .FirstOrDefaultAsync(cancellationToken);
            if (visitInstanceId is null)
                throw new NotFoundException(
                    "Không tìm thấy yêu cầu hậu cần.", LogisticsTaskErrorCodes.RequestNotFound);
            var visitRequestId = await _context.VisitRequestCampuses
                .Where(c => c.VisitInstanceId == visitInstanceId)
                .Select(c => (ulong?)c.VisitRequestId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến tiếp khách.");

            // Lock hierarchy (see IUserMutationLockService) — this handler previously took no lock and
            // wrapped only the email-sending half in a transaction, leaving the item mutation itself
            // uncovered.
            await using var transaction = await _context.BeginSerializedTransactionAsync(cancellationToken);
            await _lockService.LockVisitRequestsAsync(new[] { visitRequestId }, cancellationToken);
            await _lockService.LockVisitRequestCampusesAsync(new[] { visitInstanceId.Value }, cancellationToken);
            await _lockService.LockVisitLogisticsItemsAsync(new[] { request.LogisticsItemId }, cancellationToken);

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null)
                throw new NotFoundException(
                    "Không tìm thấy yêu cầu hậu cần.", LogisticsTaskErrorCodes.RequestNotFound);

            // Phòng ban chỉ được đề xuất số lượng THẤP HƠN số lượng dự kiến mượn của Host (đàm phán
            // giảm khi không đáp ứng đủ) — không được đề xuất tăng số lượng.
            if (request.ProposedQuantity is { } pqCheck && l.Quantity.HasValue && pqCheck >= l.Quantity.Value)
                throw new ConflictException(
                    $"Số lượng đề xuất phải nhỏ hơn số lượng dự kiến ({l.Quantity.Value}).",
                    LogisticsTaskErrorCodes.ProposalQuantityInvalid);

            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || l.RequestedToDepartmentId != user.DepartmentId)
                throw new AuthBusinessException(
                    LogisticsTaskErrorCodes.ProposalOutOfDepartmentScope,
                    "Không có quyền đề xuất thay đổi đơn yêu cầu của phòng ban khác.");

            var isDepartmentStaff = string.Equals(_currentUserService.RoleCode, RoleCodes.Department, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_currentUserService.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase);
            if (isDepartmentStaff && l.AssignedToUserId != userId)
                throw new AuthBusinessException(
                    LogisticsTaskErrorCodes.ProposalNotAssignedToProposer,
                    "Bạn chỉ có thể đề xuất thay đổi đơn yêu cầu được giao cho mình.");

            DateTime? ps = null, pe = null;
            if (!string.IsNullOrEmpty(request.ProposedUsageStartAt) && DateTime.TryParse(request.ProposedUsageStartAt, out var s))
                ps = DateTime.SpecifyKind(s, DateTimeKind.Unspecified);
            if (!string.IsNullOrEmpty(request.ProposedUsageEndAt) && DateTime.TryParse(request.ProposedUsageEndAt, out var e))
                pe = DateTime.SpecifyKind(e, DateTimeKind.Unspecified);
            if (ps.HasValue && pe.HasValue && pe.Value <= ps.Value)
                throw new ValidationException(
                    "Thời gian kết thúc đề xuất phải sau thời gian bắt đầu.",
                    LogisticsTaskErrorCodes.ProposalWindowInvalid);

            var now = VietnamTime.Now();

            // "Ai đề xuất, người đó phụ trách": nếu đơn chưa có ai phụ trách (thường là Trưởng phòng
            // đề xuất thẳng trước khi phân công cho Staff), người gửi đề xuất tự động trở thành người
            // phụ trách — tránh tình trạng đơn "đang xử lý" mà không ai đứng tên. Không cướp việc khỏi
            // Staff đã được giao trước đó (chỉ gán khi đang trống).
            if (l.AssignedToUserId == null)
            {
                l.AssignedToUserId = userId;
                l.AssignedBy = userId;
                l.AssignedAt = now;
            }

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

            PreparedSystemEmail? prepared = await PrepareProposalEmailAsync(l, userId, now, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Best-effort, same as every other system email here: the proposal is already committed,
            // and the Host can still respond from the Portal, so a delivery failure is recorded rather
            // than thrown.
            if (prepared is not null)
                await _dispatcher.DeliverAsync(prepared, cancellationToken);
            return true;
        }

        /// <summary>
        /// Records (but does not send) the Host-facing proposal notice. Per the business rule (spec
        /// BUG-07), a proposal decision is Portal-only: this email carries NO public Approve/Reject
        /// token — only a login-required "Xem chi tiết trong hệ thống" link — so there is nothing here
        /// for a mail scanner or a forwarded email to act on. Returns null (nothing to send) when there
        /// is no Host on record, matching the previous portal-only-proposal behavior.
        /// </summary>
        private async Task<PreparedSystemEmail?> PrepareProposalEmailAsync(
            Domain.Entities.Delegations.VisitLogisticsItem l, ulong proposerUserId, DateTime now, CancellationToken cancellationToken)
        {
            if (!l.RequestedBy.HasValue) return null; // no Host on record → portal-only proposal
            var host = await _context.Users.FirstOrDefaultAsync(u => u.UserId == l.RequestedBy.Value, cancellationToken);
            if (host == null || string.IsNullOrWhiteSpace(host.Email)) return null;

            // Mixed per-campus v2: the in-app notification names THIS instance's delegation.
            var delegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_context, new[] { l.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(l.VisitInstanceId) ?? "FPT University";

            // The Host is told WHICH department is asking. Without it the mail says only "the handling
            // department proposes a change", which the Host cannot act on or verify.
            var departmentName = l.RequestedToDepartmentId is null ? "Phòng ban xử lý" : await _context.Departments
                .Where(d => d.DepartmentId == l.RequestedToDepartmentId.Value).Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "Phòng ban xử lý";

            var detailUrl = _tokens.BuildHostVisitProcessUrl(l.VisitInstanceId);

            var prepared = await _dispatcher.PrepareAsync(
                new SystemEmailRequest(
                    SystemEmailTemplates.LogisticsChangeProposalToHost,
                    new EmailRecipient(host.Email, host.FullName),
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["hostName"] = host.FullName,
                        ["logisticsTitle"] = l.Title,
                        ["departmentName"] = departmentName,
                        ["delegationName"] = delegationName,
                        // A proposal is a counter-offer, so the mail states WHAT is being proposed,
                        // not just why. Sending the rationale alone forced the Host into the portal
                        // to discover the numbers they were being asked to approve.
                        ["originalQuantity"] = l.Quantity?.ToString() ?? Unchanged,
                        ["proposedQuantity"] = l.ProposedQuantity?.ToString() ?? Unchanged,
                        ["proposedUsageStartAt"] = FormatMoment(l.ProposedUsageStartAt),
                        ["proposedUsageEndAt"] = FormatMoment(l.ProposedUsageEndAt),
                        ["proposedDescription"] = string.IsNullOrWhiteSpace(l.ProposedDescription)
                            ? Unchanged
                            : l.ProposedDescription!,
                        // Guaranteed non-empty by the caller: the rationale is mandatory, and falls
                        // back to the proposed description before this point.
                        ["proposalNote"] = l.ProposalNote ?? string.Empty,
                    },
                    TrustedBlocks: new System.Collections.Generic.Dictionary<string, string>
                    {
                        [EmailTrustedBlocks.ActionBlock] = EmailComposition.DetailLinkBlock(
                            detailUrl, "Xem chi tiết trong hệ thống"),
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

            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: host.UserId,
                    Title: "Phòng ban đề xuất thay đổi hậu cần",
                    Message: $"Phòng ban đề xuất thay đổi cho yêu cầu \"{l.Title}\" của đoàn {delegationName}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsProposalCreated,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: l.LogisticsItemId,
                    ActorUserId: proposerUserId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    IsActionRequired: true,
                    VisitInstanceId: l.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                    ActionUrl: $"/dashboard/visit/process/{l.VisitInstanceId}",
                    MetadataJson: PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                        PEMS.Application.Notifications.Common.NotificationEventKeys.LogisticsProposalCreated,
                        new { delegationName, departmentName })),
                cancellationToken
            );

            return prepared;
        }
    }
}
