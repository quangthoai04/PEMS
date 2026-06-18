namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

/// <summary>
/// Returned by <see cref="InitiateVisitRequestCommand"/>.
/// The frontend stores <c>SessionToken</c> and presents the OTP input UI.
/// </summary>
public sealed record InitiateVisitRequestResponse(
    string SessionToken,
    string Message,
    string MaskedEmail);
