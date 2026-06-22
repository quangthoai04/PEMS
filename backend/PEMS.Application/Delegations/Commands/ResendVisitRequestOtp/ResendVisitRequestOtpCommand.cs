using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

/// <summary>
/// Resends the OTP for an in-flight visit-request registration. SQL v8.3 has no
/// pending_visit_requests table, so the registrant email/name are resubmitted by the
/// frontend (it still holds the draft in sessionStorage).
/// </summary>
public sealed record ResendVisitRequestOtpCommand(
    string RegistrantEmail,
    string RegistrantFullName
) : IRequest<MessageResponse>;
