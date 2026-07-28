using System;
using System.Globalization;
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

namespace PEMS.Application.Delegations.Commands.PrepareVisitLogistics;

/// <summary>
/// Implements the Host → Department logistics request: creates the REQUESTED item, notifies the
/// department's active leader, and emails them a login-required detail link (no public token). The
/// logistics item is committed BEFORE the email is sent, so a transient SMTP failure never loses it.
/// </summary>
public sealed class PrepareVisitLogisticsCommandHandler
    : IRequestHandler<PrepareVisitLogisticsCommand, PrepareVisitLogisticsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailService _email;
    private readonly IEmailActionTokenService _tokens;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public PrepareVisitLogisticsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IEmailService email, IEmailActionTokenService tokens, IHtmlSanitizerService sanitizer,
        IFileStorageService storage, PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _email = email;
        _tokens = tokens;
        _sanitizer = sanitizer;
        _storage = storage;
        _normalizer = normalizer;
        _notificationService = notificationService;
    }

    public async Task<PrepareVisitLogisticsResponse> Handle(
        PrepareVisitLogisticsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Host-only, prep window.
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được gửi yêu cầu hậu cần.");
        if (instance.Status != VisitInstanceStatus.Assigned && instance.Status != VisitInstanceStatus.BeforeVisit)
            throw new ConflictException("Chỉ có thể gửi yêu cầu hậu cần trong giai đoạn chuẩn bị.");

        var mode = string.IsNullOrWhiteSpace(request.CoordinationMode)
            ? LogisticsCoordinationModes.SystemRequest
            : request.CoordinationMode!.Trim().ToUpperInvariant();
        var offline = mode == LogisticsCoordinationModes.OfflineCoordinated;

        // Resolve the target department: REQUIRED for SYSTEM_REQUEST, OPTIONAL for OFFLINE_COORDINATED.
        ulong? requestedDeptId = null;
        ulong? leaderUserId = null;
        string? leaderName = null;
        string? leaderEmail = null;
        if (request.DepartmentId is { } deptId && deptId > 0)
        {
            var dept = await _db.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == deptId, cancellationToken)
                ?? throw new NotFoundException("Department", deptId);
            if (dept.CampusId != instance.CampusId)
                throw new ConflictException("Không thể gửi yêu cầu tới phòng ban ngoài cơ sở của chuyến tiếp khách.");
            if (dept.DepartmentType != "GENERAL" || dept.Status != EntityStatuses.Active)
                throw new ConflictException("Phòng ban được chọn không hợp lệ.");
            requestedDeptId = dept.DepartmentId;

            if (!offline)
            {
                // Resolve the active Department Leader (the email recipient).
                var leaders = await (
                    from u in _db.Users
                    join r in _db.Roles on u.RoleId equals r.RoleId
                    where r.RoleCode == RoleCodes.Department
                          && u.SubRole == UserSubRoles.Leader
                          && u.Status == UserStatuses.Active
                          && u.PrimaryCampusId == instance.CampusId
                          && u.DepartmentId == dept.DepartmentId
                    select new { u.UserId, u.FullName, u.Email }).ToListAsync(cancellationToken);

                var leader = (dept.HeadUserId.HasValue ? leaders.FirstOrDefault(l => l.UserId == dept.HeadUserId.Value) : null)
                             ?? leaders.FirstOrDefault();
                if (leader is null)
                    throw new ConflictException("Phòng này chưa có trưởng phòng đang hoạt động, không thể gửi yêu cầu.");
                leaderUserId = leader.UserId;
                leaderName = leader.FullName;
                leaderEmail = leader.Email;
            }
        }

        if (!offline && requestedDeptId is null)
            throw new ConflictException("Vui lòng chọn phòng ban xử lý.");
        if (offline && string.IsNullOrWhiteSpace(request.OfflineCoordinationNote))
            throw new ValidationException("Vui lòng nhập ghi chú trao đổi bên ngoài (bắt buộc).");

        // Offline-coordinated requests don't send an email, so any email override is ignored.
        var editedContent = offline ? null : ValidateAndSanitizeOverride(request.EmailOverride);
        var attachInputs = offline
            ? System.Array.Empty<EmailDraftAttachmentInput>()
            : OutboundEmailAttachments.From(request.EmailOverride);
        await OutboundEmailAttachments.ValidateAsync(_db, actorId, attachInputs, cancellationToken);

        // Mixed per-campus v2: the logistics email uses THIS instance's detail name.
        var delegationName = (await Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { instance.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(instance.VisitInstanceId) ?? "FPT University";
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "FPT University";
        var requesterName = await _db.Users
            .Where(u => u.UserId == actorId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Host";

        var template = await _db.EmailTemplates
            .Where(t => t.TemplateCode == EmailActionTemplates.LogisticsRequestToDepartment)
            .FirstOrDefaultAsync(cancellationToken);
        var templateId = template?.EmailTemplateId;

        var now = _clock.VietnamNow;
        var itemType = request.ItemType.Trim().ToUpperInvariant();
        var usageStart = ParseLocal(request.UsageStartAt);
        var usageEnd = ParseLocal(request.UsageEndAt);
        var title = request.Title.Trim();

        // One active item per fixed category. The "Chuẩn bị chi tiết" screen identifies a fixed
        // category by (item_type, title) and shows a single active card each (Welcome LED, Xe điện,
        // Người lái, Phòng họp, Teabreak…); only "OTHER" may have many. We re-check on the server so a
        // double-click or a direct API call can't create a duplicate active item — the UI hiding the
        // create form is never enough. "Active" = any status that is not closed
        // (CANCELLED/REJECTED/DECLINED), matching the frontend's notion of an active item.
        if (itemType != "OTHER")
        {
            var hasActiveDuplicate = await _db.VisitLogisticsItems.AnyAsync(
                l => l.VisitInstanceId == instance.VisitInstanceId
                     && l.ItemType == itemType
                     && l.Title == title
                     && l.Status != LogisticsItemStatus.Cancelled
                     && l.Status != LogisticsItemStatus.Rejected
                     && l.Status != LogisticsItemStatus.Declined,
                cancellationToken);
            if (hasActiveDuplicate)
                throw new ConflictException(
                    $"Đã tồn tại một yêu cầu hậu cần đang xử lý cho \"{title}\". Vui lòng hủy yêu cầu hiện tại trước khi tạo mới.");
        }

        VisitLogisticsItem item;
        ulong sentEmailId = 0;
        ulong sentEmailRecipientId = 0;
        string finalSubject = string.Empty;
        string finalBody = string.Empty;

        await using (var transaction = await _db.BeginTransactionAsync(cancellationToken))
        {
            item = new VisitLogisticsItem
            {
                VisitInstanceId = instance.VisitInstanceId,
                ItemType = itemType,
                Title = title,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Quantity = request.Quantity,
                UsageStartAt = usageStart,
                UsageEndAt = usageEnd,
                // OFFLINE_COORDINATED is already handled outside the system → DONE (no further workflow);
                // SYSTEM_REQUEST starts at REQUESTED for the department to process.
                Status = offline ? LogisticsItemStatus.Done : LogisticsItemStatus.Requested,
                CoordinationMode = offline ? LogisticsCoordinationModes.OfflineCoordinated : LogisticsCoordinationModes.SystemRequest,
                OfflineCoordinationNote = offline ? request.OfflineCoordinationNote!.Trim() : null,
                RequestedBy = actorId,
                RequestedToDepartmentId = requestedDeptId,
                RequestedAt = now,
                CompletedAt = offline ? now : (DateTime?)null,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            _db.VisitLogisticsItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);

            // Email + notification only for SYSTEM_REQUEST. OFFLINE_COORDINATED leaves no email trail.
            if (!offline)
            {
                var acceptRaw = _tokens.GenerateRawToken();
                var declineRaw = _tokens.GenerateRawToken();
                var groupKey = Guid.NewGuid().ToString("N");

                var acceptUrl = _tokens.BuildPublicActionUrl(acceptRaw);
                var declineUrl = _tokens.BuildPublicActionUrl(declineRaw);
                var detailUrl = _tokens.BuildLogisticsDetailUrl(item.LogisticsItemId);

                if (editedContent != null)
                {
                    finalSubject = request.EmailOverride!.Subject!.Trim();
                    var content = EmailComposition.StripActionArtifacts(editedContent);
                    finalBody = EmailComposition.BrandedShell(content + EmailComposition.LogisticsActionBlock(acceptUrl, declineUrl, detailUrl));
                }
                else if (template != null)
                {
                    var context = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "departmentLeaderName", leaderName ?? string.Empty },
                        { "departmentHeadName", leaderName ?? string.Empty },
                        { "requesterName", requesterName },
                        { "DelegationName", delegationName },
                        { "visitName", delegationName },
                        { "CampusName", campusName },
                        { "campusName", campusName },
                        { "logisticsTitle", item.Title },
                        { "logisticsItemTitle", item.Title },
                        { "itemType", item.ItemType },
                        { "quantity", item.Quantity?.ToString() ?? "—" },
                        { "usageStartAt", usageStart?.ToString("HH:mm dd/MM/yyyy") ?? "—" },
                        { "usageEndAt", usageEnd?.ToString("HH:mm dd/MM/yyyy") ?? "—" },
                        { "detailUrl", detailUrl }
                    };
                    finalSubject = EmailComposition.RenderTemplate(template.SubjectVi ?? $"[PEMS] Yêu cầu hậu cần mới — {item.Title}", context, "LOGISTICS_REQUEST");
                    var contentHtml = EmailComposition.RenderTemplate(template.BodyVi ?? string.Empty, context, "LOGISTICS_REQUEST");
                    
                    // The DB template might contain old detail link HTML. We strip it and always append the full 3-button logistics action block.
                    contentHtml = EmailComposition.StripActionArtifacts(contentHtml);
                    contentHtml += EmailComposition.LogisticsActionBlock(acceptUrl, declineUrl, detailUrl);

                    finalBody = EmailComposition.BrandedShell(contentHtml);
                }
                else
                {
                    finalSubject = $"[PEMS] Yêu cầu hậu cần mới — {item.Title}";
                    finalBody = EmailComposition.BrandedShell(
                        DefaultContentHtml(leaderName!, requesterName, delegationName, campusName, item, usageStart, usageEnd)
                        + EmailComposition.LogisticsActionBlock(acceptUrl, declineUrl, detailUrl));
                }

                finalBody = await _normalizer.NormalizeHtmlAsync(finalBody, cancellationToken);

                var sentEmail = new SentEmail
                {
                    EmailTemplateId = templateId,
                    RelatedType = EmailActionTargetTypes.LogisticsItem,
                    RelatedId = item.LogisticsItemId,
                    Subject = finalSubject,
                    BodySnapshot = finalBody,
                    BodyFormat = PEMS.Domain.Enums.EmailBodyFormat.HTML,
                    Status = "QUEUED",
                    SentBy = actorId,
                    CreatedAt = now,
                };
                // Inline images + file attachments (cascade-insert with the sent_emails row).
                OutboundEmailAttachments.Attach(sentEmail, attachInputs, now);
                _db.SentEmails.Add(sentEmail);
                await _db.SaveChangesAsync(cancellationToken);

                var sentRecipient = new SentEmailRecipient
                {
                    SentEmailId = sentEmail.SentEmailId,
                    RecipientEmail = leaderEmail!,
                    RecipientName = leaderName!,
                    RecipientType = "TO",
                    DeliveryStatus = "QUEUED",
                    CreatedAt = now,
                };
                _db.SentEmailRecipients.Add(sentRecipient);
                await _db.SaveChangesAsync(cancellationToken);

                await _notificationService.CreateAsync(
                    new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: leaderUserId!.Value,
                        Title: "Có yêu cầu hậu cần mới",
                        Message: $"Host {requesterName} đã gửi yêu cầu \"{item.Title}\" cho đoàn {delegationName} tại {campusName}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsRequestCreated,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                        RelatedId: item.LogisticsItemId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                        IsActionRequired: true,
                        VisitInstanceId: item.VisitInstanceId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                        ActionUrl: requestedDeptId.HasValue
                            ? $"/dashboard/departments/{requestedDeptId.Value}/tasks/{item.LogisticsItemId}"
                            : $"/dashboard/visit/process/{item.VisitInstanceId}"),
                    cancellationToken
                );

                // Flush changes so sentEmail and sentRecipient get their autoincrement IDs
                await _db.SaveChangesAsync(cancellationToken);

                // Insert Email Action Tokens for Accept/Decline using the generated IDs
                _db.EmailActionTokens.Add(NewToken(_tokens.Hash(acceptRaw), EmailIntendedActions.Accept, groupKey, item.LogisticsItemId, leaderUserId.Value, leaderEmail!, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));
                _db.EmailActionTokens.Add(NewToken(_tokens.Hash(declineRaw), EmailIntendedActions.Decline, groupKey, item.LogisticsItemId, leaderUserId.Value, leaderEmail!, sentEmail.SentEmailId, sentRecipient.SentEmailRecipientId, now));

                await _db.SaveChangesAsync(cancellationToken);
                sentEmailId = sentEmail.SentEmailId;
                sentEmailRecipientId = sentRecipient.SentEmailRecipientId;
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = instance.CampusId,
                Action = offline ? "CREATE_VISIT_LOGISTICS_OFFLINE" : "CREATE_VISIT_LOGISTICS_REQUEST",
                EntityType = "VisitLogisticsItem",
                EntityId = item.LogisticsItemId,
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // Offline: nothing to send — the item is recorded (DONE) for traceability.
        if (offline)
            return new PrepareVisitLogisticsResponse(
                true, true, item.LogisticsItemId, "SKIPPED", 0,
                "Đã lưu yêu cầu hậu cần (đã trao đổi bên ngoài hệ thống).");

        string emailStatus;
        try
        {
            // Real MIME path (HTML body) with the host's inline images (cid) + file attachments
            // streamed from storage (Google Drive / local) once and reused.
            var outboundAttachments = await OutboundEmailAttachments.LoadAsync(_db, _storage, attachInputs, cancellationToken);
            await _email.SendAsync(new PEMS.Application.Common.Interfaces.OutboundEmail
            {
                ToEmail = leaderEmail!,
                Subject = finalSubject,
                Body = finalBody,
                IsHtml = true,
                Attachments = outboundAttachments,
            }, cancellationToken);
            emailStatus = "SENT";
            await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "SENT", actorId, now, null, cancellationToken);
        }
        catch (Exception ex)
        {
            emailStatus = "FAILED";
            await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "FAILED", actorId, now, ex.Message, cancellationToken);
        }

        var message = emailStatus == "SENT"
            ? "Đã gửi yêu cầu hậu cần."
            : "Đã tạo yêu cầu hậu cần nhưng gửi email thất bại.";

        return new PrepareVisitLogisticsResponse(true, true, item.LogisticsItemId, emailStatus, sentEmailId, message);
    }

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
        // Email-profile sanitize so inline-image <img src="cid:..."> + data-* refs survive.
        var sanitized = _sanitizer.SanitizeEmailHtml(rawHtml);
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ValidationException("Nội dung email không hợp lệ sau khi lọc.");
        return sanitized;
    }

    private static string DefaultContentHtml(
        string leaderName, string requesterName, string delegationName, string campusName,
        VisitLogisticsItem item, DateTime? usageStart, DateTime? usageEnd)
    {
        string HE(string? s) => EmailComposition.HE(s);
        var usage = (usageStart.HasValue || usageEnd.HasValue)
            ? $"<li><strong>Thời gian sử dụng:</strong> {HE(usageStart?.ToString("HH:mm dd/MM/yyyy") ?? "—")} - {HE(usageEnd?.ToString("HH:mm dd/MM/yyyy") ?? "—")}</li>"
            : string.Empty;
        var qty = item.Quantity.HasValue ? $"<li><strong>Số lượng dự kiến:</strong> {item.Quantity}</li>" : string.Empty;
        return $@"<p>Xin chào <strong>{HE(leaderName)}</strong>,</p>
<p>Host <strong>{HE(requesterName)}</strong> đã gửi một yêu cầu hậu cần cho đoàn <strong>{HE(delegationName)}</strong> tại {HE(campusName)}.</p>
<div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
  <ul style=""margin:0;padding-left:20px;line-height:1.7"">
    <li><strong>Hạng mục:</strong> {HE(item.Title)} ({HE(item.ItemType)})</li>
    {qty}
    {usage}
  </ul>
</div>
<p>Vui lòng đăng nhập hệ thống để tiếp nhận, đề xuất thay đổi hoặc phân công nhân sự xử lý.</p>";
    }

    private static DateTime? ParseLocal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Unspecified)
            : (DateTime?)null;
    }

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

    private static EmailActionToken NewToken(
        string tokenHash, string intendedAction, string groupKey,
        ulong targetId, ulong targetUserId, string recipientEmail,
        ulong sentEmailId, ulong sentEmailRecipientId, DateTime now)
    {
        return new EmailActionToken
        {
            TokenHash = tokenHash,
            ActionContext = EmailActionContexts.LogisticsRequestResponse,
            IntendedAction = intendedAction,
            ActionGroupKey = groupKey,
            TargetType = EmailActionTargetTypes.LogisticsItem,
            TargetId = targetId,
            RecipientUserId = targetUserId,
            RecipientEmail = recipientEmail,
            SentEmailId = sentEmailId,
            SentEmailRecipientId = sentEmailRecipientId,
            ExpiresAt = now.AddDays(14),
            ResultStatus = "PENDING",
            CreatedAt = now,
        };
    }
}
