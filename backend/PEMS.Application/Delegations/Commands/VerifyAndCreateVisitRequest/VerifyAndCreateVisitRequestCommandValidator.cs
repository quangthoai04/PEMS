using FluentValidation;

namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

/// <summary>
/// Re-validates the FULL form server-side at the create boundary (the OTP step never
/// trusts that the form was valid earlier) using the shared rule set, plus the OTP code.
/// Campus existence/ACTIVE, planned-time-not-in-past and duplicate checks are business
/// validation handled in the handler/service (they need the database).
/// </summary>
public sealed class VerifyAndCreateVisitRequestCommandValidator
    : AbstractValidator<VerifyAndCreateVisitRequestCommand>
{
    public VerifyAndCreateVisitRequestCommandValidator()
    {
        this.ApplyVisitRequestFormRules();

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Mã OTP không được để trống.")
            .Length(6).WithMessage("Mã OTP phải gồm 6 chữ số.")
            .Matches(@"^\d{6}$").WithMessage("Mã OTP chỉ được chứa chữ số.");

        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Thiếu mã phiên gửi đơn.")
            .Must(BeUuid).WithMessage("Mã phiên gửi đơn không hợp lệ.");

        RuleFor(x => x.SessionToken)
            .NotEmpty().WithMessage("Thiếu phiên xác thực OTP.");
    }

    private static bool BeUuid(string value) => Guid.TryParse(value, out _);
}
