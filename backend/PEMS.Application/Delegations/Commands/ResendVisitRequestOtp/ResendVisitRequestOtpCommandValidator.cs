using FluentValidation;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

public sealed class ResendVisitRequestOtpCommandValidator
    : AbstractValidator<ResendVisitRequestOtpCommand>
{
    public ResendVisitRequestOtpCommandValidator()
    {
        RuleFor(x => x.RegistrantEmail)
            .NotEmpty().WithMessage("Email đăng ký không được để trống.")
            .EmailAddress().WithMessage("Email đăng ký không hợp lệ.");

        RuleFor(x => x.RegistrantFullName)
            .NotEmpty().WithMessage("Họ và tên người đăng ký không được để trống.");

        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Thiếu mã phiên gửi đơn.")
            .Must(BeUuid).WithMessage("Mã phiên gửi đơn không hợp lệ.");

        RuleFor(x => x.SessionToken)
            .NotEmpty().WithMessage("Thiếu phiên xác thực OTP.");
    }

    private static bool BeUuid(string value) => Guid.TryParse(value, out _);
}
