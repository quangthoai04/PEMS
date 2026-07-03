namespace PEMS.Application.Feedbacks.Common;

/// <summary>
/// Canonical ENUM values of the v10 `feedbacks` table (feedback_rule_fixed).
/// Mirrors the MySQL definitions — do not invent values outside these sets.
/// </summary>
public static class FeedbackTypes
{
    public const string VisitorOverall = "VISITOR_OVERALL";
    public const string HostParticipant = "HOST_PARTICIPANT";
    public const string HostLogistics = "HOST_LOGISTICS";

    public static readonly string[] All = { VisitorOverall, HostParticipant, HostLogistics };
}

public static class FeedbackSubmitterRoles
{
    public const string Visitor = "VISITOR";
    public const string Host = "HOST";
}

public static class FeedbackTargetTypes
{
    public const string VisitRequest = "VISIT_REQUEST";
    public const string VisitInstance = "VISIT_INSTANCE";
    public const string VisitParticipant = "VISIT_PARTICIPANT";
    public const string GuestMember = "GUEST_MEMBER";
    public const string LogisticsItem = "LOGISTICS_ITEM";
    public const string LogisticsHandover = "LOGISTICS_HANDOVER";
    public const string User = "USER";
    public const string Department = "DEPARTMENT";

    public static readonly string[] All =
    {
        VisitRequest, VisitInstance, VisitParticipant, GuestMember,
        LogisticsItem, LogisticsHandover, User, Department,
    };
}
