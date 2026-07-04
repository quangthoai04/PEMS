using FluentValidation;

namespace PEMS.Application.Partners.Documents.Commands.UploadPartnerDocument;

public sealed class UploadPartnerDocumentCommandValidator : AbstractValidator<UploadPartnerDocumentCommand>
{
    public UploadPartnerDocumentCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0UL);
        RuleFor(x => x.FileId).GreaterThan(0UL).WithMessage("Thiếu file đính kèm.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề tài liệu là bắt buộc.")
            .MaximumLength(255);
        RuleFor(x => x.DocumentCategory).MaximumLength(100);
    }
}
