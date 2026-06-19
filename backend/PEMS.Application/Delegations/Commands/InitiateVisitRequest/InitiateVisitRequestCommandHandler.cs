using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

public sealed class InitiateVisitRequestCommandHandler
    : IRequestHandler<InitiateVisitRequestCommand, InitiateVisitRequestResponse>
{
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public InitiateVisitRequestCommandHandler(
        IOtpService otpService,
        IEmailService emailService)
    {
        _otpService  = otpService;
        _emailService = emailService;
    }

    public async Task<InitiateVisitRequestResponse> Handle(
        InitiateVisitRequestCommand request, CancellationToken cancellationToken)
    {
        var email = request.RegisterEmail.Trim().ToLowerInvariant();

        // SQL v8.3 has no pending_visit_requests table. The form draft stays on the
        // frontend (sessionStorage) and is resubmitted at the verify step. Here we only
        // create the OTP (stored in otp_tokens) and email it — nothing is persisted yet.
        var rawCode = await _otpService.CreateForEmailAsync(
            email,
            OtpPurposes.VisitRequestVerify,
            null,
            null,
            cancellationToken);

        await _emailService.SendVisitRequestOtpAsync(
            email,
            request.RegisterFullName,
            rawCode,
            cancellationToken);

        return new InitiateVisitRequestResponse(
            SessionToken: email,
            Message:      "Mã xác thực đã được gửi tới email của bạn. Vui lòng kiểm tra hộp thư.",
            MaskedEmail:  MaskEmail(email));
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        return email[..2] + new string('*', Math.Max(0, at - 2)) + email[at..];
    }
}
