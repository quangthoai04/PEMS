using FluentValidation;

namespace PEMS.Application.Delegations.VisitDocuments.Commands.UploadVisitDocument;

public sealed class UploadVisitDocumentCommandValidator : AbstractValidator<UploadVisitDocumentCommand>
{
    public const int MaxPartnersPerDocument = 10;

    public UploadVisitDocumentCommandValidator()
    {
        RuleFor(c => c.VisitInstanceId).GreaterThan(0UL);
        RuleFor(c => c.Content).NotEmpty().WithMessage("Vui lòng chọn tài liệu / hình ảnh để tải lên.");
        RuleFor(c => c.FileName).NotEmpty().WithMessage("Thiếu tên tệp tin.");
        RuleFor(c => c.Title).NotEmpty().WithMessage("Vui lòng nhập tên tài liệu.").MaximumLength(255);
        RuleFor(c => c.PartnerNames)
            .Must(names => names == null || names.Count <= MaxPartnersPerDocument)
            .WithMessage($"Chỉ được gắn tối đa {MaxPartnersPerDocument} đối tác cho một tài liệu.");
    }
}
