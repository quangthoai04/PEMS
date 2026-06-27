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

    public PrepareVisitLogisticsCommandHandler(
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

        var delegationName = instance.VisitRequest?.DelegationName ?? "FPT University";
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "FPT University";
        var requesterName = await _db.Users
            .Where(u => u.UserId == actorId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Host";

        var templateId = await _db.EmailTemplates
            .Where(t => t.TemplateCode == EmailActionTemplates.LogisticsRequestToDepartment)
            .Select(t => (ulong?)t.EmailTemplateId)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _clock.UtcNow;
        var itemType = request.ItemType.Trim().ToUpperInvariant();
        var priority = string.IsNullOrWhiteSpace(request.Priority) ? "MEDIUM" : request.Priority!.Trim().ToUpperInvariant();
        var usageStart = ParseLocal(request.UsageStartAt);
        var usageEnd = ParseLocal(request.UsageEndAt);
        var dueAt = ParseLocal(request.DueAt);
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
                Priority = priority,
                CoordinationMode = offline ? LogisticsCoordinationModes.OfflineCoordinated : LogisticsCoordinationModes.SystemRequest,
                OfflineCoordinationNote = offline ? request.OfflineCoordinationNote!.Trim() : null,
                RequestedBy = actorId,
                RequestedToDepartmentId = requestedDeptId,
                RequestedAt = now,
                CompletedAt = offline ? now : (DateTime?)null,
                DueAt = dueAt,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            _db.VisitLogisticsItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);

            // Email + notification only for SYSTEM_REQUEST. OFFLINE_COORDINATED leaves no email trail.
            if (!offline)
            {
                var detailUrl = _tokens.BuildLogisticsDetailUrl(item.LogisticsItemId);

                if (editedContent != null)
                {
                    finalSubject = request.EmailOverride!.Subject!.Trim();
                    var content = EmailComposition.StripActionArtifacts(editedContent);
                    finalBody = EmailComposition.BrandedShell(content + EmailComposition.DetailLinkBlock(detailUrl));
                }
                else
                {
                    finalSubject = $"[PEMS] Yêu cầu hậu cần mới — {item.Title}";
                    finalBody = EmailComposition.BrandedShell(
                        DefaultContentHtml(leaderName!, requesterName, delegationName, campusName, item, usageStart, usageEnd)
                        + EmailComposition.DetailLinkBlock(detailUrl));
                }

                var sentEmail = new SentEmail
                {
                    EmailTemplateId = templateId,
                    RelatedType = EmailActionTargetTypes.LogisticsItem,
                    RelatedId = item.LogisticsItemId,
                    Subject = finalSubject,
                    BodySnapshot = finalBody,
                    Status = "QUEUED",
                    SentBy = actorId,
                    CreatedAt = now,
                };
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

                _db.Notifications.Add(new Notification
                {
                    RecipientUserId = leaderUserId!.Value,
                    NotificationType = "VISIT_LOGISTICS_REQUESTED",
                    Title = "Có yêu cầu hậu cần mới",
                    Message = $"Host {requesterName} đã gửi yêu cầu \"{item.Title}\" cho đoàn {delegationName} tại {campusName}.",
                    RelatedType = "LOGISTICS_ITEM",
                    RelatedId = item.LogisticsItemId,
                    IsRead = false,
                    CreatedAt = now,
                });

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
            await _email.SendAsync(leaderEmail!, finalSubject, finalBody, cancellationToken);
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
        var sanitized = _sanitizer.Sanitize(rawHtml);
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
        var qty = item.Quantity.HasValue ? $"<li><strong>Số lượng:</strong> {item.Quantity}</li>" : string.Empty;
        return $@"<p>Xin chào <strong>{HE(leaderName)}</strong>,</p>
<p>Host <strong>{HE(requesterName)}</strong> đã gửi một yêu cầu hậu cần cho đoàn <strong>{HE(delegationName)}</strong> tại {HE(campusName)}.</p>
<div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
  <ul style=""margin:0;padding-left:20px;line-height:1.7"">
    <li><strong>Hạng mục:</strong> {HE(item.Title)} ({HE(item.ItemType)})</li>
    {qty}
    {usage}
    <li><strong>Mức ưu tiên:</strong> {HE(item.Priority)}</li>
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
}
