namespace PEMS.Domain.Enums;

/// <summary>Lifecycle of a scheduled visit reminder. Names map 1:1 to the SQL ENUM strings.</summary>
public enum VisitReminderStatus
{
    PENDING,
    SENT,
    CANCELLED,
    FAILED,
}
