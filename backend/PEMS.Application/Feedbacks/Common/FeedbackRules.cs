using PEMS.Domain.Entities.Feedbacks;

namespace PEMS.Application.Feedbacks.Common;

/// <summary>
/// Backend enforcement of the v10 feedback rule (the SQL keeps only chk_feedbacks_flow;
/// target-column mapping is intentionally validated here, not in MySQL CHECK):
/// - VISITOR_OVERALL  (submitter VISITOR): target VISIT_REQUEST | VISIT_INSTANCE, target_user_id NULL.
/// - HOST_PARTICIPANT (submitter HOST):    target VISIT_PARTICIPANT | GUEST_MEMBER | USER.
/// - HOST_LOGISTICS   (submitter HOST):    target LOGISTICS_ITEM | LOGISTICS_HANDOVER | DEPARTMENT | USER.
/// Exactly the target column matching target_type must be set; all other target columns must be NULL.
/// Never fabricate a fake target — if no real row maps, the item must not be submitted at all.
/// </summary>
public static class FeedbackRules
{
    private static readonly Dictionary<string, string[]> AllowedTargetsByType = new()
    {
        [FeedbackTypes.VisitorOverall] = new[] { FeedbackTargetTypes.VisitRequest, FeedbackTargetTypes.VisitInstance },
        [FeedbackTypes.HostParticipant] = new[] { FeedbackTargetTypes.VisitParticipant, FeedbackTargetTypes.GuestMember, FeedbackTargetTypes.User },
        [FeedbackTypes.HostLogistics] = new[] { FeedbackTargetTypes.LogisticsItem, FeedbackTargetTypes.LogisticsHandover, FeedbackTargetTypes.Department, FeedbackTargetTypes.User },
    };

    public static string SubmitterRoleOf(string feedbackType) =>
        feedbackType == FeedbackTypes.VisitorOverall ? FeedbackSubmitterRoles.Visitor : FeedbackSubmitterRoles.Host;

    public static bool IsTargetTypeAllowed(string feedbackType, string targetType) =>
        AllowedTargetsByType.TryGetValue(feedbackType, out var allowed) && allowed.Contains(targetType);

    /// <summary>
    /// Returns an error message when the target id columns don't match target_type
    /// (exactly one id set, on the right column), otherwise null.
    /// </summary>
    public static string? ValidateTargetColumns(
        string targetType,
        ulong? targetUserId,
        ulong? targetParticipantId,
        ulong? targetGuestMemberId,
        ulong? targetLogisticsItemId,
        ulong? targetHandoverId,
        ulong? targetDepartmentId)
    {
        // (column that must be set, its value) per target type; VISIT_REQUEST/VISIT_INSTANCE need none.
        var ids = new (string Type, ulong? Value)[]
        {
            (FeedbackTargetTypes.User, targetUserId),
            (FeedbackTargetTypes.VisitParticipant, targetParticipantId),
            (FeedbackTargetTypes.GuestMember, targetGuestMemberId),
            (FeedbackTargetTypes.LogisticsItem, targetLogisticsItemId),
            (FeedbackTargetTypes.LogisticsHandover, targetHandoverId),
            (FeedbackTargetTypes.Department, targetDepartmentId),
        };

        var isOverallTarget = targetType is FeedbackTargetTypes.VisitRequest or FeedbackTargetTypes.VisitInstance;
        foreach (var (type, value) in ids)
        {
            if (type == targetType)
            {
                if (value is null) return $"Thiếu id đối tượng cho target {targetType}.";
            }
            else if (value is not null)
            {
                return $"Không được set id {type} khi target là {targetType}.";
            }
        }
        if (isOverallTarget && ids.Any(x => x.Value is not null))
            return "Feedback tổng thể không được gắn id đối tượng cụ thể.";
        return null;
    }

    /// <summary>Two feedbacks point at the same business target (same-instance duplicate rule).</summary>
    public static bool SameTarget(Feedback a, string targetType,
        ulong? targetUserId, ulong? targetParticipantId, ulong? targetGuestMemberId,
        ulong? targetLogisticsItemId, ulong? targetHandoverId, ulong? targetDepartmentId) =>
        a.TargetType == targetType
        && a.TargetUserId == targetUserId
        && a.TargetParticipantId == targetParticipantId
        && a.TargetGuestMemberId == targetGuestMemberId
        && a.TargetLogisticsItemId == targetLogisticsItemId
        && a.TargetHandoverId == targetHandoverId
        && a.TargetDepartmentId == targetDepartmentId;
}
