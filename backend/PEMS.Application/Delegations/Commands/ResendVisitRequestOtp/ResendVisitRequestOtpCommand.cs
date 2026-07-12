using MediatR;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

/// <summary>
/// Resends the OTP for an in-flight visit-request registration. The old challenge
/// (identified by <see cref="SessionToken"/>) is superseded and a NEW session token is
/// returned — the frontend must replace the old one. Keeps the same
/// <see cref="SubmissionId"/> (one submit intent). A challenge that already requires
/// human verification cannot be resent (428) — resend never bypasses the CAPTCHA.
/// </summary>
public sealed record ResendVisitRequestOtpCommand(
    string RegistrantEmail,
    string RegistrantFullName,
    string SubmissionId,
    string SessionToken
) : IRequest<InitiateVisitRequestResponse>;
