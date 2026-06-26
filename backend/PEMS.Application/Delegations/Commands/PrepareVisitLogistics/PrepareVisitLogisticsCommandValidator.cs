using System.Linq;
using FluentValidation;

namespace PEMS.Application.Delegations.Commands.PrepareVisitLogistics;

public sealed class PrepareVisitLogisticsCommandValidator : AbstractValidator<PrepareVisitLogisticsCommand>
{
    public PrepareVisitLogisticsCommandValidator()
    {
        RuleFor(x => x.VisitInstanceId).GreaterThan(0ul);
        RuleFor(x => x.DepartmentId).GreaterThan(0ul);

        RuleFor(x => x.ItemType)
            .Must(t => t != null && LogisticsItemTypes.All.Contains(t.Trim().ToUpperInvariant()))
            .WithMessage("Loại hạng mục hậu cần không hợp lệ.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề yêu cầu không được để trống.")
            .MaximumLength(255);

        RuleFor(x => x.Quantity)
            .Must(q => q is null || q >= 1).WithMessage("Số lượng phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.Priority)
            .Must(p => string.IsNullOrWhiteSpace(p) || LogisticsPriorities.All.Contains(p!.Trim().ToUpperInvariant()))
            .WithMessage("Mức ưu tiên không hợp lệ.");
    }
}
