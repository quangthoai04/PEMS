namespace PEMS.Application.Common.Interfaces;

/// <summary>Abstraction over the system clock to keep handlers testable.</summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }

    /// <summary>Current Vietnam wall-clock time (Asia/Ho_Chi_Minh). Use this when comparing against
    /// DATETIME columns that store wall-clock values (e.g. visit_instance_reminder_settings.scheduled_at).</summary>
    DateTime VietnamNow { get; }
}
