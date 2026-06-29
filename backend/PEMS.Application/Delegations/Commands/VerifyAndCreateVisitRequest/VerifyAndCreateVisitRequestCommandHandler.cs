using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using Microsoft.Extensions.Logging;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

public sealed class VerifyAndCreateVisitRequestCommandHandler
    : IRequestHandler<VerifyAndCreateVisitRequestCommand, VerifyAndCreateVisitRequestResponse>
{
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

        // ── Atomic submit: consume OTP → dedupe → provision visitor → insert request +
        //    campuses + guests must all commit together, or nothing at all. ──
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        VisitRequest visitRequest;
        try
        {
            // ── 1. Verify OTP (no server-side draft in v8.3 — form is resubmitted by client) ──
            var otpResult = await _otpService.VerifyAsync(
                email,
                OtpPurposes.VisitRequestVerify,
                request.OtpCode,
                cancellationToken);

            if (!otpResult.Success)
            {
                throw new BusinessRuleException(otpResult.FailureReason switch
                {
                    "expired"         => "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.",
                    "max_attempts"    => "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu mã mới.",
                    "mismatch"        => "Mã OTP không đúng. Vui lòng kiểm tra lại.",
                    "no_active_token" => "Không tìm thấy mã OTP. Vui lòng yêu cầu mã mới.",
                    _                 => "Xác thực OTP thất bại."
                });
            }

            // ── 2. Duplicate guard: same registrant + delegation + scope submitted very
            //       recently and not already rejected/cancelled → reject as a double-submit. ──
            var duplicateWindowStart = now.AddMinutes(-10);
            var isDuplicate = await _db.VisitRequests.AsNoTracking().AnyAsync(r =>
                r.RegistrantEmail == email &&
                r.DelegationName == request.DelegationName &&
                r.VisitScope == visitScope &&
                r.SubmittedAt >= duplicateWindowStart &&
                r.Status != VisitRequestStatuses.Rejected &&
                r.Status != VisitRequestStatuses.Cancelled,
                cancellationToken);

            if (isDuplicate)
                throw new ConflictException(
                    "Một đơn đăng ký tương tự vừa được gửi. Vui lòng kiểm tra email xác nhận trước khi gửi lại.",
                    VisitRequestErrorCodes.DuplicateVisitRequest);

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
                request.TransportationType,
                request.TransportationDetail,
                request.MediaConsentStatus,
                request.MediaConsentNote,
                request.PartnerId,
                request.Notes);

            // ── 4. Provision Visitor account (links existing or creates a new VISITOR) ──
            var visitorUserId = await _userProvisionService.EnsureVisitorAccountAsync(
                contactEmail,
                contactName,
                contactPhone,
                now,
                cancellationToken);

            // ── 5. Create VisitRequest + child aggregates (campuses, guests) ──────
            visitRequest = await _visitRequestService.CreateAsync(formData, visitorUserId, "VISITOR_SUBMITTED", now, cancellationToken);

            // ── 6. Approval routing — request decision status only (PENDING_APPROVAL) ──
            visitRequest.Status          = _approvalRouting.DetermineInitialStatus(formData.VisitScope);
            visitRequest.EmailVerifiedAt = now;

            await _db.SaveChangesAsync(cancellationToken);

            // --- 6.5. Send In-App Notifications ---
            if (visitRequest.VisitScope == VisitScopes.MultiCampus)
            {
                var hoUsers = await _db.Users
                    .Where(u => u.Role.RoleCode == "HO" && u.Status == "ACTIVE")
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);
                    
                var notifications = hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationItem(
                    id,
                    "Có yêu cầu liên cơ sở mới",
                    "Yêu cầu tiếp khách liên cơ sở đang chờ HO duyệt.",
                    PEMS.Application.Notifications.Common.NotificationTypes.CrossCampusRequestSubmitted,
                    PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    visitRequest.VisitRequestId
                ));
                await _notificationService.CreateManyAsync(notifications, cancellationToken);
            }
            else
            {
                var campusIds = visitRequest.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
                var staffLeaders = await _db.Users
                    .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == "LEADER" && u.PrimaryCampusId.HasValue && campusIds.Contains(u.PrimaryCampusId.Value) && u.Status == "ACTIVE")
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);
                    
                var notifications = staffLeaders.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationItem(
                    id,
                    "Có yêu cầu tiếp khách mới",
                    $"{visitRequest.DelegationName} đang chờ duyệt tại cơ sở của bạn.",
                    PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    visitRequest.VisitRequestId
                ));
                await _notificationService.CreateManyAsync(notifications, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
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
}
