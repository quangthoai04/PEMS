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

        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Thiếu mã phiên gửi đơn.")
            .Must(BeUuid).WithMessage("Mã phiên gửi đơn không hợp lệ.");
    }

    private static bool BeUuid(string value) => Guid.TryParse(value, out _);
}
