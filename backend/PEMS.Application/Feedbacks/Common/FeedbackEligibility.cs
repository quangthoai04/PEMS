using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Feedbacks.Common;

/// <summary>
/// Who may rate a campus visit instance and when.
/// - Visitor (visit_requests.visitor_user_id) rates the overall visit.
/// - Host (visit_request_campuses.current_host_user_id) rates participants + logistics providers.
/// Both only from AFTER_VISIT onward — rating opens when the Host confirms the reception has
/// ended ("Xác nhận kết thúc tiếp khách"), never while the visit is still DURING_VISIT.
/// </summary>
public static class FeedbackEligibility
{
    public static readonly string[] EligibleInstanceStatuses =
    {
        VisitInstanceStatuses.AfterVisit,
        VisitInstanceStatuses.Closed,
    };

    public static bool IsInstanceStatusEligible(string status) =>
        EligibleInstanceStatuses.Contains(status);

    /// <summary>VISITOR / HOST for the current user on this instance, or null when neither.</summary>
    public static string? ActorTypeOf(ulong userId, VisitRequest request, VisitRequestCampus instance)
    {
        if (instance.CurrentHostUserId == userId) return FeedbackSubmitterRoles.Host;
        if (request.VisitorUserId == userId) return FeedbackSubmitterRoles.Visitor;
        return null;
    }
}
