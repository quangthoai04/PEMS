using System.Linq;
using FluentValidation;

namespace PEMS.Application.Delegations.Commands.PrepareVisitLogistics;

public sealed class PrepareVisitLogisticsCommandValidator : AbstractValidator<PrepareVisitLogisticsCommand>
{
    private static bool IsOffline(PrepareVisitLogisticsCommand c)
        => string.Equals(c.CoordinationMode?.Trim(), LogisticsCoordinationModes.OfflineCoordinated,
            System.StringComparison.OrdinalIgnoreCase);

    public PrepareVisitLogisticsCommandValidator()
    {
        RuleFor(x => x.VisitInstanceId).GreaterThan(0ul);

        RuleFor(x => x.CoordinationMode)
            .Must(m => string.IsNullOrWhiteSpace(m) || LogisticsCoordinationModes.All.Contains(m!.Trim().ToUpperInvariant()))
            .WithMessage("Hình thức xử lý hậu cần không hợp lệ.");

        RuleFor(x => x.DepartmentId)
            .Must(d => d is null || d > 0).WithMessage("Phòng ban không hợp lệ.");

        // SYSTEM_REQUEST must target a department; OFFLINE_COORDINATED may omit it.
        RuleFor(x => x)
            .Must(c => IsOffline(c) || (c.DepartmentId.HasValue && c.DepartmentId.Value > 0))
            .WithMessage("Vui lòng chọn phòng ban xử lý.");

        // OFFLINE_COORDINATED must carry a note (the trace of the offline arrangement).
        RuleFor(x => x)
            .Must(c => !IsOffline(c) || !string.IsNullOrWhiteSpace(c.OfflineCoordinationNote))
            .WithMessage("Vui lòng nhập ghi chú trao đổi bên ngoài.");

        RuleFor(x => x.OfflineCoordinationNote).MaximumLength(5000);

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
