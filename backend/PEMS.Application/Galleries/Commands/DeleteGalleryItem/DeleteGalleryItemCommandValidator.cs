using FluentValidation;

namespace PEMS.Application.Galleries.Commands.DeleteGalleryItem;

public sealed class DeleteGalleryItemCommandValidator : AbstractValidator<DeleteGalleryItemCommand>
{
    public DeleteGalleryItemCommandValidator()
    {
        RuleFor(x => x.GalleryItemId).GreaterThan(0);
    }
}
