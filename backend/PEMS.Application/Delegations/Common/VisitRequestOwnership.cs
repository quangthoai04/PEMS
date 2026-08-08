using System.Linq;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.Delegations.Common;

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
    /// Staff Leader OF THIS CAMPUS — the approval authority before a decision, and the person who hands
    /// the Host role over after one. Campus scoping is part of the relation, not a separate check: a
    /// leader of a different campus is a stranger to this one, whatever their role name says.
    /// </summary>
    public static bool IsCampusLeader(
        PEMS.Application.Common.Interfaces.ICurrentUserService currentUser, ulong campusId)
        => currentUser.UserId.HasValue
           && currentUser.RoleCode == RoleCodes.Staff
           && currentUser.SubRole == UserSubRoles.Leader
           && currentUser.PrimaryCampusId == campusId;

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
