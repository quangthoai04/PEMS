namespace PEMS.Domain.Enums;

/// <summary>Audience of a scheduled visit reminder. Names map 1:1 to the SQL ENUM strings.</summary>
public enum VisitReminderTargetGroup
{
    HOST,
    PARTICIPANTS,
    HOST_AND_PARTICIPANTS,
}
