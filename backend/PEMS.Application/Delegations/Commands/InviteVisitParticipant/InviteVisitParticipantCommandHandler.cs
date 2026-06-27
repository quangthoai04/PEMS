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
/// </summary>
public sealed class InviteVisitParticipantCommandHandler
    : IRequestHandler<InviteVisitParticipantCommand, InviteVisitParticipantResponse>
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(14);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailService _email;
    private readonly IEmailActionTokenService _tokens;
    private readonly IHtmlSanitizerService _sanitizer;

    public InviteVisitParticipantCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IEmailService email, IEmailActionTokenService tokens, IHtmlSanitizerService sanitizer)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _email = email;
        _tokens = tokens;
        _sanitizer = sanitizer;
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
        var (targetUserId, participantRole, roleLabel) =
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
        var delegationName = instance.VisitRequest.DelegationName;

        var now = _clock.UtcNow;

        // ── Validate the optional host-edited email content (Part C) ──
        var editedContent = ValidateAndSanitizeOverride(request.EmailOverride);

        // Link the sent_email back to its source template for traceability.
        var templateCode = participantRole == ParticipantRoles.DeptSupport
            ? EmailActionTemplates.DepartmentLeaderInvitation
            : participantRole == ParticipantRoles.Student
                ? EmailActionTemplates.StudentInvitation
                : EmailActionTemplates.ParticipantInvitation;
        var templateId = await _db.EmailTemplates
            .Where(t => t.TemplateCode == templateCode)
            .Select(t => (ulong?)t.EmailTemplateId)
            .FirstOrDefaultAsync(cancellationToken);

        VisitParticipant participant;
        ulong sentEmailId;
        ulong sentEmailRecipientId;
        string finalSubject;
        string finalBody;

        await using (var transaction = await _db.BeginTransactionAsync(cancellationToken))
        {
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

            // 2) Mint the real one-time tokens FIRST so the body can carry the live URLs, then build
            //    the final subject/body (edited content + system action block, or the default email).
            var acceptRaw = _tokens.GenerateRawToken();
            var declineRaw = _tokens.GenerateRawToken();
            var groupKey = Guid.NewGuid().ToString("N");

            var acceptUrl = _tokens.BuildPublicActionUrl(acceptRaw);
            var declineUrl = _tokens.BuildPublicActionUrl(declineRaw);
            var assignUrl = participantRole == ParticipantRoles.DeptSupport
                ? _tokens.BuildDepartmentAssignmentUrl(instance.VisitInstanceId, participant.ParticipantId)
                : null;

            if (editedContent != null)
            {
                // Host-edited: trust only the content; the backend injects the real action block.
                finalSubject = request.EmailOverride!.Subject!.Trim();
                var content = EmailComposition.StripActionArtifacts(editedContent);
                finalBody = EmailComposition.BrandedShell(
                    content + EmailComposition.AcceptDeclineBlock(acceptUrl, declineUrl, assignUrl));
            }
            else
            {
                var mail = ParticipantInvitationEmailBuilder.Build(
                    recipientName: recipient.FullName,
                    participantRoleLabel: roleLabel,
                    delegationName: delegationName,
                    campusName: campusName,
                    plannedTimeText: FormatWindow(instance.PlannedStartAt, instance.PlannedEndAt),
                    hostName: hostName,
                    acceptUrl: acceptUrl, declineUrl: declineUrl, assignStaffUrl: assignUrl, message: request.Message);
                finalSubject = mail.Subject;
                finalBody = mail.HtmlBody;
            }

            // 3) sent_emails + recipient — body_snapshot is the FINAL content actually sent.
            var sentEmail = new SentEmail
            {
                EmailTemplateId = templateId,
                RelatedType = EmailActionTargetTypes.VisitParticipant,
                RelatedId = participant.ParticipantId,
                Subject = finalSubject,
                BodySnapshot = finalBody,
                BodyFormat = PEMS.Domain.Enums.EmailBodyFormat.HTML,
                Status = "QUEUED",
                SentBy = actorId,
                CreatedAt = now,
            };
            _db.SentEmails.Add(sentEmail);
            await _db.SaveChangesAsync(cancellationToken);

            var sentRecipient = new SentEmailRecipient
            {
                SentEmailId = sentEmail.SentEmailId,
                RecipientEmail = recipient.Email,
                RecipientName = recipient.FullName,
                RecipientType = "TO",
                DeliveryStatus = "QUEUED",
                CreatedAt = now,
            };
            _db.SentEmailRecipients.Add(sentRecipient);
            await _db.SaveChangesAsync(cancellationToken);

            // 4) ACCEPT + DECLINE one-time tokens (share an action_group_key). Only the hash is stored.
            _db.EmailActionTokens.Add(NewToken(_tokens.Hash(acceptRaw), EmailIntendedActions.Accept, groupKey, participant.ParticipantId, targetUserId, recipient.Email, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));
            _db.EmailActionTokens.Add(NewToken(_tokens.Hash(declineRaw), EmailIntendedActions.Decline, groupKey, participant.ParticipantId, targetUserId, recipient.Email, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));

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
            _db.Notifications.Add(new Notification
            {
                RecipientUserId = targetUserId,
                NotificationType = "VISIT_PARTICIPANT_INVITED",
                Title = "Lời mời tham gia tiếp khách",
                Message = $"Bạn được mời tham gia hỗ trợ đoàn {delegationName} tại {campusName}.",
                RelatedType = "VisitParticipant",
                RelatedId = participant.ParticipantId,
                IsRead = false,
                CreatedAt = now,
            });
            await _db.SaveChangesAsync(cancellationToken);

            sentEmailId = sentEmail.SentEmailId;
            sentEmailRecipientId = sentRecipient.SentEmailRecipientId;

            await transaction.CommitAsync(cancellationToken);
        }

        // ── Send the FINAL body AFTER the participant + tokens are durably committed ──
        bool emailQueued;
        string emailStatus;
        try
        {
            await _email.SendAsync(recipient.Email, finalSubject, finalBody, cancellationToken);
            emailQueued = true;
            emailStatus = "SENT";
            await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "SENT", actorId, now, null, cancellationToken);
        }
        catch (Exception ex)
        {
            // The invitation is already persisted; record the failure so it can be retried/resent.
            emailQueued = false;
            emailStatus = "FAILED";
            await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "FAILED", actorId, now, ex.Message, cancellationToken);
        }

        var message = emailQueued
            ? "Đã gửi lời mời."
            : "Đã tạo lời mời nhưng gửi email thất bại.";

        return new InviteVisitParticipantResponse(
            participant.ParticipantId, targetUserId, participantRole, participant.Status,
            emailQueued, recipient.Email, message, emailStatus, sentEmailId);
    }

    /// <summary>Validates + sanitizes the optional host-edited email; returns the sanitized content
    /// HTML, or null when no override was supplied.</summary>
    private string? ValidateAndSanitizeOverride(EmailOverride? ov)
    {
        if (ov is null || !ov.UseEditedContent) return null;

        if (string.IsNullOrWhiteSpace(ov.Subject))
            throw new ValidationException("Tiêu đề email không được để trống.");
        if (ov.Subject.Trim().Length > EmailOverrideLimits.SubjectMax)
            throw new ValidationException($"Tiêu đề email tối đa {EmailOverrideLimits.SubjectMax} ký tự.");

        // Prefer the host-edited plain text (bodyText) → safe HTML; fall back to legacy bodyHtml.
        var rawHtml = EmailComposition.ResolveEditableHtml(ov);
        if (string.IsNullOrWhiteSpace(rawHtml))
            throw new ValidationException("Nội dung email không được để trống.");
        if (rawHtml.Length > EmailOverrideLimits.BodyMax)
            throw new ValidationException($"Nội dung email vượt quá {EmailOverrideLimits.BodyMax} ký tự.");

        var sanitized = _sanitizer.Sanitize(rawHtml);
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ValidationException("Nội dung email không hợp lệ sau khi lọc.");
        return sanitized;
    }

    // ── helpers ──

    private async Task<(ulong UserId, string ParticipantRole, string RoleLabel)> ResolveInviteeAsync(
        string type, InviteVisitParticipantCommand request, VisitRequestCampus instance, CancellationToken ct)
    {
        var campusId = instance.CampusId;

        if (type == InviteParticipantTypes.IcSupport)
        {
            if (request.UserId is null)
                throw new ValidationException("Thiếu thông tin nhân sự cần mời.");
            var ok = await (
                from u in _db.Users
                join r in _db.Roles on u.RoleId equals r.RoleId
                join d in _db.Departments on u.DepartmentId equals d.DepartmentId
                where u.UserId == request.UserId.Value
                      && r.RoleCode == RoleCodes.Staff
                      && u.SubRole == UserSubRoles.Staff
                      && u.Status == UserStatuses.Active
                      && u.PrimaryCampusId == campusId
                      && d.DepartmentType == "IC"
                      && d.Status == EntityStatuses.Active
                      && u.UserId != instance.CurrentHostUserId
                select u.UserId).AnyAsync(ct);
            if (!ok)
                throw new ConflictException("Nhân sự được chọn không hợp lệ (không phải Staff IC đang hoạt động cùng cơ sở).");
            return (request.UserId.Value, ParticipantRoles.IcSupport, "Staff hỗ trợ IC");
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
            return (request.UserId.Value, ParticipantRoles.Student, "Sinh viên hỗ trợ");
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

            return (leaderId.Value, ParticipantRoles.DeptSupport, "Trưởng phòng (Phòng ban hỗ trợ)");
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

    private async Task UpdateEmailStatusAsync(
        ulong sentEmailId, ulong sentEmailRecipientId, string status, ulong actorId, DateTime now,
        string? error, CancellationToken ct)
    {
        var sentEmail = await _db.SentEmails.FirstOrDefaultAsync(e => e.SentEmailId == sentEmailId, ct);
        if (sentEmail != null)
        {
            sentEmail.Status = status;
            sentEmail.LastAttemptAt = now;
            sentEmail.RetryCount += 1;
            if (status == "SENT") { sentEmail.SentBy = actorId; sentEmail.SentAt = now; }
            else { sentEmail.ErrorMessage = Truncate(error, 1000); }
        }
        var rec = await _db.SentEmailRecipients.FirstOrDefaultAsync(r => r.SentEmailRecipientId == sentEmailRecipientId, ct);
        if (rec != null)
        {
            rec.DeliveryStatus = status;
            if (status == "SENT") rec.SentAt = now;
            else rec.ErrorMessage = Truncate(error, 1000);
        }
        await _db.SaveChangesAsync(ct);
    }

    private static string FormatWindow(DateTime start, DateTime end)
        => $"{start:HH:mm dd/MM/yyyy} - {end:HH:mm dd/MM/yyyy}";

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
