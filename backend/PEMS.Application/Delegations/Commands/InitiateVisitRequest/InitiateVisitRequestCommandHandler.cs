using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

public sealed class InitiateVisitRequestCommandHandler
    : IRequestHandler<InitiateVisitRequestCommand, InitiateVisitRequestResponse>
{
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IUserProvisionService _userProvisionService;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public InitiateVisitRequestCommandHandler(
        IOtpService otpService,
        IEmailService emailService,
        IUserProvisionService userProvisionService,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _otpService  = otpService;
        _emailService = emailService;
        _userProvisionService = userProvisionService;
        _configuration = configuration;
    }

    public async Task<InitiateVisitRequestResponse> Handle(
        InitiateVisitRequestCommand request, CancellationToken cancellationToken)
    {
        var email = request.RegistrantEmail.Trim().ToLowerInvariant();

        // ── Fail fast: the contact email is what becomes the VISITOR account. If it already
        //    belongs to a non-VISITOR (or inactive VISITOR) account, reject BEFORE sending an
        //    OTP so the registrant can fix the contact email up front. ──
        var contactEmail = request.IsContactSelf ? email : request.ContactPerson.Email;
        await _userProvisionService.ValidateContactEmailCanBeUsedForVisitorAsync(
            contactEmail, cancellationToken);

        // SQL v8.3 has no pending_visit_requests table. The form draft stays on the
        // frontend (sessionStorage) and is resubmitted at the verify step. Here we only
        // create the OTP (stored in otp_tokens) and email it — nothing is persisted yet.
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
        catch (Exception ex)
        {
            var details = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            throw new PEMS.Application.Common.Exceptions.BusinessRuleException($"Không thể gửi mã OTP. Lỗi: {ex.Message} - {details}");
        }

        var isEmailEnabled = bool.TryParse(_configuration["Smtp:Enabled"], out var e) && e;
        var msg = isEmailEnabled 
            ? "Mã xác thực đã được gửi tới email của bạn. Vui lòng kiểm tra hộp thư."
            : "Hệ thống đang ở chế độ DEV (Smtp:Enabled=false). Mã xác thực đã được in ra log của backend.";

        return new InitiateVisitRequestResponse(
            SessionToken: email,
            Message:      msg,
            MaskedEmail:  MaskEmail(email));
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        return email[..2] + new string('*', Math.Max(0, at - 2)) + email[at..];
    }
}
