using System.Linq;
using FluentValidation;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.AgendaTemplates.Commands.CreateAgendaTemplate;

public sealed class CreateAgendaTemplateCommandValidator : AbstractValidator<CreateAgendaTemplateCommand>
{
    public CreateAgendaTemplateCommandValidator()
    {
        RuleFor(x => x.VisitType)
            .Must(VisitTypes.IsValid).WithMessage("Loại hình visit không hợp lệ.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên mẫu agenda là bắt buộc.")
            .MaximumLength(150);
        RuleFor(x => x.Status)
            .Must(s => AgendaTemplateStatuses.All.Contains(s)).WithMessage("Trạng thái mẫu agenda không hợp lệ.");
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Mẫu agenda phải có ít nhất một mục lịch trình.");
        RuleForEach(x => x.Items).SetValidator(new AgendaTemplateItemInputValidator());
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.DisplayOrder).Distinct().Count() == items.Count)
            .WithMessage("Thứ tự hiển thị (displayOrder) không được trùng trong cùng một mẫu.")
            .When(x => x.Items is { Count: > 0 });
    }
}
