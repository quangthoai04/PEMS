using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Application.Delegations.News;

/// <summary>
/// Access rules for visit-instance news (UC tin tức):
///  • View: any user in scope of the campus instance. Internal roles (Host / Staff Leader of
///    campus / HO / accepted participant) see every post; a Visitor owner sees ONLY published posts.
///  • Create / edit: ONLY the Host or an accepted IC-Staff / Student participant, while the visit
///    is live. Department participants do NOT write news (chốt rule §11.2); Visitor / HO / Staff
///    Leader never write — they only view.
/// (Unlike minutes, there can be MANY news posts per instance.)
/// </summary>
internal static class VisitNewsAccess
{
    public static (bool InScope, bool CanCreate, bool CanSeeUnpublished) Evaluate(
        VisitRequestCampus instance, VisitRequest visit, ICurrentUserService user, string? acceptedParticipantRole)
    {
        var userId = user.UserId!.Value;
        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = user.RoleCode == RoleCodes.Staff
            && string.Equals(user.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && user.PrimaryCampusId == instance.CampusId;
        bool isHo = user.RoleCode == RoleCodes.Ho;
        bool isVisitorOwner = user.RoleCode == RoleCodes.Visitor && visit.VisitorUserId == userId;
        bool isAccepted = acceptedParticipantRole != null;

        bool inScope = isHost || isStaffLeaderOfCampus || isHo || isVisitorOwner || isAccepted;

        bool isLive = instance.Status != VisitInstanceStatus.Closed
            && instance.Status != VisitInstanceStatus.Cancelled
            && visit.Status != VisitRequestStatuses.Cancelled;

        // Writers: Host, or accepted IC-Staff / Student participant. NOT Department, Visitor, HO, Staff Leader.
        bool canCreate = (isHost
            || (isAccepted && (acceptedParticipantRole == ParticipantRoles.IcSupport
                               || acceptedParticipantRole == ParticipantRoles.Student)))
            && isLive;

        // Everyone in scope except a pure Visitor owner may see unpublished (draft/pending/rejected) posts.
        bool canSeeUnpublished = inScope && !(isVisitorOwner && !isHost && !isAccepted);

        return (inScope, canCreate, canSeeUnpublished);
    }
}
