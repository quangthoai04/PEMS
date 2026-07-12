using MediatR;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;

namespace PEMS.Application.Delegations.Commands.RecoverVisitRequestOtp;

/// <summary>
/// Human-verification recovery after the OTP challenge was burned by too many wrong
/// codes. Validates the Turnstile token SERVER-SIDE; on success the old challenge is
/// permanently invalidated and a brand-new challenge (attempt count 0,
/// <c>issue_reason = HUMAN_RECOVERY</c>) is issued for the SAME submission intent.
/// CAPTCHA success never re-opens the old code.
/// </summary>
public sealed record RecoverVisitRequestOtpCommand(
    string SubmissionId,
    string SessionToken,
    string HumanVerificationToken,
    string RegistrantFullName
) : IRequest<InitiateVisitRequestResponse>;
