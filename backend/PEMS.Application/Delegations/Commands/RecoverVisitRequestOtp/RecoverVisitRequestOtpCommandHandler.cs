using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.RecoverVisitRequestOtp;

public sealed class RecoverVisitRequestOtpCommandHandler
    : IRequestHandler<RecoverVisitRequestOtpCommand, InitiateVisitRequestResponse>
{
    private readonly IOtpService _otpService;
    private readonly IHumanVerificationService _humanVerification;
    private readonly IEmailService _emailService;
    private readonly IRequestMetadataService _requestMetadata;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public RecoverVisitRequestOtpCommandHandler(
        IOtpService otpService,
        IHumanVerificationService humanVerification,
        IEmailService emailService,
        IRequestMetadataService requestMetadata,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _otpService        = otpService;
        _humanVerification = humanVerification;
        _emailService      = emailService;
        _requestMetadata   = requestMetadata;
        _configuration     = configuration;
    }

    public async Task<InitiateVisitRequestResponse> Handle(
        RecoverVisitRequestOtpCommand request, CancellationToken cancellationToken)
    {
        // 1. Server-side human verification FIRST — no CAPTCHA, no new code.
        //    The remote IP is derived server-side, never taken from the body.
        var verification = await _humanVerification.VerifyAsync(
            request.HumanVerificationToken,
            _requestMetadata.IpAddress,
            cancellationToken);

        if (!verification.Success)
        {
            throw new OtpChallengeException(
                400,
                OtpErrorCodes.HumanVerificationFailed,
                "Xác minh không thành công. Vui lòng thử lại.",
                humanVerificationRequired: true);
        }

        // 2. Invalidate the old challenge and issue a fresh one (attempts = 0,
        //    issue_reason = HUMAN_RECOVERY) atomically. Recovery quota is enforced inside.
        var issue = await _otpService.RecoverChallengeAsync(
            request.SessionToken,
            OtpPurposes.VisitRequestVerify,
            request.SubmissionId,
            _requestMetadata.IpAddress,
            _requestMetadata.UserAgent,
            cancellationToken);

        try
        {
            await _emailService.SendVisitRequestOtpAsync(
                issue.Email,
                request.RegistrantFullName,
                issue.Code,
                cancellationToken);
        }
        catch (Exception)
        {
            throw new BusinessRuleException("Không thể gửi mã OTP. Vui lòng thử lại sau.", "OTP_SEND_FAILED");
        }

        var isEmailEnabled = bool.TryParse(_configuration["Smtp:Enabled"], out var e) && e;
        var msg = isEmailEnabled
            ? "Xác minh thành công. Mã xác thực mới đã được gửi tới email của bạn."
            : "Hệ thống đang ở chế độ DEV (Smtp:Enabled=false). Mã xác thực mới đã được in ra log của backend.";

        return new InitiateVisitRequestResponse(
            SessionToken:       issue.SessionToken,
            Message:            msg,
            MaskedEmail:        MaskEmail(issue.Email),
            ExpiresAt:          issue.ExpiresAt,
            ResendAfterSeconds: issue.ResendAfterSeconds,
            MaxAttempts:        issue.MaxAttempts);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        return email[..2] + new string('*', Math.Max(0, at - 2)) + email[at..];
    }
}
