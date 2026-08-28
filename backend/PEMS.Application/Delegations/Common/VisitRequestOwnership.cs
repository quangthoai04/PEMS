using System.Linq;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Everything the pending-campus edit needs to know about WHO is asking, resolved once and answered
/// from one place — the read model that offers the button and the handler that accepts the call both
/// ask this, so a capability can never promise what the command refuses.
///
/// <para>
/// Three INDEPENDENT facts, never a single "which rulebook applies to this person". Editing is a
/// requester-side right and comes from the relations that own the content; leading the campus is an
/// approval right and comes from the campus. One account routinely holds both, and neither cancels the
/// other:
/// </para>
/// <list type="bullet">
/// <item><b>Who may edit</b> — the registrant, or this campus's confirmed operational contact. A STAFF
/// LEADER account is not excluded by being one: a leader who files a request for another campus is a
/// requester there like anybody else, and taking their own request away from them would be a
/// restriction on the person rather than a rule about the campus.</item>
/// <item><b>Who acts AS the campus's leader inside that edit</b> — only the leader OF THIS CAMPUS who
/// also filed the request. That pairing carries the two leader-only privileges, and nothing else does:
/// they exist so the person who will ANSWER the request can fix it instead of refusing it, which is
/// only their business when the request is also theirs to fix.</item>
/// <item><b>Who may decide the campus</b> — its leader, registrant or not. That lives in the approve
/// and reject commands and is not this type's business at all.</item>
/// </list>
///
/// <para>
/// So the leader of Hà Nội who registered a visit to Đà Nẵng edits Đà Nẵng as its REGISTRANT: no
/// 72-hour override, no "Lưu và duyệt", no decision — and no loss of the rights their own request gives
/// them. The leader of Đà Nẵng, on somebody else's request, has the opposite set: they decide it and do
/// not rewrite it.
/// </para>
/// </summary>
/// <param name="IsCampusLeader">Staff Leader of the campus this action targets — role AND campus.</param>
/// <param name="IsRegistrant">Filed the request this campus belongs to.</param>
/// <param name="IsOperationalContact">Confirmed contact of THIS campus — never of a sibling.</param>
public readonly record struct PendingCampusEditRelation(
    bool IsCampusLeader,
    bool IsRegistrant,
    bool IsOperationalContact)
{
    /// <summary>
    /// The one condition that carries the leader-only privileges INSIDE this edit: filing a schedule
    /// within the 72-hour registration floor, and "Lưu và duyệt". Both halves are load-bearing —
    /// leading a DIFFERENT campus grants neither, and leading this one without having filed the request
    /// grants neither, because the edit itself is not open to that actor.
    /// </summary>
    public bool ActsAsCampusLeader => IsCampusLeader && IsRegistrant;

    /// <summary>
    /// May this actor open the pending-campus edit at all — a REQUESTER-side question, answered only by
    /// requester-side relations. Relation only: lifecycle, the mutation cutoff and concurrency are still
    /// decided afterwards by <see cref="VisitMutationPolicy"/> and the edit service.
    /// </summary>
    public bool CanEdit => IsRegistrant || IsOperationalContact;

    /// <summary>
    /// May this actor approve in the same call. Strictly narrower than <see cref="CanEdit"/>: a leader
    /// who did not file the request cannot reach the edit at all, and a registrant who does not lead
    /// THIS campus reaches the edit but never the decision that travels with it. The ordinary
    /// approve/reject commands ask for neither and are untouched by this.
    /// </summary>
    public bool CanSaveAndApprove => ActsAsCampusLeader;

    /// <summary>
    /// The relation to hand <see cref="VisitMutationPolicy"/>, or null when nobody grants the edit.
    /// <c>CAMPUS_LEADER</c> is issued from HERE and nowhere else, so the policy's leader branch cannot
    /// become a second, registrant-free door — and a leader editing a campus they do not lead arrives
    /// as <c>REQUESTER</c>, which is exactly what they are there.
    /// </summary>
    public string? ViewerRelation => !CanEdit
        ? null
        : ActsAsCampusLeader ? VisitViewerRelations.CampusLeader : VisitViewerRelations.Requester;
}

/// <summary>
/// The single place that answers "what is this user to this request / this campus".
///
/// Replaces the old request-level contact model, where a request had one owner
/// (<c>visitor_user_id</c>) whose rights covered every campus. Two relations exist now and they sit
/// at different levels:
///
/// <list type="bullet">
/// <item><b>Registrant</b> — request level. Sees every campus, edits the request-level part, replaces
/// contacts, cancels the whole request. Exactly one per request, may be VISITOR / STAFF / STAFF LEADER.</item>
/// <item><b>Operational contact</b> — campus level. Operates ONE campus, and only after confirming.
/// One person may hold several campuses; holding one grants nothing on its siblings.</item>
/// </list>
///
/// Capabilities are UNIONED, never selected. A user who is both the registrant and the operational
/// contact of one campus keeps both sets — the old code picked a single "relation" string and
/// silently dropped the others.
///
/// Identity is decided ONLY by these ids. A matching contact name or phone on the form snapshot is
/// never evidence of the same person, and an email stored on the form never grants anything: it only
/// says who an invitation may be sent to.
/// </summary>
public static class VisitRequestOwnership
{
    /// <summary>Request-level owner. Null actor is never the registrant.</summary>
    public static bool IsRegistrant(VisitRequest visit, ulong? userId)
        => userId is not null && visit.RegistrantUserId == userId;

    /// <summary>Confirmed operational contact of THIS campus — never of a sibling.</summary>
    public static bool IsOperationalContact(VisitRequestCampus instance, ulong? userId)
        => userId is not null && instance.OperationalContactUserId == userId;

    /// <summary>
    /// The person actually running THIS campus. After approval the Host is the campus's owner: they
    /// decide amendments to it, and the authority travels with the role — a handover moves it to the
    /// new Host the moment it completes, and a pending proposal is then decided by whoever holds the
    /// role at DECISION time, not by whoever held it when the proposal was filed.
    /// </summary>
    public static bool IsCurrentHost(VisitRequestCampus instance, ulong? userId)
        => userId is not null && instance.CurrentHostUserId == userId;

    /// <summary>
    /// The actor's EFFECTIVE ROLE is Staff Leader, whatever campus they lead. On its own it authorizes
    /// NOTHING and it is deliberately not an input to any visit decision: every right in this file is
    /// scoped to a campus or to a relation, and a bare role test is how "leads some campus somewhere"
    /// would start standing in for "leads THIS one". Use <see cref="IsCampusLeader"/>.
    /// </summary>
    public static bool IsStaffLeader(
        PEMS.Application.Common.Interfaces.ICurrentUserService currentUser)
        => currentUser.UserId.HasValue
           && currentUser.RoleCode == RoleCodes.Staff
           && currentUser.SubRole == UserSubRoles.Leader;

    /// <summary>
    /// Staff Leader OF THIS CAMPUS — the approval authority before a decision, and the person who hands
    /// the Host role over after one. Campus scoping is part of the relation, not a separate check: a
    /// leader of a different campus is a stranger to this one, whatever their role name says.
    /// </summary>
    public static bool IsCampusLeader(
        PEMS.Application.Common.Interfaces.ICurrentUserService currentUser, ulong campusId)
        => IsStaffLeader(currentUser) && currentUser.PrimaryCampusId == campusId;

    /// <summary>
    /// Who this caller is to ONE still-pending campus, for the pending-edit door specifically. The read
    /// model and the command handler both build their verdict from this, so "the button was offered"
    /// and "the call was accepted" cannot drift apart.
    ///
    /// <para>
    /// It answers relation ONLY. Whether the campus is still editable at all (lifecycle, the mutation
    /// cutoff, the concurrency token, the campus-set rule) is decided afterwards, by
    /// <see cref="VisitMutationPolicy"/> and the edit service, exactly as before.
    /// </para>
    /// </summary>
    public static PendingCampusEditRelation ResolvePendingCampusEdit(
        VisitRequest visit,
        VisitRequestCampus instance,
        PEMS.Application.Common.Interfaces.ICurrentUserService currentUser)
        => new(
            IsCampusLeader: IsCampusLeader(currentUser, instance.CampusId),
            IsRegistrant: IsRegistrant(visit, currentUser.UserId),
            IsOperationalContact: IsOperationalContact(instance, currentUser.UserId));

    /// <summary>
    /// True when the user is the confirmed operational contact of at least one campus of the
    /// request. Use for "may this person see the request at all"; never to decide an action on a
    /// specific campus — that must ask <see cref="IsOperationalContact"/> for that campus.
    /// </summary>
    public static bool IsOperationalContactOfAny(VisitRequest visit, ulong? userId)
        => userId is not null
           && visit.CampusInstances.Any(c => c.OperationalContactUserId == userId);

    /// <summary>
    /// The GUEST side of one campus: the registrant (who owns every campus of their request) or the
    /// confirmed operational contact of THIS campus. It is the replacement for the old
    /// "role == VISITOR &amp;&amp; visit.visitor_user_id == me" test that guarded the read scopes.
    ///
    /// <para>
    /// Two things changed with it. The role check is gone — a registrant may be a STAFF or STAFF
    /// LEADER account, and gating on VISITOR locked exactly those people out of their own request.
    /// And it takes the CAMPUS, because a contact who confirmed one campus is not on the guest side
    /// of its siblings.
    /// </para>
    /// </summary>
    public static bool IsGuestSide(VisitRequest visit, VisitRequestCampus instance, ulong? userId)
        => IsRegistrant(visit, userId) || IsOperationalContact(instance, userId);

    /// <summary>
    /// The global confirmation gate. While this is true, Staff Leaders may VIEW a request within
    /// their normal responsibility scope, but NO Staff Leader of ANY campus may approve or reject
    /// it — including a campus whose own contact is already confirmed, and including the case where
    /// the registrant is that campus's own Staff Leader.
    ///
    /// <para>
    /// Consume it for approval, rejection, and anything that presupposes a decision is due (the
    /// host picker, a "there is something to review" announcement). Do NOT consume it as a
    /// visibility exclusion: a leader who cannot yet decide still needs to know the request exists,
    /// and the read paths deliberately no longer ask this question. What stops a stranger reading
    /// the request is the campus responsibility rule (<see cref="IsCampusLeader"/>), which this
    /// change did not touch.
    /// </para>
    /// </summary>
    public static bool IsBehindContactGate(VisitRequest visit)
        => visit.Status == VisitRequestStatuses.PendingContactConfirmation;

    /// <summary>
    /// Recomputes the gate from the campuses themselves rather than trusting the stored aggregate.
    /// Use inside a write transaction that has just changed a campus, where the aggregate has not
    /// been recomputed yet.
    /// </summary>
    public static bool HasUnconfirmedCampus(VisitRequest visit)
        => visit.CampusInstances.Any(c =>
            c.Status != VisitInstanceStatuses.Cancelled && c.OperationalContactUserId is null);

    /// <summary>
    /// The guest side of the REQUEST as a whole: the registrant, or somebody who holds at least one
    /// of its campuses. Use it for reads ("may this person open the request at all") and for
    /// request-scoped history. Never use it to authorize a write on a particular campus — that must
    /// ask <see cref="IsGuestSide"/> for that campus, or the holder of campus A could edit campus B.
    /// </summary>
    public static bool IsRequesterSide(VisitRequest visit, ulong? userId)
        => IsRegistrant(visit, userId) || IsOperationalContactOfAny(visit, userId);

    /// <summary>
    /// Campuses of the request this user may act on as operational contact. Empty for everybody
    /// else, including the registrant — the registrant owns the request, not the campus floor.
    /// </summary>
    public static IEnumerable<VisitRequestCampus> OperatedCampuses(VisitRequest visit, ulong? userId)
        => userId is null
            ? Enumerable.Empty<VisitRequestCampus>()
            : visit.CampusInstances.Where(c => c.OperationalContactUserId == userId);

    /// <summary>
    /// Who to tell about something that happened to ONE campus: the person running that campus, and
    /// the registrant who submitted the request. Distinct, and never a sibling campus's contact —
    /// they have their own day to prepare and this is not news about it.
    ///
    /// <para>
    /// The set can be empty (an unconfirmed campus of a request whose registrant has no account yet),
    /// which is why callers iterate it rather than assuming a recipient exists.
    /// </para>
    /// </summary>
    public static IEnumerable<ulong> GuestSideRecipients(VisitRequest visit, VisitRequestCampus instance)
    {
        var seen = new HashSet<ulong>();
        if (instance.OperationalContactUserId is { } contactId) seen.Add(contactId);
        if (visit.RegistrantUserId is { } registrantId) seen.Add(registrantId);
        return seen;
    }
}
