using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// What ONE actor may read of a request's UC-32 change history, resolved once and answered from one
/// place.
///
/// <para>
/// History visibility is NARROWER than detail visibility, deliberately. Being invited to support a
/// campus — as IC support, department support or a student — shows that campus's detail and grants
/// nothing here: the change history says who altered what and when, which is a record of the request's
/// administration rather than of the day being prepared. Widening it to every participant so a
/// frontend error would go away is exactly the change this type exists to prevent.
/// </para>
///
/// <para>
/// It is shared because the read model that decides whether to OFFER the section and the two handlers
/// that SERVE it must answer the same question. They previously carried three copies of this decision;
/// the copy in the read model did not exist at all, so the detail page mounted a section the endpoint
/// then refused with 403.
/// </para>
/// </summary>
/// <param name="VisibleInstanceIds">Campuses whose history this actor may read — possibly a subset.</param>
/// <param name="IncludeRequestLevel">Request-wide events (creation, resubmit, request cancellation).</param>
/// <param name="IncludeIdentity">The masked operational-contact identity timeline.</param>
public readonly record struct VisitHistoryScope(
    IReadOnlyList<ulong> VisibleInstanceIds,
    bool IncludeRequestLevel,
    bool IncludeIdentity)
{
    /// <summary>
    /// Whether this actor may open the history AT ALL. False is the same verdict the endpoints turn
    /// into a 403, so a capability built from it can never promise a section the API will refuse.
    /// </summary>
    public bool CanViewHistory => VisibleInstanceIds.Count > 0 || IncludeRequestLevel;

    /// <summary>Nobody: no campus, no request-level events, no identity timeline.</summary>
    public static VisitHistoryScope None { get; } =
        new(Array.Empty<ulong>(), IncludeRequestLevel: false, IncludeIdentity: false);
}

/// <summary>
/// The single place that answers "what of this request's history may this actor read".
///
/// The scope, unchanged from the rules the timeline has always applied:
/// <list type="bullet">
/// <item><b>Registrant / HO</b> — every campus, the request-level events, and the masked identity
/// timeline.</item>
/// <item><b>Operational contact</b> — only the campuses they hold, with those campuses' identity
/// events. No request-level events: those describe the request as a whole, which is not theirs.</item>
/// <item><b>Staff Leader</b> — only their primary campus's instances, no identity timeline.</item>
/// <item><b>Current Host</b> — only the instances they host, no identity timeline.</item>
/// <item><b>Anyone else</b>, supporting participants included — nothing.</item>
/// </list>
/// </summary>
public static class VisitHistoryVisibility
{
    /// <summary>
    /// Resolves the actor's history scope. <paramref name="visit"/> must have been loaded with its
    /// <c>CampusInstances</c> — every branch below is decided from that collection.
    /// </summary>
    public static VisitHistoryScope Resolve(VisitRequest visit, ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
            return VisitHistoryScope.None;

        // The registrant owns the request, so they see every campus's history plus the request-level
        // events. An operational contact sees the history of the campuses THEY hold and no
        // request-level events.
        var isRegistrant = VisitRequestOwnership.IsRegistrant(visit, actorId);
        var isHo = currentUser.RoleCode == RoleCodes.Ho;
        if (isRegistrant || isHo)
            return new VisitHistoryScope(
                visit.CampusInstances.Select(c => c.VisitInstanceId).ToList(),
                IncludeRequestLevel: true,
                IncludeIdentity: true);

        var operatedInstanceIds = VisitRequestOwnership.OperatedCampuses(visit, actorId)
            .Select(c => c.VisitInstanceId).ToList();
        if (operatedInstanceIds.Count > 0)
            return new VisitHistoryScope(
                operatedInstanceIds, IncludeRequestLevel: false, IncludeIdentity: true);

        // Checked BEFORE the Host branch, as it always has been: a leader who also hosts a campus
        // outside their own reads their own campus, not both.
        if (currentUser.RoleCode == RoleCodes.Staff && currentUser.SubRole == UserSubRoles.Leader
            && currentUser.PrimaryCampusId is { } campusId)
            return new VisitHistoryScope(
                visit.CampusInstances.Where(c => c.CampusId == campusId)
                    .Select(c => c.VisitInstanceId).ToList(),
                IncludeRequestLevel: false,
                IncludeIdentity: false);

        // Current Host of one or more campuses. Everybody else lands here with an empty list, which is
        // the refusal: a supporting participant hosts nothing.
        return new VisitHistoryScope(
            visit.CampusInstances.Where(c => c.CurrentHostUserId == actorId)
                .Select(c => c.VisitInstanceId).ToList(),
            IncludeRequestLevel: false,
            IncludeIdentity: false);
    }
}
