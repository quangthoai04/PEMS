using FluentValidation;

namespace PEMS.Application.Delegations.VisitPhotos.Commands.RemoveVisitPhoto;

public sealed class RemoveVisitPhotoCommandValidator : AbstractValidator<RemoveVisitPhotoCommand>
{
    public RemoveVisitPhotoCommandValidator()
    {
        RuleFor(c => c.VisitPhotoId).GreaterThan(0UL);
        RuleFor(c => c.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Vui lòng nhập lý do xóa ảnh.")
            .MaximumLength(500).WithMessage("Lý do xóa tối đa 500 ký tự.");
    }
}
