using FluentValidation;

namespace PEMS.Application.Galleries.Commands.ChangeGalleryLocationStatus;

/// <summary>Input validation for UC-LOC-08/09. The ACTIVE/INACTIVE check + scope live in the handler.</summary>
public sealed class ChangeGalleryLocationStatusCommandValidator : AbstractValidator<ChangeGalleryLocationStatusCommand>
{
    public ChangeGalleryLocationStatusCommandValidator()
    {
        RuleFor(x => x.LocationId)
            .GreaterThan(0).WithMessage("Không tìm thấy vị trí Gallery.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái không hợp lệ.");
    }
}
