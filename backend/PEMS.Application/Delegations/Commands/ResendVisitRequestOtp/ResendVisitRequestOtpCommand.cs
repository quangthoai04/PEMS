using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

/// <summary>
/// Resends the OTP for an in-flight visit-request session.
/// The <c>SessionToken</c> returned by <c>InitiateVisitRequestCommand</c> is required.
/// </summary>
public sealed record ResendVisitRequestOtpCommand(
    string SessionToken
) : IRequest<MessageResponse>;
