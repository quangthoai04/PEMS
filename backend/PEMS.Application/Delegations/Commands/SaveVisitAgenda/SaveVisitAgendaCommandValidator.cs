using FluentValidation;

namespace PEMS.Application.Delegations.Commands.SaveVisitAgenda;

public sealed class SaveVisitAgendaCommandValidator
    : AbstractValidator<SaveVisitAgendaCommand>
{
    public SaveVisitAgendaCommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0ul).WithMessage("VisitRequestId không hợp lệ.");
        RuleFor(x => x.VisitInstanceId).GreaterThan(0ul).WithMessage("VisitInstanceId không hợp lệ.");
        RuleFor(x => x.Items).NotNull().WithMessage("Danh sách lịch trình không hợp lệ.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Title)
                .NotEmpty().WithMessage("Nội dung mục lịch trình không được để trống.")
                .MaximumLength(255).WithMessage("Nội dung mục lịch trình quá dài.");
            item.RuleFor(i => i.Location)
                .MaximumLength(255).WithMessage("Địa điểm quá dài.");
            item.RuleFor(i => i.EndTime)
                .Must((i, end) => end == null || end > i.StartTime)
                .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");
        });
    }
}
