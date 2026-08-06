using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.News;

/// <summary>
/// Access rules for visit-instance news (logic duyệt mới — tất cả bài đều qua Staff Leader):
///  • View list: Host / Staff Leader of campus / HO see EVERY post of the instance; an accepted
///    participant sees ONLY their own posts; a Visitor owner sees ONLY published posts.
///  • Create: ONLY the Host or an accepted IC-Staff / Student participant, from the AFTER_VISIT
///    stage onward (news is written after the reception; a PUBLISHED post — or news_not_required —
///    is a close condition, so writing must open BEFORE close and stays open after). Department
///    participants do NOT write news; Visitor / HO / Staff Leader never write.
///  • Edit / resubmit: the AUTHOR only — enforced in the command handlers.
///  • Approve / reject: Staff Leader of the campus only — via the News review UC.
/// (There can be MANY news posts per instance — one per author.)
/// </summary>
internal static class VisitNewsAccess
{
    internal readonly record struct VisitNewsActor(
        bool InScope,
        bool CanCreate,
        bool IsHost,
        bool IsStaffLeaderOfCampus,
        bool IsHo,
        bool IsGuestSide,
        bool IsAcceptedParticipant);

    public static VisitNewsActor Evaluate(
        VisitRequestCampus instance, VisitRequest visit, ICurrentUserService user, string? acceptedParticipantRole)
    {
        var userId = user.UserId!.Value;
        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = user.RoleCode == RoleCodes.Staff
            && string.Equals(user.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && user.PrimaryCampusId == instance.CampusId;
        bool isHo = user.RoleCode == RoleCodes.Ho;
        bool isGuestSide = VisitRequestOwnership.IsGuestSide(visit, instance, userId);
        bool isAccepted = acceptedParticipantRole != null;

        bool inScope = isHost || isStaffLeaderOfCampus || isHo || isGuestSide || isAccepted;

        // Writing window: from AFTER_VISIT (news is a close condition) and still allowed after
        // CLOSED (late posts are fine); never on a cancelled instance/request.
        bool inWritingWindow = (instance.Status == VisitInstanceStatus.AfterVisit
                             || instance.Status == VisitInstanceStatus.Closed)
            && instance.Status != VisitInstanceStatus.Cancelled
            && visit.Status != VisitRequestStatuses.Cancelled;

        // Writers: Host, or accepted IC-Staff / Student participant. NOT Department, Visitor, HO, Staff Leader.
        bool canCreate = (isHost
            || (isAccepted && (acceptedParticipantRole == ParticipantRoles.IcSupport
                               || acceptedParticipantRole == ParticipantRoles.Student)))
            && inWritingWindow;

        return new VisitNewsActor(inScope, canCreate, isHost, isStaffLeaderOfCampus, isHo, isGuestSide, isAccepted);
    }
}
