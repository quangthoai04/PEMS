using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Campuses.Commands.ManageCampusStatus;

public sealed class ManageCampusStatusCommandValidator : AbstractValidator<ManageCampusStatusCommand>
{
    public ManageCampusStatusCommandValidator()
    {
        RuleFor(x => x.CampusId)
            .GreaterThan(0ul).WithMessage("Campus không hợp lệ.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái là bắt buộc.")
            .Must(s => s == EntityStatuses.Active || s == EntityStatuses.Inactive)
            .WithMessage("Trạng thái chỉ có thể là ACTIVE hoặc INACTIVE.");
    }
}
