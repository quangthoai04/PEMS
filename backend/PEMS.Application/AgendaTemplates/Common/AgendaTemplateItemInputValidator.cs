using FluentValidation;

namespace PEMS.Application.AgendaTemplates.Common;

/// <summary>Per-item rules shared by create/update template commands.</summary>
public sealed class AgendaTemplateItemInputValidator : AbstractValidator<AgendaTemplateItemInput>
{
    public AgendaTemplateItemInputValidator()
    {
        RuleFor(i => i.Title)
            .NotEmpty().WithMessage("Tiêu đề mục lịch trình là bắt buộc.")
            .MaximumLength(255);
        RuleFor(i => i.DurationMinutes)
            .GreaterThan(0).WithMessage("Thời lượng (phút) phải lớn hơn 0.");
        RuleFor(i => i.StartOffsetMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Offset bắt đầu (phút) không được âm.");
        RuleFor(i => i.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị không được âm.");
        RuleFor(i => i.Location).MaximumLength(255);
        RuleFor(i => i.ResponsibleRoleLabel).MaximumLength(150);
    }
}
