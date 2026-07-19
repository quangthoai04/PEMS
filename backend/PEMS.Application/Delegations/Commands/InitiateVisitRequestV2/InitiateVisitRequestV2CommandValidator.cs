using FluentValidation;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequestV2;

/// <summary>
/// Structural validation for public v2 initiate — REUSES the exact same <see cref="VisitRequestFormDataV2Validator"/>
/// as authenticated create-v2 (plan §5: one canonical rule set, no duplicated business rules, and never the v1
/// validator with its 3-hour / mandatory-support rules). Runs in the MediatR <c>ValidationBehaviour</c> BEFORE the
/// handler so a malformed form never mints an OTP. DB/clock-dependent rules (campus ACTIVE, not-in-the-past,
/// partner) are authoritatively re-checked by the create service at verify inside the transaction.
/// </summary>
public sealed class InitiateVisitRequestV2CommandValidator : AbstractValidator<InitiateVisitRequestV2Command>
{
    public InitiateVisitRequestV2CommandValidator()
    {
        RuleFor(x => x.Form).NotNull().WithMessage("Thiếu dữ liệu biểu mẫu.");
        RuleFor(x => x.Form).SetValidator(new VisitRequestFormDataV2Validator()!).When(x => x.Form is not null);
    }
}
