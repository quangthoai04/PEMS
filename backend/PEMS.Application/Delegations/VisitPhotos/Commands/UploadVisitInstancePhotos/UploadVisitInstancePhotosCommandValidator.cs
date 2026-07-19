using FluentValidation;

namespace PEMS.Application.Delegations.VisitPhotos.Commands.UploadVisitInstancePhotos;

public sealed class UploadVisitInstancePhotosCommandValidator
    : AbstractValidator<UploadVisitInstancePhotosCommand>
{
    public const int MaxFilesPerUpload = 10;

    public UploadVisitInstancePhotosCommandValidator()
    {
        RuleFor(c => c.VisitInstanceId).GreaterThan(0UL);
        RuleFor(c => c.Files)
            .NotEmpty().WithMessage("Vui lòng chọn ít nhất một ảnh để tải lên.")
            .Must(f => f.Count <= MaxFilesPerUpload)
            .WithMessage($"Chỉ được tải lên tối đa {MaxFilesPerUpload} ảnh mỗi lần.");
    }
}
