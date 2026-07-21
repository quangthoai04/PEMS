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
    private readonly IRequestMetadataService _requestMetadata;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public InitiateVisitRequestCommandHandler(
        IOtpService otpService,
        IEmailService emailService,
        IUserProvisionService userProvisionService,
        IRequestMetadataService requestMetadata,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _otpService  = otpService;
        _emailService = emailService;
        _userProvisionService = userProvisionService;
        _requestMetadata = requestMetadata;
        _configuration = configuration;
    }

    public async Task<InitiateVisitRequestResponse> Handle(
        InitiateVisitRequestCommand request, CancellationToken cancellationToken)
    {
        var email = request.RegistrantEmail.Trim().ToLowerInvariant();

        // ── Fail fast (actor relation): the PUBLIC registrant email also becomes/links a
        //    VISITOR account at verify time. An internal-account email must never enter the
        //    public OTP flow — reject BEFORE any OTP is sent. ──
        await _userProvisionService.ValidateRegistrantEmailUsableForPublicFlowAsync(
            email, cancellationToken);

        // ── Fail fast: the contact email is what becomes the VISITOR account. If it already
        //    belongs to a non-VISITOR (or inactive VISITOR) account, reject BEFORE sending an
        //    OTP so the registrant can fix the contact email up front. ──
        var contactEmail = request.IsContactSelf ? email : request.ContactPerson.Email;
        await _userProvisionService.ValidateContactEmailCanBeUsedForVisitorAsync(
            contactEmail, cancellationToken);

        // The form draft stays on the frontend (sessionStorage) and is resubmitted at the
        // verify step. Here we only create the OTP challenge (otp_tokens) and email the code.
        // The challenge is bound to email + purpose + submissionId; IP/User-Agent are taken
        // server-side, never from the body.
        var issue = await _otpService.CreateChallengeAsync(
            email,
            OtpPurposes.VisitRequestVerify,
            request.SubmissionId,
            OtpIssueReasons.Initial,
            _requestMetadata.IpAddress,
            _requestMetadata.UserAgent,
            cancellationToken);

        try
        {
            await _emailService.SendVisitRequestOtpAsync(
                email,
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
            ? "Mã xác thực đã được gửi tới email của bạn. Vui lòng kiểm tra hộp thư."
            : "Hệ thống đang ở chế độ DEV (Smtp:Enabled=false). Mã xác thực đã được in ra log của backend.";

        return new InitiateVisitRequestResponse(
            SessionToken:       issue.SessionToken,
            Message:            msg,
            MaskedEmail:        MaskEmail(email),
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
