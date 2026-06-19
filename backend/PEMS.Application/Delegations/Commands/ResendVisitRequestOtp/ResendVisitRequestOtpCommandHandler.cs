using MediatR;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

public sealed class ResendVisitRequestOtpCommandHandler
    : IRequestHandler<ResendVisitRequestOtpCommand, MessageResponse>
{
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public ResendVisitRequestOtpCommandHandler(
        IOtpService otpService,
        IEmailService emailService)
    {
        _otpService   = otpService;
        _emailService = emailService;
    }

    public async Task<MessageResponse> Handle(
        ResendVisitRequestOtpCommand request, CancellationToken cancellationToken)
    {
        var email = request.RegisterEmail.Trim().ToLowerInvariant();

        // Issue a fresh OTP for the registrant email (old code, if any, is superseded).
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

        return new MessageResponse("Mã xác thực mới đã được gửi tới email của bạn.");
    }
}
