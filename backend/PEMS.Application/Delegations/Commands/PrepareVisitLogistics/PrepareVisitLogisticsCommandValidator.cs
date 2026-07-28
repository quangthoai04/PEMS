using System.Linq;
using FluentValidation;

using PEMS.Application.Common;
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
            .NotNull()
            .When(x => !IsOffline(x) && (x.ItemType == "ROOM" || x.ItemType == "TRANSPORT" || x.ItemType == "MEAL" || x.ItemType == "EQUIPMENT"))
            .WithMessage("LOGISTICS_QUANTITY_REQUIRED")
            .Must(q => q is null || q >= 1)
            .WithMessage("LOGISTICS_QUANTITY_INVALID");

        RuleFor(x => x.UsageStartAt)
            .NotNull()
            .When(x => !IsOffline(x))
            .WithMessage("LOGISTICS_USAGE_TIME_REQUIRED")
            .Must(s => string.Compare(s, VietnamTime.Now().AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm")) >= 0)
            .When(x => !string.IsNullOrEmpty(x.UsageStartAt) && !IsOffline(x))
            .WithMessage("LOGISTICS_USAGE_START_IN_PAST");

        RuleFor(x => x.UsageEndAt)
            .NotNull()
            .When(x => !IsOffline(x))
            .WithMessage("LOGISTICS_USAGE_TIME_REQUIRED")
            .Must((x, end) => string.Compare(end, x.UsageStartAt) > 0)
            .When(x => !string.IsNullOrEmpty(x.UsageStartAt) && !string.IsNullOrEmpty(x.UsageEndAt) && !IsOffline(x))
            .WithMessage("LOGISTICS_USAGE_END_BEFORE_START");
    }
}
