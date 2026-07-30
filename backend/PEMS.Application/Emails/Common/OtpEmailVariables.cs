using System.Collections.Generic;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The variables the two OTP templates declare — <c>AUTH_PASSWORD_RESET_OTP</c> and
/// <c>VISIT_REQUEST_OTP</c> — built in one place so four call sites cannot disagree about them.
///
/// <para>
/// The lifetime is passed in rather than looked up here because the two flows use different settings
/// (<c>Otp:CodeMinutes</c> for a password reset, the shorter <c>Otp:VisitRequestCodeMinutes</c> for a
/// public visit request). Reading it from <see cref="IOtpService"/> instead of writing a number into
/// the template is what keeps the sentence "the code is valid for N minutes" true when the setting
/// changes.
/// </para>
/// <para>
/// The code is a credential: it appears here as the <c>otpCode</c> variable and nowhere else. That name
/// is classified in <see cref="SensitiveEmailVariables"/>, which is what stops the body being stored and
/// what refuses a subject that would interpolate it.
/// </para>
/// </summary>
public static class OtpEmailVariables
{
    public static Dictionary<string, string> For(string fullName, string code, int expireMinutes)
        => new()
        {
            ["fullName"] = fullName,
            ["otpCode"] = code,
            ["expireMinutes"] = expireMinutes.ToString(),
        };
}
