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
/// Visibility rule (identical for list, count and detail so the three can never drift):
/// <list type="bullet">
/// <item>SINGLE_CAMPUS: the Visitor is visible to the campus the request was sent to,
///       in ANY request status (the Visitor ↔ campus relation already exists even when the
///       request was rejected/cancelled).</item>
/// <item>MULTI_CAMPUS: visible only AFTER HO has released the request — i.e.
///       <c>status IN (APPROVED, CANCELLED)</c> and the campus instance is no longer
///       <c>WAITING_REQUEST_APPROVAL</c>. PENDING_APPROVAL / REJECTED stay hidden.</item>
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
    /// The campus instances of <paramref name="campusId"/> that make their Visitor visible to
    /// the Staff Leader, applying the single-/multi-campus release rule above. Each row also has a
    /// non-null <c>VisitRequest.VisitorUserId</c> (AF-05: requests without an account are skipped).
    /// </summary>
    public static IQueryable<VisitRequestCampus> VisibleInstances(IApplicationDbContext db, ulong campusId)
        => db.VisitRequestCampuses.Where(vrc =>
            vrc.CampusId == campusId
            && vrc.VisitRequest.VisitorUserId != null
            && (
                vrc.VisitRequest.VisitScope == VisitScopes.SingleCampus
                || (
                    vrc.VisitRequest.VisitScope == VisitScopes.MultiCampus
                    && (vrc.VisitRequest.Status == VisitRequestStatuses.Approved
                        || vrc.VisitRequest.Status == VisitRequestStatuses.Cancelled)
                    && vrc.Status != VisitInstanceStatuses.WaitingRequestApproval
                )
            ));
}
