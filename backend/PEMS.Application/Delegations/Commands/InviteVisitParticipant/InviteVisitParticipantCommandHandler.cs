using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.InviteVisitParticipant;

/// <summary>
/// Host invites a supporting participant: inserts/refreshes the visit_participants row (status
/// INVITED), persists a sent_emails record + ACCEPT/DECLINE email_action_tokens (one-time, hashed),
/// then sends the invitation email best-effort. The participant is committed BEFORE the email is
/// sent, so a transient SMTP failure never loses the invitation (spec §21.5/§21.6).
///
/// <para>
/// Content comes from <c>email_templates</c> — <c>VISIT_PARTICIPANT_INVITATION</c>,
/// <c>VISIT_STUDENT_INVITATION</c> or <c>VISIT_DEPARTMENT_LEADER_INVITATION</c> depending on who is being
/// invited — unless the Host rewrote it, which is a named content mode rather than a bypass: authored
/// content is validated, sanitised, rendered and guarded by the same pipeline, and the accept/decline
/// buttons are still minted by the backend from the real tokens.
/// </para>
/// <para>
/// The message is RECORDED inside this handler's transaction and SENT after it commits. That split is
/// what lets <c>email_action_tokens.sent_email_id</c> (NOT NULL) be written atomically with the tokens it
/// belongs to, without handing anything to SMTP that a rollback would then erase.
/// </para>
/// </summary>
public sealed class InviteVisitParticipantCommandHandler
    : IRequestHandler<InviteVisitParticipantCommand, InviteVisitParticipantResponse>
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(14);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IEmailActionTokenService _tokens;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService _formReadService;
    private readonly IUserMutationLockService _lockService;

    public InviteVisitParticipantCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        ISystemEmailDispatcher dispatcher, IEmailActionTokenService tokens, IHtmlSanitizerService sanitizer,
        IFileStorageService storage, PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService formReadService,
        IUserMutationLockService lockService)
    {
        _lockService = lockService;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _dispatcher = dispatcher;
        _tokens = tokens;
        _sanitizer = sanitizer;
        _storage = storage;
        _normalizer = normalizer;
        _notificationService = notificationService;
        _formReadService = formReadService;
    }

    public async Task<InviteVisitParticipantResponse> Handle(
        InviteVisitParticipantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;
        var type = (request.ParticipantType ?? string.Empty).Trim().ToUpperInvariant();

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // ── Host + window guards (never trust the frontend) ──
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được mời thành phần tham gia.");
        if (instance.Status != VisitInstanceStatus.Assigned && instance.Status != VisitInstanceStatus.BeforeVisit)
            throw new ConflictException("Chỉ có thể mời thành phần tham gia trong giai đoạn chuẩn bị.");

        // ── Resolve the real invitee + participant role from the DB ──
        var (targetUserId, participantRole, roleLabel, recipientDeptId) =
            await ResolveInviteeAsync(type, request, instance, cancellationToken);

        // ── Duplicate handling (unique (visit_instance_id, user_id)) ──
        var existing = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == targetUserId,
                cancellationToken);
        if (existing != null
            && (existing.Status == ParticipantStatuses.Invited
                || existing.Status == ParticipantStatuses.Accepted
                || existing.Status == ParticipantStatuses.Assigned))
        {
            throw new ConflictException("Người này đã có trong danh sách tham gia của đoàn.");
        }

        var recipient = await _db.Users
            .Where(u => u.UserId == targetUserId)
            .Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("User", targetUserId);

        var hostName = await _db.Users
            .Where(u => u.UserId == actorId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Host";
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "FPT University";
        // The invitation is for THIS campus instance, so the delegation name comes from that instance's own
        // detail — never a sibling's, and never a request-level value, which does not exist.
        var formContent = await _formReadService.ResolveCampusFormContentAsync(
            instance.VisitRequest, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = formContent[instance.VisitInstanceId].DelegationName;

        var now = _clock.VietnamNow;

        // ── The optional host-edited email content + attachments (Part C / rich editor) ──
        // Validation and sanitising happen inside AuthoredByUser.Create — an unchecked instance of that
        // type cannot exist — so a rejected edit throws here, before anything is written or sent.
        var content = await ResolveContentAsync(request.EmailOverride, cancellationToken);
        var attachInputs = OutboundEmailAttachments.From(request.EmailOverride);
        await OutboundEmailAttachments.ValidateAsync(_db, actorId, attachInputs, cancellationToken);

        // Which template this message is. The invitee's role decides it; the Host never picks.
        var templateCode = participantRole == ParticipantRoles.DeptSupport
            ? SystemEmailTemplates.VisitDepartmentLeaderInvitation
            : participantRole == ParticipantRoles.Student
                ? SystemEmailTemplates.VisitStudentInvitation
                : SystemEmailTemplates.VisitParticipantInvitation;

        VisitParticipant participant;
        PreparedSystemEmail prepared;

        await using (var transaction = await _db.BeginTransactionAsync(cancellationToken))
        {
            // ── 0) Shared lock protocol (see IUserMutationLockService). The invitee's eligibility
            //    was resolved above without a lock; a role change could have committed in between,
            //    which would leave an INVITED row on an account that no longer has the permissions
            //    the invitation implies. Lock the account, then re-resolve against the committed
            //    state — a Staff Leader's role change either lost the race and now sees this
            //    invitation as a blocker, or won it and this invite is refused. ──
            await _lockService.LockUsersAsync(new[] { targetUserId }, cancellationToken);

            var reconfirmed = await ResolveInviteeAsync(type, request, instance, cancellationToken);
            if (reconfirmed.UserId != targetUserId || reconfirmed.ParticipantRole != participantRole)
                throw new ConflictException(
                    "Vai trò của nhân sự này vừa thay đổi. Vui lòng tải lại danh sách và mời lại.");

            // 1) participant row (reuse a previously DECLINED/REMOVED row, else create new).
            if (existing != null)
            {
                participant = existing;
                participant.ParticipantRole = participantRole;
                participant.IsHost = false;
                participant.Status = ParticipantStatuses.Invited;
                participant.InvitedBy = actorId;
                participant.InvitedAt = now;
                participant.RespondedAt = null;
                participant.AssignedBy = null;
                participant.AssignedAt = null;
                participant.UpdatedAt = now;
                participant.UpdatedBy = actorId;
            }
            else
            {
                participant = new VisitParticipant
                {
                    VisitInstanceId = instance.VisitInstanceId,
                    UserId = targetUserId,
                    ParticipantRole = participantRole,
                    IsHost = false,
                    Status = ParticipantStatuses.Invited,
                    InvitedBy = actorId,
                    InvitedAt = now,
                    Note = null,
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                _db.VisitParticipants.Add(participant);
            }
            await _db.SaveChangesAsync(cancellationToken);

            // 2) Mint the real one-time tokens FIRST so the action block can carry the live URLs. The
            //    block is built here, by the backend, in both content modes — a Host who rewrites the
            //    message never gets to write, move or omit the buttons that carry these tokens.
            var acceptRaw = _tokens.GenerateRawToken();
            var declineRaw = _tokens.GenerateRawToken();
            var groupKey = Guid.NewGuid().ToString("N");

            var acceptUrl = _tokens.BuildPublicActionUrl(acceptRaw);
            var declineUrl = _tokens.BuildPublicActionUrl(declineRaw);
            var assignUrl = participantRole == ParticipantRoles.DeptSupport
                ? _tokens.BuildDepartmentAssignmentUrl(instance.VisitInstanceId, participant.ParticipantId)
                : null;

            // 3) Render + record, inside this transaction. Nothing is sent yet: the tokens below must be
            //    written atomically with the sent_emails row they point at.
            prepared = await _dispatcher.PrepareAsync(
                new SystemEmailRequest(
                    templateCode,
                    new EmailRecipient(recipient.Email, recipient.FullName),
                    new Dictionary<string, string>
                    {
                        ["recipientName"] = recipient.FullName,
                        ["delegationName"] = delegationName,
                        ["campusName"] = campusName,
                        ["plannedTime"] = FormatWindow(instance.PlannedStartAt, instance.PlannedEndAt),
                        ["hostName"] = hostName,
                        ["roleLabel"] = roleLabel,
                        ["hostMessage"] = request.Message?.Trim() ?? string.Empty,
                    },
                    TrustedBlocks: new Dictionary<string, string>
                    {
                        [EmailTrustedBlocks.ActionBlock] =
                            EmailComposition.AcceptDeclineBlock(acceptUrl, declineUrl, assignUrl),
                    },
                    RelatedType: EmailActionTargetTypes.VisitParticipant,
                    RelatedId: participant.ParticipantId,
                    SentBy: actorId)
                {
                    Content = content,
                    // The invitation already names the Host; the scope is what lets the dispatcher print a
                    // way to reach them. Scoped to THIS instance, so a multi-campus request never hands an
                    // invitee another campus's Host.
                    ContactScope = new EmailContactScope(
                        VisitInstanceId: instance.VisitInstanceId, CampusId: instance.CampusId),
                    // …and whoever the Host named instead, for this invitation only. Re-checked against
                    // the database here; the preview that produced it granted nothing.
                    ContactOverride = request.EmailOverride?.ContactOverride,
                },
                cancellationToken);

            // Inline images + file attachments, linked to the row the dispatcher just wrote.
            AttachTo(prepared.SentEmailId, attachInputs, now);

            // 4) ACCEPT + DECLINE one-time tokens (share an action_group_key). Only the hash is stored.
            _db.EmailActionTokens.Add(NewToken(_tokens.Hash(acceptRaw), EmailIntendedActions.Accept, groupKey, participant.ParticipantId, targetUserId, recipient.Email, prepared.SentEmailId, prepared.SentEmailRecipientId, now));
            _db.EmailActionTokens.Add(NewToken(_tokens.Hash(declineRaw), EmailIntendedActions.Decline, groupKey, participant.ParticipantId, targetUserId, recipient.Email, prepared.SentEmailId, prepared.SentEmailRecipientId, now));

            // 5) audit + an in-app notification for the invitee.
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = instance.CampusId,
                Action = "INVITE_VISIT_PARTICIPANT",
                EntityType = "VisitParticipant",
                EntityId = participant.ParticipantId,
                CreatedAt = now,
            });
            var isDeptRecipient = participantRole == ParticipantRoles.DeptSupport;
            var invitationActionUrl = isDeptRecipient && recipientDeptId.HasValue
                ? $"/dashboard/departments/{recipientDeptId.Value}/invitations/{participant.ParticipantId}"
                : $"/dashboard/visit/process/{instance.VisitInstanceId}?tab=participants&participantId={participant.ParticipantId}";

            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: targetUserId,
                    Title: "Lời mời tham gia tiếp khách",
                    Message: $"Bạn được mời tham gia hỗ trợ đoàn {delegationName} tại {campusName}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.ParticipationInvited,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitParticipant,
                    RelatedId: participant.ParticipantId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Invitation,
                    IsActionRequired: true,
                    VisitRequestId: instance.VisitRequestId,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitInvitation,
                    ActionUrl: invitationActionUrl),
                cancellationToken
            );
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        // ── Send AFTER the participant + tokens are durably committed ──
        // Attachment bytes are streamed here rather than inside the transaction, so a slow or unavailable
        // file store cannot hold a database transaction open — and cannot lose an invitation that is
        // already valid. A failure at this point leaves the message FAILED and resendable.
        EmailDeliveryResult delivery;
        try
        {
            var outboundAttachments = await OutboundEmailAttachments.LoadAsync(
                _db, _storage, attachInputs, cancellationToken);

            delivery = await _dispatcher.DeliverAsync(
                prepared with { Attachments = outboundAttachments }, cancellationToken);
        }
        catch (Exception)
        {
            // The exception text is not recorded: an attachment failure can name a storage path or a
            // signed URL, and this message is shown in the email history.
            delivery = EmailDeliveryResult.Failed(
                "ATTACHMENT_LOAD_FAILED", "Không đọc được tệp đính kèm khi gửi email.");
            await MarkDeliveryFailedAsync(prepared, delivery.SafeMessage, cancellationToken);
        }

        var status = new SystemEmailDispatchResult(delivery, prepared.SentEmailId, prepared.EmailTemplateId)
            .NotificationStatus;
        var emailQueued = delivery.Status != EmailDeliveryStatus.Failed;

        var message = delivery.Status switch
        {
            EmailDeliveryStatus.Sent => "Đã gửi lời mời.",
            EmailDeliveryStatus.Skipped => "Đã tạo lời mời; email chưa được gửi (SMTP đang tắt).",
            _ => "Đã tạo lời mời nhưng gửi email thất bại.",
        };

        return new InviteVisitParticipantResponse(
            participant.ParticipantId, targetUserId, participantRole, participant.Status,
            emailQueued, recipient.Email, message, status, prepared.SentEmailId);
    }

    /// <summary>
    /// Turns the optional host edit into a content mode. No override, or an override the Host left
    /// switched off, means the template is the content — which is the normal case and the only case
    /// before this screen let anyone edit anything.
    /// </summary>
    private async Task<SystemEmailContent> ResolveContentAsync(EmailOverride? ov, CancellationToken ct)
    {
        if (ov is null || !ov.UseEditedContent) return SystemEmailContent.FromTemplate.Instance;

        // Prefer the host-edited plain text (bodyText) → safe HTML; fall back to legacy bodyHtml. Inline
        // images are normalised BEFORE the content is fixed, because the normaliser exists for images the
        // Host pasted — the template body has none.
        var rawHtml = await _normalizer.NormalizeHtmlAsync(EmailComposition.ResolveEditableHtml(ov), ct);

        return SystemEmailContent.AuthoredByUser.Create(ov.Subject, rawHtml, _sanitizer);
    }

    /// <summary>Adds sent_email_attachments rows for a message the dispatcher has already written.</summary>
    private void AttachTo(ulong sentEmailId, IReadOnlyList<EmailComposeAttachmentInput> inputs, DateTime now)
    {
        var order = 0;
        foreach (var a in inputs)
        {
            _db.SentEmailAttachments.Add(new SentEmailAttachment
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

    /// <summary>
    /// Records a failure that happened before the sender was reached at all — loading an attachment, for
    /// instance. The dispatcher writes the outcome for everything it handles itself; this covers the gap
    /// between "the message is recorded" and "the message reached the sender".
    /// </summary>
    private async Task MarkDeliveryFailedAsync(PreparedSystemEmail prepared, string? error, CancellationToken ct)
    {
        var now = _clock.VietnamNow;

        var sentEmail = await _db.SentEmails
            .FirstOrDefaultAsync(e => e.SentEmailId == prepared.SentEmailId, ct);
        if (sentEmail is not null)
        {
            sentEmail.Status = "FAILED";
            sentEmail.LastAttemptAt = now;
            sentEmail.ErrorMessage = Truncate(error, 1000);
        }

        var rec = await _db.SentEmailRecipients
            .FirstOrDefaultAsync(r => r.SentEmailRecipientId == prepared.SentEmailRecipientId, ct);
        if (rec is not null)
        {
            rec.DeliveryStatus = "FAILED";
            rec.ErrorMessage = Truncate(error, 1000);
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ──

    private async Task<(ulong UserId, string ParticipantRole, string RoleLabel, ulong? DepartmentId)> ResolveInviteeAsync(
        string type, InviteVisitParticipantCommand request, VisitRequestCampus instance, CancellationToken ct)
    {
        var campusId = instance.CampusId;

        if (type == InviteParticipantTypes.IcSupport)
        {
            if (request.UserId is null)
                throw new ValidationException("Thiếu thông tin nhân sự cần mời.");
            // Both IC Staff (STAFF+STAFF) and Staff Leader (STAFF+LEADER) of an ACTIVE IC
            // department on this campus are valid IC_SUPPORT invitees — same participant role,
            // never the current host. Re-validated here so a direct API call can't bypass the
            // candidate query.
            var invitee = await (
                from u in _db.Users
                join r in _db.Roles on u.RoleId equals r.RoleId
                join d in _db.Departments on u.DepartmentId equals d.DepartmentId
                where u.UserId == request.UserId.Value
                      && r.RoleCode == RoleCodes.Staff
                      && (u.SubRole == UserSubRoles.Staff || u.SubRole == UserSubRoles.Leader)
                      && u.Status == UserStatuses.Active
                      && u.PrimaryCampusId == campusId
                      && d.DepartmentType == "IC"
                      && d.Status == EntityStatuses.Active
                      && u.UserId != instance.CurrentHostUserId
                select new { u.UserId, u.SubRole }).FirstOrDefaultAsync(ct);
            if (invitee is null)
                throw new ConflictException("Nhân sự được chọn không hợp lệ (không phải Staff hoặc Staff Leader IC đang hoạt động cùng cơ sở).");
            var roleLabel = string.Equals(invitee.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
                ? "Staff Leader hỗ trợ IC"
                : "Staff hỗ trợ IC";
            return (request.UserId.Value, ParticipantRoles.IcSupport, roleLabel, null);
        }

        if (type == InviteParticipantTypes.Student)
        {
            if (request.UserId is null)
                throw new ValidationException("Thiếu thông tin sinh viên cần mời.");
            var ok = await (
                from u in _db.Users
                join r in _db.Roles on u.RoleId equals r.RoleId
                where u.UserId == request.UserId.Value
                      && r.RoleCode == RoleCodes.Student
                      && u.Status == UserStatuses.Active
                      && u.PrimaryCampusId == campusId
                select u.UserId).AnyAsync(ct);
            if (!ok)
                throw new ConflictException("Sinh viên được chọn không hợp lệ (không hoạt động hoặc khác cơ sở).");
            return (request.UserId.Value, ParticipantRoles.Student, "Sinh viên hỗ trợ", null);
        }

        if (type == InviteParticipantTypes.DeptSupport)
        {
            if (request.DepartmentId is null)
                throw new ValidationException("Thiếu thông tin phòng ban cần mời.");

            var dept = await _db.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId.Value, ct)
                ?? throw new NotFoundException("Department", request.DepartmentId.Value);

            if (dept.CampusId != campusId)
                throw new ConflictException("Không thể mời phòng ban ngoài cơ sở của chuyến tiếp khách.");
            if (dept.DepartmentType != "GENERAL" || dept.Status != EntityStatuses.Active)
                throw new ConflictException("Phòng ban được chọn không hợp lệ.");

            // Backend resolves the active Department Leader (the real invitee) — frontend never
            // supplies the leader id.
            var leaders = await (
                from u in _db.Users
                join r in _db.Roles on u.RoleId equals r.RoleId
                where r.RoleCode == RoleCodes.Department
                      && u.SubRole == UserSubRoles.Leader
                      && u.Status == UserStatuses.Active
                      && u.PrimaryCampusId == campusId
                      && u.DepartmentId == dept.DepartmentId
                select new { u.UserId }).ToListAsync(ct);

            var leaderId = (dept.HeadUserId.HasValue && leaders.Any(l => l.UserId == dept.HeadUserId.Value)
                    ? dept.HeadUserId
                    : leaders.Select(l => (ulong?)l.UserId).FirstOrDefault());

            if (leaderId is null)
                throw new ConflictException("Phòng này chưa có trưởng phòng đang hoạt động, không thể gửi lời mời.");

            return (leaderId.Value, ParticipantRoles.DeptSupport, "Trưởng phòng (Phòng ban hỗ trợ)", dept.DepartmentId);
        }

        throw new ValidationException("Loại thành phần tham gia không hợp lệ.");
    }

    private static EmailActionToken NewToken(
        string tokenHash, string intendedAction, string groupKey, ulong participantId,
        ulong recipientUserId, string recipientEmail, ulong sentEmailId, ulong sentEmailRecipientId, DateTime now)
        => new()
        {
            TokenHash = tokenHash,
            ActionGroupKey = groupKey,
            ActionContext = EmailActionContexts.ParticipationResponse,
            TargetType = EmailActionTargetTypes.VisitParticipant,
            TargetId = participantId,
            IntendedAction = intendedAction,
            RecipientUserId = recipientUserId,
            RecipientEmail = recipientEmail,
            SentEmailId = sentEmailId,
            SentEmailRecipientId = sentEmailRecipientId,
            ExpiresAt = now.Add(TokenTtl),
            ResultStatus = EmailActionResultStatuses.Pending,
            CreatedAt = now,
        };

    private static string FormatWindow(DateTime start, DateTime end)
        => $"{start:HH:mm dd/MM/yyyy} - {end:HH:mm dd/MM/yyyy}";

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
