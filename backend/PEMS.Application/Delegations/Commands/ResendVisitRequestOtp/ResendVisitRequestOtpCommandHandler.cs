using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Delegations.Commands.VisitRequestOtp;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

public sealed class ResendVisitRequestOtpCommandHandler
    : IRequestHandler<ResendVisitRequestOtpCommand, InitiateVisitRequestResponse>
{
    private readonly IOtpService _otpService;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IRequestMetadataService _requestMetadata;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public ResendVisitRequestOtpCommandHandler(
        IOtpService otpService,
        ISystemEmailDispatcher dispatcher,
        IRequestMetadataService requestMetadata,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _otpService   = otpService;
        _dispatcher = dispatcher;
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

        // The address comes from the stored challenge row, never from the request body — resending must
        // not become a way to redirect somebody else's code.
        await VisitRequestOtpMail.SendAsync(
            _dispatcher, _otpService, issue.Email, request.RegistrantFullName, issue.Code, cancellationToken);

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
