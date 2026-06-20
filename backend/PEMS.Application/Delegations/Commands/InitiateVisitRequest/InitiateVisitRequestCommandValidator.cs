using FluentValidation;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

/// <summary>
/// Validates the full visit-request form at the send-OTP step using the shared rule set,
/// so an invalid form is rejected before any OTP email is sent. The same rules run again
/// at the VerifyAndCreate step (the create boundary).
/// </summary>
public sealed class InitiateVisitRequestCommandValidator : AbstractValidator<InitiateVisitRequestCommand>
{
    public InitiateVisitRequestCommandValidator()
    {
        this.ApplyVisitRequestFormRules();
    }
}
