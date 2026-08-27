using System.Linq;
using FluentValidation;
using PEMS.Domain.Enums;

namespace PEMS.Application.Delegations.Commands.SaveVisitInstanceReminderSettings;

public sealed class SaveVisitInstanceReminderSettingsCommandValidator
    : AbstractValidator<SaveVisitInstanceReminderSettingsCommand>
{
    /// <summary>Same ceiling the old days_before field allowed (31 days), expressed in minutes.</summary>
    private const int MaxOffsetMinutes = 31 * 24 * 60;

    public SaveVisitInstanceReminderSettingsCommandValidator()
    {
        RuleFor(x => x.VisitInstanceId).GreaterThan(0ul);

        RuleFor(x => x.Items).NotNull();

        // No duplicate (channel, target_group) pairs in one payload — the DB unique key would reject
        // them and the meaning is ambiguous.
        RuleFor(x => x.Items)
            .Must(items => items == null || items
                .GroupBy(i => (i.Channel?.Trim().ToUpperInvariant(), i.TargetGroup?.Trim().ToUpperInvariant()))
                .All(g => g.Count() == 1))
            .WithMessage("Trùng cấu hình cảnh báo cho cùng một kênh và nhóm người nhận.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Channel)
                .Must(c => System.Enum.TryParse<VisitReminderChannel>((c ?? string.Empty).Trim(), out _))
                .WithMessage("Kênh cảnh báo không hợp lệ (IN_APP hoặc EMAIL).");

            item.RuleFor(i => i.TargetGroup)
                .Must(t => System.Enum.TryParse<VisitReminderTargetGroup>((t ?? string.Empty).Trim(), out _))
                .WithMessage("Nhóm người nhận không hợp lệ.");

            // HOST_AND_PARTICIPANTS is a legacy value the current UI (2 cards: Người phụ trách /
            // Thành phần tham gia) never offers and never sends — it stays in the enum only so the
            // dispatch service and historical rows keep resolving correctly. Refusing it here closes
            // the door on a NEW row that could overlap a HOST or PARTICIPANTS row's recipients and
            // double-notify the Host. Existing legacy rows are migrated by a separate data patch, not
            // rejected retroactively — this rule only stops new writes.
            item.RuleFor(i => i.TargetGroup)
                .Must(t => !string.Equals((t ?? string.Empty).Trim(), nameof(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS),
                    System.StringComparison.OrdinalIgnoreCase))
                .WithMessage("Nhóm người nhận HOST_AND_PARTICIPANTS không còn được hỗ trợ trong cấu hình mới.");

            item.RuleFor(i => i.OffsetMinutes)
                .InclusiveBetween(1, MaxOffsetMinutes)
                .WithMessage($"Thời gian nhắc trước phải từ 1 phút đến {MaxOffsetMinutes / (24 * 60)} ngày.");
        });
    }
}
