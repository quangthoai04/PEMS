using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Notifications;

namespace PEMS.Application.Delegations.Commands.AssignDepartmentStaff;

public sealed class AssignDepartmentStaffCommandHandler : IRequestHandler<AssignDepartmentStaffCommand, ulong>
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(14);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailService _email;
    private readonly IEmailActionTokenService _tokens;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly IVisitFormReadService _formReadService;

    public AssignDepartmentStaffCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        IEmailService email,
        IEmailActionTokenService tokens,
        IHtmlSanitizerService sanitizer,
        IFileStorageService storage,
        IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _email = email;
        _tokens = tokens;
        _sanitizer = sanitizer;
        _storage = storage;
        _formReadService = formReadService;
    }

    public async Task<ulong> Handle(AssignDepartmentStaffCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var leaderParticipant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId, cancellationToken)
            ?? throw new NotFoundException("VisitParticipant", request.ParticipantId);

        // Chỉ Department Leader được phân công
        if (_currentUser.RoleCode != RoleCodes.Department || _currentUser.SubRole != UserSubRoles.Leader)
            throw new ForbiddenException("Chỉ Department Leader mới được giao việc xuống Staff.");

        var participantUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == leaderParticipant.UserId, cancellationToken);
        if (participantUser?.DepartmentId != _currentUser.DepartmentId)
            throw new ForbiddenException("Bạn chỉ có thể giao nhiệm vụ thuộc phòng ban của mình.");

        // if (leaderParticipant.ParticipantRole != ParticipantRoles.DeptSupport)
        //     throw new ConflictException("Chỉ có thể giao nhiệm vụ từ vai trò DEPT_SUPPORT.");

        var targetStaff = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.DepartmentStaffUserId, cancellationToken)
            ?? throw new NotFoundException("User", request.DepartmentStaffUserId);

        // Kiểm tra target user cũng phải là DEPT và cùng department
        if (targetStaff.Role?.RoleCode != RoleCodes.Department || targetStaff.DepartmentId != _currentUser.DepartmentId)
            throw new ConflictException("Người được phân công phải thuộc cùng phòng ban.");

        var now = _clock.VietnamNow;
        var editedContent = ValidateAndSanitizeOverride(request.EmailOverride);
        var attachInputs = OutboundEmailAttachments.From(request.EmailOverride);
        await OutboundEmailAttachments.ValidateAsync(_db, userId, attachInputs, cancellationToken);

        var instanceInfo = await (
            from inst in _db.VisitRequestCampuses
            join vr in _db.VisitRequests on inst.VisitRequestId equals vr.VisitRequestId
            where inst.VisitInstanceId == leaderParticipant.VisitInstanceId
            select new
            {
                vr.VisitRequestId,
                vr.DelegationName,
                vr.FormSchemaVersion,
                inst.PlannedStartAt,
                inst.PlannedEndAt
            }).FirstOrDefaultAsync(cancellationToken);

        // The assignment email is for THIS campus instance → v2 (incl. mixed) sources the delegation name from
        // this instance's per-campus detail (never global, never a sibling); v1 keeps the global value.
        var delegationName = instanceInfo?.DelegationName ?? "đoàn khách";
        if (instanceInfo != null && instanceInfo.FormSchemaVersion >= FormSchemaVersions.PerCampus)
        {
            var visit = await _db.VisitRequests.AsNoTracking()
                .FirstAsync(v => v.VisitRequestId == instanceInfo.VisitRequestId, cancellationToken);
            var formContent = await _formReadService.ResolveCampusFormContentAsync(
                visit, new[] { leaderParticipant.VisitInstanceId }, cancellationToken);
            delegationName = formContent[leaderParticipant.VisitInstanceId].DelegationName;
        }
        var templateId = await _db.EmailTemplates
            .Where(t => t.TemplateCode == EmailActionTemplates.ParticipantInvitation)
            .Select(t => (ulong?)t.EmailTemplateId)
            .FirstOrDefaultAsync(cancellationToken);

        var existingParticipant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.VisitInstanceId == leaderParticipant.VisitInstanceId && p.UserId == targetStaff.UserId, cancellationToken);

        VisitParticipant assignedParticipant;
        ulong sentEmailId;
        ulong sentEmailRecipientId;
        string finalSubject;
        string finalBody;
        if (existingParticipant != null)
        {
            // Nếu đã tham gia, cập nhật role và status nếu cần thiết
            existingParticipant.ParticipantRole = ParticipantRoles.DeptSupport;
            existingParticipant.Status = ParticipantStatuses.Assigned;
            existingParticipant.AssignedBy = userId;
            existingParticipant.AssignedAt = now;
            existingParticipant.UpdatedAt = now;
            existingParticipant.UpdatedBy = userId;
            assignedParticipant = existingParticipant;
        }
        else
        {
            // Tạo participant mới cho Staff
            assignedParticipant = new VisitParticipant
            {
                VisitInstanceId = leaderParticipant.VisitInstanceId,
                UserId = targetStaff.UserId,
                ParticipantRole = ParticipantRoles.DeptSupport,
                IsHost = false,
                Status = ParticipantStatuses.Assigned,
                AssignedBy = userId,
                AssignedAt = now,
                Note = request.Note,
                CreatedAt = now,
                CreatedBy = userId
            };

            _db.VisitParticipants.Add(assignedParticipant);
        }

        leaderParticipant.Status = ParticipantStatuses.Assigned;
        leaderParticipant.UpdatedAt = now;
        leaderParticipant.UpdatedBy = userId;

        if (existingParticipant != null)
        {
            await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _db, EmailActionTargetTypes.VisitParticipant, existingParticipant.ParticipantId, "Thành phần tham gia đã được phân công trực tiếp.", now, cancellationToken);
        }

        await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitParticipant, leaderParticipant.ParticipantId, "Thành phần tham gia đã được phân công trực tiếp.", now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var acceptRaw = _tokens.GenerateRawToken();
        var declineRaw = _tokens.GenerateRawToken();
        var groupKey = Guid.NewGuid().ToString("N");
        var acceptUrl = _tokens.BuildPublicActionUrl(acceptRaw);
        var declineUrl = _tokens.BuildPublicActionUrl(declineRaw);

        if (editedContent != null)
        {
            finalSubject = request.EmailOverride!.Subject!.Trim();
            var content = EmailComposition.StripActionArtifacts(editedContent);
            finalBody = EmailComposition.BrandedShell(content + EmailComposition.AcceptDeclineBlock(acceptUrl, declineUrl));
        }
        else
        {
            finalSubject = $"[PEMS] Bạn được ủy quyền tham gia đón tiếp — {delegationName}";
            finalBody = EmailComposition.BrandedShell(DefaultContentHtml(targetStaff.FullName, delegationName, instanceInfo?.PlannedStartAt, instanceInfo?.PlannedEndAt)
                + EmailComposition.AcceptDeclineBlock(acceptUrl, declineUrl));
        }

        var sentEmail = new SentEmail
        {
            EmailTemplateId = templateId,
            RelatedType = EmailActionTargetTypes.VisitParticipant,
            RelatedId = assignedParticipant.ParticipantId,
            Subject = finalSubject,
            BodySnapshot = finalBody,
            Status = "QUEUED",
            SentBy = userId,
            CreatedAt = now,
        };
        OutboundEmailAttachments.Attach(sentEmail, attachInputs, now);
        _db.SentEmails.Add(sentEmail);
        await _db.SaveChangesAsync(cancellationToken);

        var sentRecipient = new SentEmailRecipient
        {
            SentEmailId = sentEmail.SentEmailId,
            RecipientEmail = targetStaff.Email,
            RecipientName = targetStaff.FullName,
            RecipientType = "TO",
            DeliveryStatus = "QUEUED",
            CreatedAt = now,
        };
        _db.SentEmailRecipients.Add(sentRecipient);
        await _db.SaveChangesAsync(cancellationToken);

        _db.EmailActionTokens.Add(NewToken(_tokens.Hash(acceptRaw), EmailIntendedActions.Accept, groupKey, assignedParticipant.ParticipantId, targetStaff.UserId, targetStaff.Email, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));
        _db.EmailActionTokens.Add(NewToken(_tokens.Hash(declineRaw), EmailIntendedActions.Decline, groupKey, assignedParticipant.ParticipantId, targetStaff.UserId, targetStaff.Email, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));
        _db.Notifications.Add(new Notification
        {
            RecipientUserId = targetStaff.UserId,
            NotificationType = "VISIT_INVITATION_ASSIGNED",
            Title = "Bạn được ủy quyền tham gia đón tiếp",
            Message = $"Bạn được ủy quyền tham gia đón tiếp đoàn {delegationName}.",
            RelatedType = "VisitParticipant",
            RelatedId = assignedParticipant.ParticipantId,
            VisitRequestId = instanceInfo?.VisitRequestId,
            VisitInstanceId = leaderParticipant.VisitInstanceId,
            ActionType = "OPEN_VISIT_INVITATION",
            ActionUrl = $"/dashboard/departments/{_currentUser.DepartmentId}/invitations/{assignedParticipant.ParticipantId}",
            IsRead = false,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        sentEmailId = sentEmail.SentEmailId;
        sentEmailRecipientId = sentRecipient.SentEmailRecipientId;

        try
        {
            var outboundAttachments = await OutboundEmailAttachments.LoadAsync(_db, _storage, attachInputs, cancellationToken);
            await _email.SendAsync(new OutboundEmail
            {
                ToEmail = targetStaff.Email,
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

        return assignedParticipant.ParticipantId;
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

        var sanitized = _sanitizer.SanitizeEmailHtml(ov.BodyHtml);
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ValidationException("Nội dung email không hợp lệ sau khi lọc.");
        return sanitized;
    }

    private static string DefaultContentHtml(string assigneeName, string delegationName, DateTime? startAt, DateTime? endAt)
    {
        string HE(string? s) => EmailComposition.HE(s);
        var time = startAt.HasValue
            ? $"{startAt.Value:HH:mm dd/MM/yyyy}{(endAt.HasValue ? $" - {endAt.Value:HH:mm dd/MM/yyyy}" : string.Empty)}"
            : "Chưa có thông tin";
        return $@"<p>Xin chào <strong>{HE(assigneeName)}</strong>,</p>
<p>Bạn được ủy quyền tham gia đón tiếp đoàn <strong>{HE(delegationName)}</strong>.</p>
<div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
  <ul style=""margin:0;padding-left:20px;line-height:1.7"">
    <li><strong>Đoàn khách:</strong> {HE(delegationName)}</li>
    <li><strong>Thời gian:</strong> {HE(time)}</li>
  </ul>
</div>
<p>Vui lòng phản hồi bằng một trong các nút dưới đây.</p>";
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

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
