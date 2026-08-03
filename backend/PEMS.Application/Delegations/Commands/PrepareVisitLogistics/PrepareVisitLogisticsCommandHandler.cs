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
/// department's active leader, and emails them accept/decline/detail buttons carrying real one-time
/// tokens. The logistics item is committed BEFORE the email is sent, so a transient SMTP failure never
/// loses it.
///
/// <para>
/// Content comes from <c>LOGISTICS_REQUEST_TO_DEPARTMENT</c> unless the Host rewrote it. There is no
/// fallback: a missing, inactive or half-translated template fails with a stable error rather than
/// quietly sending something this file made up — which is what used to happen, twice over
/// (<c>SubjectVi ?? "…"</c> and a whole hard-coded body for the no-template case).
/// </para>
/// </summary>
public sealed class PrepareVisitLogisticsCommandHandler
    : IRequestHandler<PrepareVisitLogisticsCommand, PrepareVisitLogisticsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IEmailActionTokenService _tokens;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public PrepareVisitLogisticsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        ISystemEmailDispatcher dispatcher, IEmailActionTokenService tokens, IHtmlSanitizerService sanitizer,
        IFileStorageService storage, PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _dispatcher = dispatcher;
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
        var content = offline
            ? SystemEmailContent.FromTemplate.Instance
            : await ResolveContentAsync(request.EmailOverride, cancellationToken);
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

        var now = _clock.VietnamNow;
        var itemType = request.ItemType.Trim().ToUpperInvariant();
        var usageStart = ParseLocal(request.UsageStartAt);
        var usageEnd = ParseLocal(request.UsageEndAt);
        var title = request.Title.Trim();

        // Not user-facing: response deadline defaults to 24h before the item is due to be used.
        // OFFLINE_COORDINATED items are recorded DONE immediately — no department workflow to meet a
        // deadline for.
        var dueAt = offline ? (DateTime?)null : usageStart?.AddHours(-24);

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
        PreparedSystemEmail? prepared = null;

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
                DueAt = dueAt,
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

                // Render + record, inside this transaction. The caller supplies the FINAL display value
                // for every declared variable: a renderer that substitutes "Chưa có thông tin" for a
                // value nobody passed is a fallback, and a fallback is how a template edit stops mattering.
                prepared = await _dispatcher.PrepareAsync(
                    new SystemEmailRequest(
                        SystemEmailTemplates.LogisticsRequestToDepartment,
                        new EmailRecipient(leaderEmail!, leaderName),
                        new System.Collections.Generic.Dictionary<string, string>
                        {
                            ["departmentLeaderName"] = leaderName ?? string.Empty,
                            ["requesterName"] = requesterName,
                            ["logisticsTitle"] = item.Title,
                            // The label the request screen shows, not the column's code: "Suất ăn /
                            // Teabreak", never "MEAL".
                            ["logisticsItemType"] = LogisticsItemTypeText.Label(item.ItemType),
                            ["quantity"] = item.Quantity?.ToString() ?? "Chưa nhập",
                            ["usageStartAt"] = FormatMoment(usageStart) ?? "Chưa chọn thời gian",
                            ["usageEndAt"] = FormatMoment(usageEnd) ?? "Chưa chọn thời gian",
                            // The Host's "Mô tả chi tiết", verbatim. It travels under its own name now:
                            // it used to be passed as "coordinationNote" and printed under the heading
                            // "Ghi chú phối hợp", which is a different field entirely — so the one piece
                            // of the request that says WHAT to prepare arrived labelled as something
                            // else, and the preview, which supplies the real coordination note (always
                            // absent on a SYSTEM_REQUEST), showed "Không có ghi chú phối hợp." where the
                            // send would show the description. Same variable, two meanings, two outputs.
                            //
                            // An item saved without a description is a legacy row or a Host who left the
                            // field blank; say so plainly rather than printing an empty heading.
                            ["logisticsDescription"] = string.IsNullOrWhiteSpace(item.Description)
                                ? "Chưa có mô tả chi tiết."
                                : item.Description!,
                        },
                        TrustedBlocks: new System.Collections.Generic.Dictionary<string, string>
                        {
                            [EmailTrustedBlocks.ActionBlock] =
                                EmailComposition.LogisticsActionBlock(acceptUrl, declineUrl, detailUrl),
                        },
                        RelatedType: EmailActionTargetTypes.LogisticsItem,
                        RelatedId: item.LogisticsItemId,
                        SentBy: actorId)
                    {
                        Content = content,
                        // The department has to coordinate with whoever asked: the Host of this instance,
                        // or the requester when no Host is assigned yet (HOST_THEN_SENDER).
                        ContactScope = new EmailContactScope(
                            VisitInstanceId: instance.VisitInstanceId, CampusId: instance.CampusId),
                    },
                    cancellationToken);

                AttachTo(prepared.SentEmailId, attachInputs, now);

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

                // Accept/decline tokens, pointing at the message the dispatcher just recorded.
                _db.EmailActionTokens.Add(NewToken(_tokens.Hash(acceptRaw), EmailIntendedActions.Accept, groupKey, item.LogisticsItemId, leaderUserId.Value, leaderEmail!, prepared.SentEmailId, prepared.SentEmailRecipientId, now));
                _db.EmailActionTokens.Add(NewToken(_tokens.Hash(declineRaw), EmailIntendedActions.Decline, groupKey, item.LogisticsItemId, leaderUserId.Value, leaderEmail!, prepared.SentEmailId, prepared.SentEmailRecipientId, now));

                await _db.SaveChangesAsync(cancellationToken);
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

        EmailDeliveryResult delivery;
        try
        {
            // Real MIME path (HTML body) with the host's inline images (cid) + file attachments
            // streamed from storage (Google Drive / local) once and reused.
            var outboundAttachments = await OutboundEmailAttachments.LoadAsync(_db, _storage, attachInputs, cancellationToken);
            delivery = await _dispatcher.DeliverAsync(
                prepared! with { Attachments = outboundAttachments }, cancellationToken);
        }
        catch (Exception)
        {
            // The exception text is not recorded: a storage failure can name a path or a signed URL, and
            // this message is shown in the email history.
            delivery = EmailDeliveryResult.Failed(
                "ATTACHMENT_LOAD_FAILED", "Không đọc được tệp đính kèm khi gửi email.");
            await MarkDeliveryFailedAsync(prepared!, delivery.SafeMessage, now, cancellationToken);
        }

        var emailStatus = new SystemEmailDispatchResult(
            delivery, prepared!.SentEmailId, prepared.EmailTemplateId).NotificationStatus;

        var message = delivery.Status switch
        {
            EmailDeliveryStatus.Sent => "Đã gửi yêu cầu hậu cần.",
            EmailDeliveryStatus.Skipped => "Đã tạo yêu cầu hậu cần; email chưa được gửi (SMTP đang tắt).",
            _ => "Đã tạo yêu cầu hậu cần nhưng gửi email thất bại.",
        };

        return new PrepareVisitLogisticsResponse(
            true, true, item.LogisticsItemId, emailStatus, prepared.SentEmailId, message);
    }

    /// <summary>
    /// Turns the optional Host edit into a content mode. Validation and sanitising happen inside
    /// <see cref="SystemEmailContent.AuthoredByUser.Create"/>, which is the only way to build the type.
    /// </summary>
    private async Task<SystemEmailContent> ResolveContentAsync(EmailOverride? ov, CancellationToken ct)
    {
        if (ov is null || !ov.UseEditedContent) return SystemEmailContent.FromTemplate.Instance;

        // Inline images are normalised BEFORE the content is fixed — the normaliser exists for images
        // the Host pasted, and the template body has none.
        var rawHtml = await _normalizer.NormalizeHtmlAsync(EmailComposition.ResolveEditableHtml(ov), ct);
        return SystemEmailContent.AuthoredByUser.Create(ov.Subject, rawHtml, _sanitizer);
    }

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
        PreparedSystemEmail prepared, string? safeMessage, DateTime now, CancellationToken ct)
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

    private static string? FormatMoment(DateTime? value)
        => value?.ToString("HH:mm dd/MM/yyyy");

    private static DateTime? ParseLocal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Unspecified)
            : (DateTime?)null;
    }

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
