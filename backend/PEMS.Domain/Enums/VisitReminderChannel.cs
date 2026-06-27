namespace PEMS.Domain.Enums;

/// <summary>Delivery channel for a scheduled visit reminder. Names map 1:1 to the SQL ENUM strings.</summary>
public enum VisitReminderChannel
{
    IN_APP,
    EMAIL,
}
