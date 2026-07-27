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

/// <summary>
/// A Department Leader hands one of their own staff a visit they were invited to support.
///
/// <para>
/// The message is a <c>VISIT_DEPARTMENT_STAFF_ASSIGNMENT</c>, not an invitation: the recipient is being
/// told they have been assigned, by name, by their own department. It still carries accept/decline
/// buttons so the reception owner learns whether the person can actually come.
/// </para>
/// <para>
/// <b>The write ordering here is unchanged from before this handler used the dispatcher</b> — a sequence
/// of saves with no explicit transaction. Recording the message before the tokens is what keeps
/// <c>email_action_tokens.sent_email_id</c> pointing at a row that exists; making the whole sequence
/// atomic would be a change to this command's lifecycle, which is a separate decision.
/// </para>
/// </summary>
public sealed class AssignDepartmentStaffCommandHandler : IRequestHandler<AssignDepartmentStaffCommand, ulong>
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(14);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IEmailActionTokenService _tokens;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly IVisitFormReadService _formReadService;

    public AssignDepartmentStaffCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        ISystemEmailDispatcher dispatcher,
        IEmailActionTokenService tokens,
        IHtmlSanitizerService sanitizer,
        IFileStorageService storage,
        IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _dispatcher = dispatcher;
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
        var content = ResolveContent(request.EmailOverride);
        var attachInputs = OutboundEmailAttachments.From(request.EmailOverride);
        await OutboundEmailAttachments.ValidateAsync(_db, userId, attachInputs, cancellationToken);

        var instanceInfo = await (
            from inst in _db.VisitRequestCampuses
            join vr in _db.VisitRequests on inst.VisitRequestId equals vr.VisitRequestId
            where inst.VisitInstanceId == leaderParticipant.VisitInstanceId
            select new
            {
                vr.VisitRequestId,
                inst.CampusId,
                inst.PlannedStartAt,
                inst.PlannedEndAt
            }).FirstOrDefaultAsync(cancellationToken);

        // The assignment email is for THIS campus instance, so the delegation name comes from this
        // instance's own per-campus detail — never a sibling campus.
        var delegationName = "đoàn khách";
        if (instanceInfo != null)
        {
            var visit = await _db.VisitRequests.AsNoTracking()
                .FirstAsync(v => v.VisitRequestId == instanceInfo.VisitRequestId, cancellationToken);
            var formContent = await _formReadService.ResolveCampusFormContentAsync(
                visit, new[] { leaderParticipant.VisitInstanceId }, cancellationToken);
            delegationName = formContent[leaderParticipant.VisitInstanceId].DelegationName;
        }

        // The message names the campus and the department, because "you have been assigned" is only
        // actionable if the reader can tell which visit and on whose authority.
        var campusName = instanceInfo is null ? "FPT University" : await _db.Campuses
            .Where(c => c.CampusId == instanceInfo.CampusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "FPT University";
        var departmentName = await _db.Departments
            .Where(d => d.DepartmentId == targetStaff.DepartmentId).Select(d => d.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Phòng ban";

        var existingParticipant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.VisitInstanceId == leaderParticipant.VisitInstanceId && p.UserId == targetStaff.UserId, cancellationToken);

        VisitParticipant assignedParticipant;
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

        // Record the message (content from email_templates, or the Leader's own words) BEFORE the tokens,
        // so both point at a sent_emails row that already exists. Nothing is sent yet.
        var prepared = await _dispatcher.PrepareAsync(
            new SystemEmailRequest(
                SystemEmailTemplates.VisitDepartmentStaffAssignment,
                new EmailRecipient(targetStaff.Email, targetStaff.FullName),
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["recipientName"] = targetStaff.FullName,
                    ["delegationName"] = delegationName,
                    ["campusName"] = campusName,
                    ["plannedTime"] = FormatWindow(instanceInfo?.PlannedStartAt, instanceInfo?.PlannedEndAt),
                    ["departmentName"] = departmentName,
                },
                TrustedBlocks: new System.Collections.Generic.Dictionary<string, string>
                {
                    [EmailTrustedBlocks.ActionBlock] = EmailComposition.AcceptDeclineBlock(acceptUrl, declineUrl),
                },
                RelatedType: EmailActionTargetTypes.VisitParticipant,
                RelatedId: assignedParticipant.ParticipantId,
                SentBy: userId)
            {
                Content = content,
            },
            cancellationToken);

        AttachTo(prepared.SentEmailId, attachInputs, now);

        _db.EmailActionTokens.Add(NewToken(_tokens.Hash(acceptRaw), EmailIntendedActions.Accept, groupKey, assignedParticipant.ParticipantId, targetStaff.UserId, targetStaff.Email, prepared.SentEmailId, prepared.SentEmailRecipientId, now));
        _db.EmailActionTokens.Add(NewToken(_tokens.Hash(declineRaw), EmailIntendedActions.Decline, groupKey, assignedParticipant.ParticipantId, targetStaff.UserId, targetStaff.Email, prepared.SentEmailId, prepared.SentEmailRecipientId, now));
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

        // Attachment bytes are streamed after the rows are saved, so a slow file store cannot delay the
        // assignment itself; a failure here leaves the message FAILED and resendable.
        try
        {
            var outboundAttachments = await OutboundEmailAttachments.LoadAsync(
                _db, _storage, attachInputs, cancellationToken);

            await _dispatcher.DeliverAsync(
                prepared with { Attachments = outboundAttachments }, cancellationToken);
        }
        catch (Exception)
        {
            // The exception text is not recorded: a storage failure can name a path or a signed URL, and
            // this message is shown in the email history.
            await MarkDeliveryFailedAsync(
                prepared, "Không đọc được tệp đính kèm khi gửi email.", now, cancellationToken);
        }

        return assignedParticipant.ParticipantId;
    }

    /// <summary>
    /// Turns the optional Leader edit into a content mode. Validation and sanitising happen inside
    /// <see cref="SystemEmailContent.AuthoredByUser.Create"/>, which is the only way to build the type.
    /// </summary>
    private SystemEmailContent ResolveContent(EmailOverride? ov)
        => ov is null || !ov.UseEditedContent
            ? SystemEmailContent.FromTemplate.Instance
            : SystemEmailContent.AuthoredByUser.Create(
                ov.Subject, EmailComposition.ResolveEditableHtml(ov), _sanitizer);

    /// <summary>Adds sent_email_attachments rows for a message the dispatcher has already written.</summary>
    private void AttachTo(ulong sentEmailId, System.Collections.Generic.IReadOnlyList<EmailDraftAttachmentInput> inputs, DateTime now)
    {
        var order = 0;
        foreach (var a in inputs)
        {
            _db.SentEmailAttachments.Add(new SentEmailAttachment
            {
                SentEmailId = sentEmailId,
                FileId = a.FileId,
                AttachmentType = EmailDraftWriter.ParseAttachmentType(a.AttachmentType),
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
        var sentEmail = await _db.SentEmails
            .FirstOrDefaultAsync(e => e.SentEmailId == prepared.SentEmailId, ct);
        if (sentEmail is not null)
        {
            sentEmail.Status = "FAILED";
            sentEmail.LastAttemptAt = now;
            sentEmail.ErrorMessage = safeMessage;
        }

        var rec = await _db.SentEmailRecipients
            .FirstOrDefaultAsync(r => r.SentEmailRecipientId == prepared.SentEmailRecipientId, ct);
        if (rec is not null)
        {
            rec.DeliveryStatus = "FAILED";
            rec.ErrorMessage = safeMessage;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string FormatWindow(DateTime? start, DateTime? end)
        => start.HasValue
            ? $"{start.Value:HH:mm dd/MM/yyyy}{(end.HasValue ? $" - {end.Value:HH:mm dd/MM/yyyy}" : string.Empty)}"
            : "Chưa có thông tin";

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

}
