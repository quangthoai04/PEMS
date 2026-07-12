using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using Microsoft.Extensions.Logging;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

public sealed class VerifyAndCreateVisitRequestCommandHandler
    : IRequestHandler<VerifyAndCreateVisitRequestCommand, VerifyAndCreateVisitRequestResponse>
{
    // Two submit intents with the same business fingerprint inside this window are
    // treated as one duplicate submission (unless the old one is REJECTED/CANCELLED).
    private const int DuplicateWindowMinutes = 15;

    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IVisitRequestService _visitRequestService;
    private readonly IUserProvisionService _userProvisionService;
    private readonly IApprovalRoutingService _approvalRouting;
    private readonly IEmailService _emailService;
    private readonly IDateTimeService _clock;
    private readonly ILogger<VerifyAndCreateVisitRequestCommandHandler> _logger;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public VerifyAndCreateVisitRequestCommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        IVisitRequestService visitRequestService,
        IUserProvisionService userProvisionService,
        IApprovalRoutingService approvalRouting,
        IEmailService emailService,
        IDateTimeService clock,
        ILogger<VerifyAndCreateVisitRequestCommandHandler> logger,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db                   = db;
        _otpService           = otpService;
        _visitRequestService  = visitRequestService;
        _userProvisionService = userProvisionService;
        _approvalRouting      = approvalRouting;
        _emailService         = emailService;
        _clock                = clock;
        _logger               = logger;
        _notificationService  = notificationService;
    }

    public async Task<VerifyAndCreateVisitRequestResponse> Handle(
        VerifyAndCreateVisitRequestCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var email = request.RegistrantEmail.Trim().ToLowerInvariant();

        var visitScope = request.VisitScope == VisitScopes.MultiCampus
            ? VisitScopes.MultiCampus
            : VisitScopes.SingleCampus;

        // Contact point is the account holder; falls back to registrant when IsContactSelf.
        var contactEmail = request.IsContactSelf ? email : request.ContactPerson.Email;
        var contactName  = request.IsContactSelf ? request.RegistrantFullName : request.ContactPerson.FullName;
        var contactPhone = request.IsContactSelf ? request.RegistrantPhone : request.ContactPerson.Phone;

        // Server-side fingerprint — the client never decides it.
        var fingerprint = VisitRequestFingerprintBuilder.BuildFromForm(request);

        // ── Idempotency pre-check (BEFORE OTP verify): a retry whose original submit
        //    already committed must be replayed idempotently — its OTP is already
        //    consumed, so verifying again would return a misleading OTP error. ──
        var replay = await CheckIdempotentReplayAsync(request.SubmissionId, email, fingerprint, cancellationToken);
        if (replay is not null)
            return replay;

        // ── Atomic submit: consume OTP → dedupe → provision visitor → insert request +
        //    campuses + guests must all commit together, or nothing at all. The one
        //    deliberate exception: a WRONG code COMMITS its attempt-state update and only
        //    then surfaces the typed error (attempts must survive the failed request). ──
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var committed = false;

        VisitRequest visitRequest;
        try
        {
            // ── 1. Verify OTP challenge. Locks the challenge row (FOR UPDATE) — concurrent
            //       attempts and same-submission retries serialize here. ──
            var otpResult = await _otpService.VerifyChallengeAsync(
                request.SessionToken,
                email,
                OtpPurposes.VisitRequestVerify,
                request.SubmissionId,
                request.OtpCode,
                cancellationToken);

            if (!otpResult.Success)
            {
                // A concurrent/earlier retry of THIS submission may have already created the
                // request and consumed the OTP — replay idempotently instead of erroring.
                var lateReplay = await CheckIdempotentReplayAsync(
                    request.SubmissionId, email, fingerprint, cancellationToken);
                if (lateReplay is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    committed = true;
                    return lateReplay;
                }

                // Persist attempt/cooldown/burn state FIRST, then surface the typed error.
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                throw BuildOtpException(otpResult);
            }

            var otpToken = otpResult.Token!;

            // ── 2. Duplicate guard (business fingerprint): another submit intent with the
            //       same core visit identity committed within the window → consume the OTP
            //       but create NOTHING new (no request/account/children/notifications). ──
            var duplicateWindowStart = now.AddMinutes(-DuplicateWindowMinutes);
            var duplicate = await _db.VisitRequests.AsNoTracking()
                .Where(r => r.BusinessFingerprint == fingerprint
                            && r.SubmittedAt >= duplicateWindowStart
                            && r.Status != VisitRequestStatuses.Rejected
                            && r.Status != VisitRequestStatuses.Cancelled)
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new { r.VisitRequestId, r.RequestCode, r.Status, r.SubmittedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (duplicate is not null)
            {
                otpToken.UsedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                committed = true;

                throw new ConflictException(
                    "Một đơn đăng ký với nội dung tương tự vừa được gửi trước đó. Không có đơn mới nào được tạo.",
                    VisitRequestErrorCodes.DuplicateVisitRequest,
                    new
                    {
                        existingVisitRequestId = duplicate.VisitRequestId,
                        existingRequestCode    = duplicate.RequestCode,
                        existingStatus         = duplicate.Status,
                        existingSubmittedAt    = duplicate.SubmittedAt
                    });
            }

            // ── 3. Rebuild the form payload from the resubmitted command ──────────
            var formData = new VisitRequestFormData(
                request.RegistrantFullName,
                request.RegistrantNationality,
                request.RegistrantOrganization,
                request.RegistrantPosition,
                request.RegistrantPhone,
                email,
                request.DelegationName,
                visitScope,
                request.VisitType,
                request.VisitTypeOther,
                request.CampusVisits,
                request.Purpose,
                request.WorkingContent,
                request.Visitors,
                request.SupportMembers,
                request.ContactPerson,
                request.IsContactSelf,
                request.WorkingLanguage,
                request.TransportationNote,
                request.MediaConsentStatus,
                request.MediaConsentNote,
                request.PartnerId,
                request.Notes);

            // ── 3.5. Transactional routing check: Validate Staff Leader presence for all chosen campuses ──
            var campusCodes = request.CampusVisits.Select(c => c.CampusId).Distinct().ToList();
            var campusIds = await _db.Campuses
                .Where(c => campusCodes.Contains(c.CampusCode))
                .Select(c => c.CampusId)
                .ToListAsync(cancellationToken);

            // For each campus, we need at least one ACTIVE Staff Leader in the IC department
            var validCampuses = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Where(u => u.Role.RoleCode == RoleCodes.Staff
                            && u.SubRole == "LEADER"
                            && u.PrimaryCampusId.HasValue
                            && campusIds.Contains(u.PrimaryCampusId.Value)
                            && u.Status == "ACTIVE"
                            && u.Department != null
                            && u.Department.DepartmentType == "IC")
                .Select(u => u.PrimaryCampusId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (validCampuses.Count < campusIds.Count)
            {
                throw new BusinessRuleException(
                    "Một hoặc nhiều cơ sở bạn chọn hiện tại chưa có Staff Leader (IC) để tiếp nhận đơn. Vui lòng liên hệ FPTU để được hỗ trợ.",
                    VisitRequestErrorCodes.CampusHasNoActiveStaffLeader);
            }

            // ── 4. Provision BOTH accounts inside the same transaction (actor relation):
            //       registrant (submitter, read-only) + contact owner (action owner).
            //       Same normalized email ⇒ one account reused for both FKs. The registrant
            //       email was already re-checked against internal accounts below; the contact
            //       email conflict rules live inside EnsureVisitorAccountAsync. ──
            await _userProvisionService.ValidateRegistrantEmailUsableForPublicFlowAsync(
                email, cancellationToken);

            var registrantUserId = await _userProvisionService.EnsureVisitorAccountAsync(
                email,
                request.RegistrantFullName,
                request.RegistrantPhone,
                now,
                cancellationToken);

            var visitorUserId = string.Equals(email, contactEmail.Trim(), StringComparison.OrdinalIgnoreCase)
                ? registrantUserId
                : await _userProvisionService.EnsureVisitorAccountAsync(
                    contactEmail,
                    contactName,
                    contactPhone,
                    now,
                    cancellationToken);

            // ── 5. Create VisitRequest + child aggregates (campuses, guests) ──────
            visitRequest = await _visitRequestService.CreateAsync(
                formData, visitorUserId, registrantUserId, "VISITOR_SUBMITTED", now, cancellationToken);

            // Submit-intent idempotency + core-identity fingerprint. submission_id has a
            // UNIQUE index — if a concurrent retry of this intent won the race, the insert
            // below throws and is replayed idempotently in the DbUpdateException catch.
            visitRequest.SubmissionId        = request.SubmissionId;
            visitRequest.BusinessFingerprint = fingerprint;

            // ── 6. Approval routing — request decision status only (PENDING_APPROVAL) ──
            visitRequest.Status          = _approvalRouting.DetermineInitialStatus(formData.VisitScope);
            visitRequest.EmailVerifiedAt = now;

            // ── 6.4. Consume the OTP atomically with the request creation. If anything
            //         below fails, the rollback also un-consumes the OTP (it stays usable). ──
            otpToken.UsedAt = now;

            await _db.SaveChangesAsync(cancellationToken);

            // --- 6.5. Send In-App Notifications ---
            // Campus-independent approval: HO no longer approves multi-campus requests, so
            // approval routing is straight to the ACTIVE Staff Leader(s) of each campus. HO is
            // still notified for VISIBILITY (spec §5 HO rule "có đơn liên cơ sở mới") even though
            // HO doesn't act on it.
            {
                var notificationCampusIds = visitRequest.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
                var staffLeaders = await _db.Users
                    .Where(u => u.Role.RoleCode == RoleCodes.Staff
                                && u.SubRole == "LEADER"
                                && u.PrimaryCampusId.HasValue
                                && notificationCampusIds.Contains(u.PrimaryCampusId.Value)
                                && u.Status == "ACTIVE")
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);

                var notifications = staffLeaders.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Có yêu cầu tiếp khách mới",
                    Message: $"{visitRequest.DelegationName} đang chờ xử lý tại cơ sở của bạn. Vui lòng xem chi tiết, duyệt/từ chối và chọn host nếu duyệt.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visitRequest.VisitRequestId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: visitRequest.VisitRequestId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: "/dashboard/visit"
                )).ToList();

                if (visitScope == VisitScopes.MultiCampus)
                {
                    var hoUsers = await _db.Users
                        .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == "ACTIVE")
                        .Select(u => u.UserId)
                        .ToListAsync(cancellationToken);

                    notifications.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: id,
                        Title: "Có đơn liên cơ sở mới",
                        Message: $"{visitRequest.DelegationName} vừa gửi đơn liên cơ sở, đang chờ các cơ sở xử lý.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                        RelatedId: visitRequest.VisitRequestId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        IsActionRequired: false,
                        VisitRequestId: visitRequest.VisitRequestId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: "/dashboard/visit"
                    )));
                }

                await _notificationService.CreateManyAsync(notifications, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        catch (DbUpdateException dbEx) when (!committed)
        {
            // Most likely uq_visit_requests_submission_id: a concurrent retry of the SAME
            // submission intent won the insert race. Roll back, re-query the winner and
            // replay idempotently; a different-content reuse of the key is rejected.
            await transaction.RollbackAsync(cancellationToken);

            var racedReplay = await CheckIdempotentReplayAsync(
                request.SubmissionId, email, fingerprint, cancellationToken);
            if (racedReplay is not null)
                return racedReplay;

            _logger.LogError(dbEx, "UC-17 verify insert failed with DbUpdateException and no idempotent row for submission {SubmissionId}.", request.SubmissionId);
            throw;
        }
        catch
        {
            if (!committed)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── 7. Send confirmation email AFTER commit (fire-and-forget — do not block response) ──
        _ = Task.Run(async () =>
        {
            try
            {
                var minTime = request.CampusVisits.Min(s => s.StartDatetime);
                var maxTime = request.CampusVisits.Max(s => s.EndDatetime);
                var plannedTimeText = minTime.Date == maxTime.Date
                    ? $"{minTime:dd/MM/yyyy} ({minTime:HH:mm} - {maxTime:HH:mm})"
                    : $"{minTime:dd/MM/yyyy HH:mm} - {maxTime:dd/MM/yyyy HH:mm}";

                await _emailService.SendVisitorAccountCreatedOrLinkedEmailAsync(
                    contactEmail,
                    contactName,
                    request.DelegationName,
                    visitRequest.RequestCode,
                    request.VisitScope,
                    plannedTimeText,
                    CancellationToken.None);

                if (!string.Equals(email, contactEmail, StringComparison.OrdinalIgnoreCase))
                {
                    await _emailService.SendRegistrantConfirmationAsync(
                        email,
                        request.RegistrantFullName,
                        contactName,
                        contactEmail,
                        request.DelegationName,
                        visitRequest.RequestCode,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send VISITOR account notification email for visit request {VisitRequestId} to {ContactEmail}",
                    visitRequest.VisitRequestId,
                    contactEmail);
            }
        });

        return new VerifyAndCreateVisitRequestResponse(
            visitRequest.VisitRequestId,
            visitRequest.RequestCode,
            visitRequest.Status,
            "Đơn đăng ký thăm quan đã được gửi thành công và đang chờ phê duyệt.");
    }

    /// <summary>
    /// Same-submission-intent replay handling: if a request with this submissionId already
    /// exists AND its registrant email + fingerprint match, the retry gets the ORIGINAL
    /// result back (HTTP 200, no new row, no new side effects). Same key with different
    /// content is an idempotency-key reuse and is rejected (409).
    /// </summary>
    private async Task<VerifyAndCreateVisitRequestResponse?> CheckIdempotentReplayAsync(
        string submissionId, string normalizedEmail, string fingerprint, CancellationToken cancellationToken)
    {
        var existing = await _db.VisitRequests.AsNoTracking()
            .Where(r => r.SubmissionId == submissionId)
            .Select(r => new { r.VisitRequestId, r.RequestCode, r.Status, r.RegistrantEmail, r.BusinessFingerprint })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
            return null;

        if (!string.Equals(existing.RegistrantEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.BusinessFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Phiên gửi đơn này đã được dùng cho một nội dung khác. Vui lòng tải lại trang và gửi lại.",
                VisitRequestErrorCodes.IdempotencyKeyReused);
        }

        return new VerifyAndCreateVisitRequestResponse(
            existing.VisitRequestId,
            existing.RequestCode,
            existing.Status,
            "Đơn đăng ký này đã được ghi nhận trước đó. Không có đơn mới nào được tạo.");
    }

    /// <summary>Maps a failed challenge verification to the typed error contract.</summary>
    private static OtpChallengeException BuildOtpException(OtpChallengeVerification result)
    {
        var (status, message) = result.ErrorCode switch
        {
            OtpErrorCodes.Invalid                   => (400, "Mã OTP không đúng. Vui lòng kiểm tra lại."),
            OtpErrorCodes.Expired                   => (400, "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới."),
            OtpErrorCodes.NotFound                  => (400, "Không tìm thấy phiên xác thực. Vui lòng yêu cầu mã mới."),
            OtpErrorCodes.SessionInvalid            => (400, "Phiên xác thực không còn hiệu lực. Vui lòng yêu cầu mã mới."),
            OtpErrorCodes.RetryLater                => (429, "Bạn thao tác quá nhanh. Vui lòng chờ trước khi thử lại."),
            OtpErrorCodes.ResendTooSoon             => (429, "Temporarily unable to issue another verification code."),
            OtpErrorCodes.StandardRateLimited       => (429, "Temporarily unable to issue another verification code."),
            OtpErrorCodes.RecoveryRateLimited       => (429, "Temporarily unable to issue another verification code."),
            OtpErrorCodes.AbsoluteRateLimited       => (429, "Temporarily unable to issue another verification code."),
            OtpErrorCodes.HumanVerificationRequired => (428, "Bạn đã nhập sai quá nhiều lần. Vui lòng xác minh bạn không phải robot để nhận mã mới."),
            _                                       => (400, "Xác thực OTP thất bại.")
        };

        return new OtpChallengeException(
            status,
            result.ErrorCode ?? OtpErrorCodes.SessionInvalid,
            message,
            remainingAttempts: result.RemainingAttempts,
            retryAfterSeconds: result.RetryAfterSeconds > 0 ? result.RetryAfterSeconds : null,
            retryAtUtc: result.RetryAtUtc,
            humanVerificationRequired: result.HumanVerificationRequired);
    }
}
