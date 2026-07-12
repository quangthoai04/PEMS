using FluentValidation;

namespace PEMS.Application.Delegations.Commands.RecoverVisitRequestOtp;

public sealed class RecoverVisitRequestOtpCommandValidator
    : AbstractValidator<RecoverVisitRequestOtpCommand>
{
    public RecoverVisitRequestOtpCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Thiếu mã phiên gửi đơn.")
            .Must(BeUuid).WithMessage("Mã phiên gửi đơn không hợp lệ.");

        RuleFor(x => x.SessionToken)
            .NotEmpty().WithMessage("Thiếu phiên xác thực OTP.");

        RuleFor(x => x.HumanVerificationToken)
            .NotEmpty().WithMessage("Thiếu mã xác minh người thật.");

        RuleFor(x => x.RegistrantFullName)
            .NotEmpty().WithMessage("Họ và tên người đăng ký không được để trống.");
    }

    private static bool BeUuid(string value) => Guid.TryParse(value, out _);
}
