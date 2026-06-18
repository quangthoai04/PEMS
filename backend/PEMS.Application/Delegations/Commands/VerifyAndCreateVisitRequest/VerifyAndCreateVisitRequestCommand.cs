using MediatR;

namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

/// <summary>
/// Step 2 of UC-17: verifies the OTP, creates the VisitRequest, provisions the
/// Visitor account, routes to the correct approval queue, and sends confirmation.
/// </summary>
public sealed record VerifyAndCreateVisitRequestCommand(
    string SessionToken,
    string OtpCode
) : IRequest<VerifyAndCreateVisitRequestResponse>;
