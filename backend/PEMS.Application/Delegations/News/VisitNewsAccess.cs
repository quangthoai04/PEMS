using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.News;

/// <summary>
/// THE answer to "may this person write the news for this visit". One evaluator, used by every path
/// that asks: the process page's list, the Create-News eligible query, the process permission flags,
/// and the create command itself.
///
/// <para>
/// It is one type because it was four, and they disagreed. The list only asked about the writing
/// window and the writer's role; the eligible query added <c>news_not_required</c> and media consent
/// but dropped the role restriction; the create command applied the two gates and also dropped the
/// role restriction; the process permissions used a fourth combination with no writing window at all.
/// The visible result was a screen offering "Tạo bài tin tức" and a Create-News page answering
/// "chuyến tiếp khách này chưa đủ điều kiện" — two verdicts about one user and one visit — plus a
/// DEPT_SUPPORT participant who could reach the create endpoint that the button correctly hid.
/// </para>
///
/// <para>Other news rules, unchanged and enforced where they already live:</para>
/// <list type="bullet">
/// <item>View list — Host / campus Staff Leader / HO see every post; an accepted participant sees
/// only their own; a Visitor owner sees only published ones.</item>
/// <item>Edit / resubmit — the AUTHOR only (command handlers).</item>
/// <item>Approve / reject — the campus's Staff Leader, through the News review UC.</item>
/// </list>
/// </summary>
internal static class VisitNewsAccess
{
    /// <summary>
    /// Why creating is refused. Stable codes, so the Create-News page can say the ONE true reason
    /// instead of the guess it used to print ("chưa vào giai đoạn Sau tiếp khách, không yêu cầu tin
    /// tức, hoặc bạn không phải Host/người tham gia") — a sentence that is three-quarters wrong
    /// whichever cause applies.
    /// </summary>
    internal static class ReasonCodes
    {
        public const string NotInScope = "NEWS_VISIT_NOT_IN_SCOPE";
        public const string NotInWritingWindow = "NEWS_VISIT_NOT_IN_WRITING_WINDOW";
        public const string ParticipantRoleNotAllowed = "NEWS_VISIT_PARTICIPANT_ROLE_NOT_ALLOWED";
        public const string NewsNotRequired = "NEWS_VISIT_NOT_REQUIRED";
        public const string MediaConsentDenied = "NEWS_VISIT_MEDIA_CONSENT_DENIED";
        public const string AlreadyExists = "NEWS_ALREADY_EXISTS_FOR_VISIT_INSTANCE";
    }

    /// <summary>The structured verdict every caller reads, instead of recomputing the parts.</summary>
    internal readonly record struct VisitNewsActor(
        bool InScope,
        bool CanCreate,
        string? ReasonCode,
        bool IsHost,
        bool IsStaffLeaderOfCampus,
        bool IsHo,
        bool IsGuestSide,
        bool IsAcceptedParticipant,
        bool IsEligibleWriterRelation,
        bool WritingWindowOpen,
        bool MediaConsentAllowed,
        bool NewsRequired,
        bool AlreadyHasOwnNews);

    /// <summary>
    /// The campus states news may be written from. AFTER_VISIT because news is written about a visit
    /// that has happened, and CLOSED as well because a published post (or <c>news_not_required</c>) is
    /// a CLOSE condition — writing has to open before close and stay open after it.
    /// </summary>
    public static bool IsWritingWindow(string? instanceStatus) =>
        instanceStatus == VisitInstanceStatus.AfterVisit || instanceStatus == VisitInstanceStatus.Closed;

    /// <summary>
    /// Participant roles that may write, given the participation is confirmed. DEPT_SUPPORT is
    /// deliberately absent: a department contributes logistics to a visit, not its public story. The
    /// create command used to accept any accepted participant, so a DEPT_SUPPORT could post through
    /// the API something the UI never offered them.
    /// </summary>
    public static bool IsWriterParticipantRole(string? participantRole) =>
        participantRole == ParticipantRoles.IcSupport || participantRole == ParticipantRoles.Student;

    /// <param name="acceptedParticipantRole">
    /// The caller's participant role on this instance, or null. Must be resolved from an ACCEPTED (or
    /// ASSIGNED-equivalent, per the caller's own participation query) row — this evaluator judges the
    /// ROLE, and treats a non-null value as "participation is confirmed".
    /// </param>
    /// <param name="alreadyHasOwnNews">
    /// Whether this author already has a post for this instance. One post per author per visit stays
    /// the rule; the caller supplies the fact because only it knows whether it has loaded it.
    /// </param>
    public static VisitNewsActor Evaluate(
        VisitRequestCampus instance,
        VisitRequest visit,
        ICurrentUserService user,
        string? acceptedParticipantRole,
        bool alreadyHasOwnNews = false)
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

        // A Staff Leader who happens to BE the current Host writes as the Host — the relation, not the
        // job title, is what grants it. A Staff Leader who is merely the campus's approver does not.
        bool eligibleWriterRelation = isHost
            || (isAccepted && IsWriterParticipantRole(acceptedParticipantRole));

        bool live = instance.Status != VisitInstanceStatus.Cancelled
                    && visit.Status != VisitRequestStatuses.Cancelled;
        bool writingWindowOpen = live && IsWritingWindow(instance.Status);

        // The two gates the list used to ignore, which is exactly why its verdict disagreed with the
        // Create-News page: the guest refused media coverage, or the campus recorded that no news is
        // needed. Both are properties of the VISIT, not of the writer.
        bool newsRequired = !instance.NewsNotRequired;
        bool mediaConsentAllowed =
            instance.FormDetail?.MediaConsentStatus == MediaConsentStatus.Agreed;

        // Order matters: the reason reported is the FIRST thing that stands in the way, so a person
        // outside the visit is told that rather than being told about media consent for a visit they
        // have nothing to do with.
        string? reason =
            !inScope ? ReasonCodes.NotInScope
            : !eligibleWriterRelation ? ReasonCodes.ParticipantRoleNotAllowed
            : !writingWindowOpen ? ReasonCodes.NotInWritingWindow
            : !newsRequired ? ReasonCodes.NewsNotRequired
            : !mediaConsentAllowed ? ReasonCodes.MediaConsentDenied
            : alreadyHasOwnNews ? ReasonCodes.AlreadyExists
            : null;

        return new VisitNewsActor(
            inScope,
            CanCreate: reason is null,
            reason,
            isHost, isStaffLeaderOfCampus, isHo, isGuestSide, isAccepted,
            eligibleWriterRelation, writingWindowOpen, mediaConsentAllowed, newsRequired,
            alreadyHasOwnNews);
    }
}
