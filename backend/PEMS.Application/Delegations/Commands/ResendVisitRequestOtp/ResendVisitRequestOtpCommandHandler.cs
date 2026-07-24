using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.VisitRequestOtp;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

public sealed class ResendVisitRequestOtpCommandHandler
    : IRequestHandler<ResendVisitRequestOtpCommand, InitiateVisitRequestResponse>
{
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IRequestMetadataService _requestMetadata;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public ResendVisitRequestOtpCommandHandler(
        IOtpService otpService,
        IEmailService emailService,
        IRequestMetadataService requestMetadata,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _otpService   = otpService;
        _emailService = emailService;
        _requestMetadata = requestMetadata;
        _configuration = configuration;
    }

    public async Task<InitiateVisitRequestResponse> Handle(
        ResendVisitRequestOtpCommand request, CancellationToken cancellationToken)
    {
        // Supersedes the old challenge and issues a fresh one bound to the SAME submission
        // intent. The email is taken from the stored challenge row, never from the body.
        var issue = await _otpService.ResendChallengeAsync(
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
            throw new PEMS.Application.Common.Exceptions.BusinessRuleException("Không thể gửi mã OTP. Vui lòng thử lại sau.", "OTP_SEND_FAILED");
        }

        var isEmailEnabled = bool.TryParse(_configuration["Smtp:Enabled"], out var e) && e;
        var msg = isEmailEnabled
            ? "Mã xác thực mới đã được gửi tới email của bạn."
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
