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
/// Two rulebooks, chosen by the actor's effective role rather than by their relation:
/// </para>
/// <list type="bullet">
/// <item><b>A Staff Leader</b> may edit a campus still awaiting a decision ONLY when it is their own
/// campus AND they filed the request themselves. Leading the campus is what makes them its DECIDER, and
/// a decider who also rewrites the thing they are deciding leaves nobody holding the request's version
/// of it. Their answer to a request they did not file is approve or reject, not edit.</item>
/// <item><b>Everyone else</b> — the registrant, the campus's confirmed operational contact — keeps the
/// ordinary requester-side rights, unchanged.</item>
/// </list>
///
/// <para>
/// The role test comes first deliberately. A Staff Leader who is the campus's operational contact, or
/// who leads a DIFFERENT campus of a request they filed, does not fall back to the requester rulebook:
/// the leader rule is a restriction on the person, not a privilege they can route around by holding a
/// second relation.
/// </para>
/// </summary>
/// <param name="ActorIsStaffLeader">Effective role only — says nothing about which campus.</param>
/// <param name="IsCampusLeader">Staff Leader of the campus this action targets.</param>
/// <param name="IsRegistrant">Filed the request this campus belongs to.</param>
/// <param name="IsOperationalContact">Confirmed contact of THIS campus — never of a sibling.</param>
public readonly record struct PendingCampusEditRelation(
    bool ActorIsStaffLeader,
    bool IsCampusLeader,
    bool IsRegistrant,
    bool IsOperationalContact)
{
    /// <summary>
    /// The one condition that carries the leader-only privileges INSIDE this edit: filing a schedule
    /// within the 72-hour registration floor, and "Lưu và duyệt". Both exist so the person who will
    /// answer the request can fix it rather than refuse it — which is only their business when the
    /// request is also theirs to fix.
    /// </summary>
    public bool ActsAsCampusLeader => IsCampusLeader && IsRegistrant;

    /// <summary>May this actor open the pending-campus edit at all (relation only — lifecycle, cutoff
    /// and concurrency are still decided afterwards by <see cref="VisitMutationPolicy"/>).</summary>
    public bool CanEdit => ActorIsStaffLeader
        ? ActsAsCampusLeader
        : IsRegistrant || IsOperationalContact;

    /// <summary>
    /// May this actor approve in the same call. Never widens <see cref="CanEdit"/>: a leader who is not
    /// the registrant cannot reach the edit, so they cannot reach the approval that travels with it —
    /// their ordinary approve/reject on the campus is a different command and is untouched by this.
    /// </summary>
    public bool CanSaveAndApprove => ActsAsCampusLeader;

    /// <summary>
    /// The relation to hand <see cref="VisitMutationPolicy"/>, or null when nobody grants the edit.
    /// <c>CAMPUS_LEADER</c> is issued from HERE and nowhere else, which is what keeps the policy's
    /// leader branch from becoming a second, registrant-free door into the same action.
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
    /// The actor's EFFECTIVE ROLE is Staff Leader — whatever campus they lead, and whatever relation
    /// they hold to the request in front of them. On its own it authorizes nothing; it answers "which
    /// rulebook applies to this person", which is what <see cref="ResolvePendingCampusEdit"/> needs and
    /// what <see cref="IsCampusLeader"/> (role AND campus) cannot express.
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
            ActorIsStaffLeader: IsStaffLeader(currentUser),
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
    /// The global confirmation gate. While this is true NO Staff Leader of ANY campus may see or
    /// process the request — including a campus whose own contact is already confirmed, and
    /// including the case where the registrant is that campus's own Staff Leader.
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
