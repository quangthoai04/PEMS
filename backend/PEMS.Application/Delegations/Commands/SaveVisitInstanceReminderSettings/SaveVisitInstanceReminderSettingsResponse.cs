using PEMS.Application.Delegations.Queries.GetVisitInstanceReminderSettings;

namespace PEMS.Application.Delegations.Commands.SaveVisitInstanceReminderSettings;

public sealed class SaveVisitInstanceReminderSettingsResponse
{
    /// <summary>The saved schedule after the upsert (same shape the GET returns).</summary>
    public List<VisitReminderSettingDto> Items { get; set; } = new();
    public string Message { get; set; } = "Đã lưu cấu hình cảnh báo.";
}
