using FluentValidation;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

public sealed class ResendVisitRequestOtpCommandValidator
    : AbstractValidator<ResendVisitRequestOtpCommand>
{
    public ResendVisitRequestOtpCommandValidator()
    {
        RuleFor(x => x.SessionToken)
            .NotEmpty().WithMessage("SessionToken không được để trống.");
    }
}
