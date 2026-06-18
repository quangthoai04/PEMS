using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

        // ── 1. Load pending session ───────────────────────────────────────────
        var pending = await _db.PendingVisitRequests
            .FirstOrDefaultAsync(p => p.PendingId == request.SessionToken, cancellationToken)
            ?? throw new NotFoundException("Phiên đăng ký không tồn tại. Vui lòng điền lại form.");

        if (pending.ExpiresAt <= now)
            throw new BusinessRuleException("Phiên đăng ký đã hết hạn. Vui lòng điền lại form và gửi lại.");

        // ── 2. Verify OTP ─────────────────────────────────────────────────────
        var otpResult = await _otpService.VerifyAsync(
            pending.Email,
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

        // ── 3. Deserialise form data ──────────────────────────────────────────
        var formData = JsonSerializer.Deserialize<PendingVisitRequestFormData>(pending.FormDataJson, _json)
            ?? throw new BusinessRuleException("Không thể đọc dữ liệu form. Vui lòng thử lại.");

        // ── 4. Provision Visitor account ──────────────────────────────────────
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

        // ── 7. Remove pending session ─────────────────────────────────────────
        _db.PendingVisitRequests.Remove(pending);
        await _db.SaveChangesAsync(cancellationToken);

        // ── 8. Send confirmation email (fire-and-forget — do not block response) ──
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
