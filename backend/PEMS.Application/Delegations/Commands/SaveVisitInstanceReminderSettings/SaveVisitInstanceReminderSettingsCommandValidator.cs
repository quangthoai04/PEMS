using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using PEMS.Domain.Enums;

namespace PEMS.Application.Delegations.Commands.SaveVisitInstanceReminderSettings;

public sealed class SaveVisitInstanceReminderSettingsCommandValidator
    : AbstractValidator<SaveVisitInstanceReminderSettingsCommand>
{
    private static readonly Regex TimeRegex = new(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

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

            item.RuleFor(i => i.DaysBefore)
                .InclusiveBetween(0, 31)
                .WithMessage("Số ngày trước phải nằm trong khoảng 0 đến 31.");

            item.RuleFor(i => i.ReminderTime)
                .Must(t => t != null && TimeRegex.IsMatch(t.Trim()))
                .WithMessage("Giờ gửi phải đúng định dạng HH:mm.");
        });
    }
}
