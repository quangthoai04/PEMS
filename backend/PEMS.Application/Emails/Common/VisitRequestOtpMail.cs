using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Sending the visit-request verification code, shared by the three public entry points that issue one:
/// initiate, resend and human-verification recovery.
///
/// <para>
/// They are identical in what they must do and in how they must fail, so the failure contract lives here
/// rather than being restated three times. A code that could not be delivered is reported to the caller
/// as <c>OTP_SEND_FAILED</c> — the visitor is waiting on that code and telling them "check your inbox"
/// would be a lie. A SKIPPED send is not a failure: with SMTP disabled outside production the code is
/// written to the backend log on purpose, and the response already says so.
/// </para>
/// </summary>
public static class VisitRequestOtpMail
{
    /// <summary>The stable code the public API has always returned when the OTP could not be sent.</summary>
    public const string SendFailedCode = "OTP_SEND_FAILED";

    public static async Task SendAsync(
        ISystemEmailDispatcher dispatcher,
        IOtpService otpService,
        string email,
        string fullName,
        string code,
        CancellationToken cancellationToken)
    {
        SystemEmailDispatchResult result;
        try
        {
            result = await dispatcher.SendAsync(new SystemEmailRequest(
                SystemEmailTemplates.VisitRequestOtp,
                // No display name: this is a public flow and the address is the only thing verified so
                // far — the name on the form has not been confirmed to belong to whoever reads the mail.
                new EmailRecipient(email),
                OtpEmailVariables.For(fullName, code, otpService.VisitRequestCodeMinutes),
                RelatedType: "VisitRequestOtp"), cancellationToken);
        }
        catch (BusinessRuleException)
        {
            // A broken template is a configuration fault, but the visitor cannot act on that distinction
            // and must not be shown it either. Same stable code, no detail.
            throw new BusinessRuleException("Không thể gửi mã OTP. Vui lòng thử lại sau.", SendFailedCode);
        }
        catch (NotFoundException)
        {
            throw new BusinessRuleException("Không thể gửi mã OTP. Vui lòng thử lại sau.", SendFailedCode);
        }
        catch (ConflictException)
        {
            throw new BusinessRuleException("Không thể gửi mã OTP. Vui lòng thử lại sau.", SendFailedCode);
        }

        if (result.Delivery.Status == EmailDeliveryStatus.Failed)
            throw new BusinessRuleException("Không thể gửi mã OTP. Vui lòng thử lại sau.", SendFailedCode);
    }
}
