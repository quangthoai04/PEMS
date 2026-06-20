using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
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

    public VerifyAndCreateVisitRequestCommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        IVisitRequestService visitRequestService,
        IUserProvisionService userProvisionService,
        IApprovalRoutingService approvalRouting,
        IEmailService emailService,
        IDateTimeService clock)
    {
        _db                   = db;
        _otpService           = otpService;
        _visitRequestService  = visitRequestService;
        _userProvisionService = userProvisionService;
        _approvalRouting      = approvalRouting;
        _emailService         = emailService;
        _clock                = clock;
    }

    public async Task<VerifyAndCreateVisitRequestResponse> Handle(
        VerifyAndCreateVisitRequestCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var email = request.RegisterEmail.Trim().ToLowerInvariant();

        var visitScope = request.VisitScope == VisitScopes.MultiCampus
            ? VisitScopes.MultiCampus
            : VisitScopes.SingleCampus;

        // Contact point is the account holder; falls back to registrant when IsContactSelf.
        var contactEmail = request.IsContactSelf ? email : request.ContactPoint.Email;
        var contactName  = request.IsContactSelf ? request.RegisterFullName : request.ContactPoint.FullName;
        var contactPhone = request.IsContactSelf ? request.RegisterPhone : request.ContactPoint.Phone;

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
                request.RegisterFullName,
                request.RegisterNationality,
                request.RegisterOrganization,
                request.RegisterJobTitle,
                request.RegisterPhone,
                email,
                request.DelegationName,
                visitScope,
                request.VisitSlots,
                request.Purpose,
                request.WorkingContent,
                request.Visitors,
                request.SupportTeam,
                request.ContactPoint,
                request.IsContactSelf,
                request.Language,
                request.Vehicle,
                request.Notes);

            // ── 4. Provision Visitor account (links existing or creates a new VISITOR) ──
            var visitorUserId = await _userProvisionService.EnsureVisitorAccountAsync(
                contactEmail,
                contactName,
                contactPhone,
                now,
                cancellationToken);

            // ── 5. Create VisitRequest + child aggregates (campuses, guests) ──────
            visitRequest = await _visitRequestService.CreateAsync(formData, visitorUserId, now, cancellationToken);

            // ── 6. Approval routing — request decision status only (PENDING_APPROVAL) ──
            visitRequest.Status          = _approvalRouting.DetermineInitialStatus(formData.VisitScope);
            visitRequest.EmailVerifiedAt = now;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── 7. Send confirmation email AFTER commit (fire-and-forget — do not block response) ──
        _ = _emailService.SendVisitRequestConfirmationAsync(
            contactEmail,
            contactName,
            visitRequest.RequestCode,
            contactEmail,
            CancellationToken.None);

        return new VerifyAndCreateVisitRequestResponse(
            visitRequest.VisitRequestId,
            visitRequest.RequestCode,
            visitRequest.Status,
            "Đơn đăng ký thăm quan đã được gửi thành công và đang chờ phê duyệt.");
    }
}
