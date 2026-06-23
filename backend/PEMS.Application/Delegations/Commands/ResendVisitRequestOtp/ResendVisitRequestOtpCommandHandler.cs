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
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public ResendVisitRequestOtpCommandHandler(
        IOtpService otpService,
        IEmailService emailService,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _otpService   = otpService;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<MessageResponse> Handle(
        ResendVisitRequestOtpCommand request, CancellationToken cancellationToken)
    {
        var email = request.RegistrantEmail.Trim().ToLowerInvariant();

        // Issue a fresh OTP for the registrant email (old code, if any, is superseded).
        var rawCode = await _otpService.CreateForEmailAsync(
            email,
            OtpPurposes.VisitRequestVerify,
            null,
            null,
            cancellationToken);

        try
        {
            await _emailService.SendVisitRequestOtpAsync(
                email,
                request.RegistrantFullName,
                rawCode,
                cancellationToken);
        }
        catch (Exception)
        {
            throw new PEMS.Application.Common.Exceptions.BusinessRuleException("Không thể gửi mã OTP. Vui lòng thử lại sau.");
        }

        var isEmailEnabled = bool.TryParse(_configuration["Smtp:Enabled"], out var e) && e;
        var msg = isEmailEnabled 
            ? "Mã xác thực mới đã được gửi tới email của bạn."
            : "Hệ thống đang ở chế độ DEV (Smtp:Enabled=false). Mã xác thực mới đã được in ra log của backend.";

        return new MessageResponse(msg);
    }
}
