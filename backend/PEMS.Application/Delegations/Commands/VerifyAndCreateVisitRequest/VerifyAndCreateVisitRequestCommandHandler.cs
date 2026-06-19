using MediatR;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

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
                "expired"       => "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.",
                "max_attempts"  => "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu mã mới.",
                "mismatch"      => "Mã OTP không đúng. Vui lòng kiểm tra lại.",
                "no_active_token" => "Không tìm thấy mã OTP. Vui lòng yêu cầu mã mới.",
                _               => "Xác thực OTP thất bại."
            });
        }

        // ── 2. Rebuild the form payload from the resubmitted command ──────────
        var formData = new VisitRequestFormData(
            request.RegisterFullName,
            request.RegisterNationality,
            request.RegisterOrganization,
            request.RegisterJobTitle,
            request.RegisterPhone,
            email,
            request.DelegationName,
            request.VisitScope,
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

        // ── 3. Provision Visitor account ──────────────────────────────────────
        //      Contact point is the account holder; falls back to registrant when IsContactSelf.
        var contactEmail = formData.IsContactSelf
            ? formData.RegisterEmail
            : formData.ContactPoint.Email;

        var contactName = formData.IsContactSelf
            ? formData.RegisterFullName
            : formData.ContactPoint.FullName;

        var contactPhone = formData.IsContactSelf
            ? formData.RegisterPhone
            : formData.ContactPoint.Phone;

        var visitorUserId = await _userProvisionService.EnsureVisitorAccountAsync(
            contactEmail,
            contactName,
            contactPhone,
            now,
            cancellationToken);

        // ── 5. Create VisitRequest + child aggregates ─────────────────────────
        var visitRequest = await _visitRequestService.CreateAsync(formData, visitorUserId, now, cancellationToken);

        // ── 6. Approval routing ───────────────────────────────────────────────
        visitRequest.Status          = _approvalRouting.DetermineInitialStatus(formData.VisitScope);
        visitRequest.EmailVerifiedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        // ── 7. Send confirmation email (fire-and-forget — do not block response) ──
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
