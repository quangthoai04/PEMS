using FluentValidation;

namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

public sealed class VerifyAndCreateVisitRequestCommandValidator
    : AbstractValidator<VerifyAndCreateVisitRequestCommand>
{
    public VerifyAndCreateVisitRequestCommandValidator()
    {
        RuleFor(x => x.SessionToken)
            .NotEmpty().WithMessage("SessionToken không được để trống.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Mã OTP không được để trống.")
            .Length(6).WithMessage("Mã OTP phải gồm 6 chữ số.")
            .Matches(@"^\d{6}$").WithMessage("Mã OTP chỉ được chứa chữ số.");
    }
}
