using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Single source of truth for the "Related Visitor Accounts" tab of a Staff Leader
/// (see UC_StaffLeader_Related_Visitor_Accounts_Tab). A Visitor has no campus, so it is
/// never filtered by <c>primary_campus_id</c>; instead a Visitor is "related" to a campus
/// through the visit-request → visit-request-campus relation.
///
/// Visibility rule (identical for list, nationalities and detail so the three can never drift):
/// a Visitor is related to the campus as soon as a <c>visit_request_campuses</c> row exists for
/// that campus and its request carries a Visitor account. Nothing else is consulted:
/// <list type="bullet">
/// <item>NOT <c>visit_requests.visit_scope</c> — under campus-independent processing each campus
///       owns its own instance, so SINGLE_CAMPUS and MULTI_CAMPUS are handled identically.</item>
/// <item>NOT <c>visit_requests.status</c> and no HO approval/release step — PENDING_APPROVAL,
///       PARTIALLY_APPROVED, APPROVED, REJECTED and CANCELLED all keep the relation.</item>
/// <item>NOT the campus instance status — WAITING_REQUEST_APPROVAL through CLOSED, plus
///       REJECTED/CANCELLED, all count: the campus really did receive that request, and a refusal
///       does not erase the history.</item>
/// </list>
/// The campus is always taken from the authenticated Staff Leader — never from the client.
/// </summary>
internal static class RelatedVisitorScope
{
    /// <summary>
    /// Validates that the caller is an active Staff Leader (STAFF + LEADER) with a campus and
    /// returns that campus id. Any other actor (ADMIN/HO/Staff-Staff/Department/Student/Visitor/
    /// anonymous) is rejected with 403. Status ACTIVE is implied by holding a valid session.
    /// </summary>
    public static ulong EnsureStaffLeaderCampus(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.RoleCode != RoleCodes.Staff
            || currentUser.SubRole != UserSubRoles.Leader
            || currentUser.PrimaryCampusId is null)
        {
            throw new AuthBusinessException(
                AccountErrorCodes.RelatedVisitorForbidden,
                "Chỉ Trưởng phòng (Staff Leader) mới được xem danh sách Visitor liên quan đến cơ sở.",
                403);
        }

        return currentUser.PrimaryCampusId.Value;
    }

    /// <summary>
    /// The campus instances of <paramref name="campusId"/> that make their Visitor related to this
    /// campus. Each row also has a non-null <c>VisitRequest.VisitorUserId</c> (AF-05: requests
    /// without a Visitor account have nothing to show on Account Management).
    ///
    /// This is the ONE predicate list, nationalities and detail all share — a second definition
    /// anywhere would let the three drift apart.
    /// </summary>
    public static IQueryable<VisitRequestCampus> VisibleInstances(IApplicationDbContext db, ulong campusId)
        => db.VisitRequestCampuses.Where(vrc =>
            vrc.CampusId == campusId
            && vrc.VisitRequest.VisitorUserId != null);
}
