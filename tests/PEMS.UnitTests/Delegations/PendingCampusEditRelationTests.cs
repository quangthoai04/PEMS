using System;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
using Xunit;

namespace PEMS.UnitTests.Delegations;

/// <summary>
/// WHO may edit a campus that is still waiting for its decision, and who may approve from inside that
/// edit. This is the relation half of the rule, decided by
/// <see cref="VisitRequestOwnership.ResolvePendingCampusEdit"/> — the one place the read model and the
/// command handler both ask, so a capability and a 403 can never disagree.
///
/// <para>
/// The rule the suite pins, as THREE independent facts rather than one rulebook per person:
/// </para>
/// <list type="bullet">
/// <item>editing is requester-side — the registrant or this campus's confirmed contact, and a STAFF
/// LEADER account is one of those like anybody else on a request they filed;</item>
/// <item>the leader-only privileges inside that edit (the 72-hour override, "Lưu và duyệt") need the
/// leader OF THIS CAMPUS who is ALSO the registrant — leading the campus makes them its decider, and a
/// decider who rewrites what they are deciding leaves nobody holding the requester's version of it;</item>
/// <item>deciding the campus is the leader's, registrant or not, and lives in other commands entirely.</item>
/// </list>
/// <para>
/// So the two mixed actors land on opposite sides: the leader of another campus who filed the request
/// edits it as a REQUESTER with no privileges, and this campus's leader on somebody else's request gets
/// no edit at all while keeping the decision.
/// </para>
/// <para>
/// Nothing here touches approving or rejecting the campus the ORDINARY way: those are
/// <c>ApproveCampusInstanceCommand</c> / <c>RejectCampusInstanceCommand</c>, they ask only for the
/// campus's leader, and this rule does not reach them.
/// </para>
/// </summary>
public sealed class PendingCampusEditRelationTests
{
    private const ulong CampusHn = 1, CampusHcm = 2;
    private const ulong Registrant = 10, LeaderHn = 3, LeaderHcm = 9, Contact = 20, Stranger = 77;

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id, string roleCode, string? subRole = null, ulong? campusId = null)
        {
            UserId = id;
            RoleCode = roleCode;
            SubRole = subRole;
            PrimaryCampusId = campusId;
        }

        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; }
        public string? SubRole { get; }
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static FakeUser Visitor(ulong id) => new(id, RoleCodes.Visitor);
    private static FakeUser Staff(ulong id, ulong campusId) => new(id, RoleCodes.Staff, UserSubRoles.Staff, campusId);
    private static FakeUser StaffLeader(ulong id, ulong campusId) => new(id, RoleCodes.Staff, UserSubRoles.Leader, campusId);

    /// <summary>A one-campus (HN) request filed by <paramref name="registrantUserId"/>, still waiting.</summary>
    private static (VisitRequest Visit, VisitRequestCampus Instance) Pending(
        ulong? registrantUserId, ulong? operationalContactUserId = null, ulong campusId = CampusHn)
    {
        var instance = new VisitRequestCampus
        {
            VisitInstanceId = 100,
            CampusId = campusId,
            Status = VisitInstanceStatuses.WaitingRequestApproval,
            PlannedStartAt = new DateTime(2026, 9, 1, 9, 0, 0),
            OperationalContactUserId = operationalContactUserId,
        };
        var visit = new VisitRequest
        {
            VisitRequestId = 500,
            Status = VisitRequestStatuses.PendingApproval,
            RegistrantUserId = registrantUserId,
        };
        visit.CampusInstances.Add(instance);
        return (visit, instance);
    }

    private static PendingCampusEditRelation Resolve(ICurrentUserService actor, VisitRequest v, VisitRequestCampus i)
        => VisitRequestOwnership.ResolvePendingCampusEdit(v, i, actor);

    // ── CASE 1 + 2 — the change this suite exists for ───────────────────────────────────────────

    /// <summary>
    /// The campus is theirs; the request is not. Before this rule they could rewrite a guest's form and
    /// approve their own rewrite in one call — the correction and the decision made by the same person,
    /// with nobody left holding what the requester actually asked for.
    /// </summary>
    [Fact]
    public void ACampusLeaderWhoDidNotFileTheRequest_MayNeitherEditNorSaveAndApprove()
    {
        var (visit, instance) = Pending(registrantUserId: Registrant);

        var relation = Resolve(StaffLeader(LeaderHn, CampusHn), visit, instance);

        Assert.True(relation.IsCampusLeader);          // still this campus's leader …
        Assert.False(relation.IsRegistrant);           // … but not its author
        Assert.False(relation.CanEdit);                // CASE 1
        Assert.False(relation.CanSaveAndApprove);      // CASE 2
        Assert.False(relation.ActsAsCampusLeader);     // and so no 72-hour override either
        Assert.Null(relation.ViewerRelation);
    }

    // ── CASE 5 + 6 — the leader who IS the registrant keeps everything ──────────────────────────

    [Fact]
    public void ACampusLeaderWhoFiledTheRequest_MayEditAndMaySaveAndApprove()
    {
        var (visit, instance) = Pending(registrantUserId: LeaderHn);

        var relation = Resolve(StaffLeader(LeaderHn, CampusHn), visit, instance);

        Assert.True(relation.CanEdit);                 // CASE 5
        Assert.True(relation.CanSaveAndApprove);       // CASE 6
        Assert.True(relation.ActsAsCampusLeader);
        // CAMPUS_LEADER is what carries the 72-hour override into the policy, and it is issued from
        // here and nowhere else — which is what stops the policy's leader branch being a second door.
        Assert.Equal(VisitViewerRelations.CampusLeader, relation.ViewerRelation);
    }

    // ── CASE 7 — an ordinary STAFF registrant edits, but does not decide ────────────────────────

    /// <summary>
    /// Their rights come from being the registrant, not from working here — and deciding the campus is
    /// not among them, because they do not lead it.
    /// </summary>
    [Fact]
    public void AStaffRegistrantWhoIsNotALeader_MayEditButMayNotSaveAndApprove()
    {
        var (visit, instance) = Pending(registrantUserId: Registrant);

        var relation = Resolve(Staff(Registrant, CampusHn), visit, instance);

        Assert.False(relation.IsCampusLeader);
        Assert.True(relation.CanEdit);
        Assert.False(relation.CanSaveAndApprove);
        Assert.Equal(VisitViewerRelations.Requester, relation.ViewerRelation);
    }

    // ── CASE 8 — the operational contact is untouched by any of this ────────────────────────────

    [Fact]
    public void TheConfirmedOperationalContactOfThisCampus_StillEdits()
    {
        var (visit, instance) = Pending(registrantUserId: Registrant, operationalContactUserId: Contact);

        var relation = Resolve(Visitor(Contact), visit, instance);

        Assert.True(relation.CanEdit);
        Assert.False(relation.CanSaveAndApprove);
        Assert.Equal(VisitViewerRelations.Requester, relation.ViewerRelation);
    }

    [Fact]
    public void TheRegistrantOfTheRequest_StillEdits()
    {
        var (visit, instance) = Pending(registrantUserId: Registrant);

        var relation = Resolve(Visitor(Registrant), visit, instance);

        Assert.True(relation.CanEdit);
        Assert.Equal(VisitViewerRelations.Requester, relation.ViewerRelation);
    }

    // ── CASE 9 — a leader of ANOTHER campus keeps their REGISTRANT rights, and gains nothing else ──

    /// <summary>
    /// The HCM leader filed a request that names HN. At HN they lead nothing, so they are simply the
    /// person who filed it: they edit as REQUESTER, and none of the leader-only privileges follow them
    /// there.
    ///
    /// <para>
    /// This is the case an earlier version got wrong, by treating "is a Staff Leader" as a rulebook for
    /// the PERSON: it refused the edit outright and so took away rights their own request gives them.
    /// The role is not a restriction — leading a campus is a fact about THAT campus, and about no other.
    /// The floor and "Lưu và duyệt" stay behind because they protect the leader who will actually have
    /// to prepare the visit at HN, which is somebody else entirely.
    /// </para>
    /// </summary>
    [Fact]
    public void AStaffLeaderOfAnotherCampus_EditsAsRegistrantWithNoLeaderPrivileges()
    {
        var (visit, instance) = Pending(registrantUserId: LeaderHcm);

        var relation = Resolve(StaffLeader(LeaderHcm, CampusHcm), visit, instance);

        Assert.False(relation.IsCampusLeader);        // not the leader of the campus being edited
        Assert.True(relation.IsRegistrant);
        Assert.True(relation.CanEdit);                // …and that is enough to edit
        Assert.False(relation.ActsAsCampusLeader);    // but not to carry the 72-hour override
        Assert.False(relation.CanSaveAndApprove);     // nor to decide the campus in the same call
        Assert.Equal(VisitViewerRelations.Requester, relation.ViewerRelation);
    }

    /// <summary>
    /// Leading another campus and holding NO requester-side relation is still nothing here — the edit
    /// needs a relation to the request, and this actor has none.
    /// </summary>
    [Fact]
    public void AStaffLeaderOfAnotherCampusWhoDidNotFileTheRequest_IsRefused()
    {
        var (visit, instance) = Pending(registrantUserId: Registrant);

        var relation = Resolve(StaffLeader(LeaderHcm, CampusHcm), visit, instance);

        Assert.False(relation.CanEdit);
        Assert.Null(relation.ViewerRelation);
    }

    // ── CASE 10 — a second relation is counted, but grants only what it is ──────────────────────

    /// <summary>
    /// This campus's leader who is ALSO its operational contact, on somebody else's request. The contact
    /// relation is real and it opens the edit — as a REQUESTER, which is what a contact is. What it does
    /// not do is hand them the leader-only privileges: those need the registrant half, and running the
    /// campus is not filing the request.
    ///
    /// <para>
    /// Unreachable through the workflow now that an operational contact must be EXTERNAL — an internal
    /// account can no longer be appointed or accept the role. Kept because rows like it exist in
    /// databases created before that rule, and this door must answer them on their own merits rather
    /// than assume the appointment door was shut.
    /// </para>
    /// </summary>
    [Fact]
    public void ACampusLeaderWhoIsOnlyTheOperationalContact_EditsAsRequesterWithNoLeaderPrivileges()
    {
        var (visit, instance) = Pending(registrantUserId: Registrant, operationalContactUserId: LeaderHn);

        var relation = Resolve(StaffLeader(LeaderHn, CampusHn), visit, instance);

        Assert.True(relation.IsOperationalContact);
        Assert.True(relation.IsCampusLeader);
        Assert.False(relation.IsRegistrant);
        Assert.True(relation.CanEdit);
        Assert.False(relation.ActsAsCampusLeader);
        Assert.False(relation.CanSaveAndApprove);
        Assert.Equal(VisitViewerRelations.Requester, relation.ViewerRelation);
    }

    // ── Strangers, as before ────────────────────────────────────────────────────────────────────

    [Fact]
    public void SomebodyWithNoRelationAtAll_IsRefused()
    {
        var (visit, instance) = Pending(registrantUserId: Registrant);

        var relation = Resolve(Staff(Stranger, CampusHn), visit, instance);

        Assert.False(relation.CanEdit);
        Assert.Null(relation.ViewerRelation);
    }

    // ── The relation actually reaches the policy the way the handler uses it ────────────────────

    /// <summary>
    /// Ties the two halves together: the relation this resolver issues is the one
    /// <see cref="VisitMutationPolicy"/> is asked about, so "may this person" and "is this action open"
    /// are answered about the same actor. A refused relation never reaches the policy at all — the
    /// handler throws Forbidden first — which is why the null case is asserted rather than evaluated.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheIssuedRelationIsWhatThePolicyIsAskedAbout(bool leaderIsRegistrant)
    {
        var (visit, instance) = Pending(registrantUserId: leaderIsRegistrant ? LeaderHn : Registrant);
        var relation = Resolve(StaffLeader(LeaderHn, CampusHn), visit, instance);

        if (!leaderIsRegistrant)
        {
            Assert.Null(relation.ViewerRelation);
            return;
        }

        var decision = VisitMutationPolicy.Evaluate(new VisitMutationContext(
            VisitMutationAction.EditPendingCampus, visit.Status, instance.Status,
            instance.PlannedStartAt, new DateTime(2026, 8, 1, 9, 0, 0), relation.ViewerRelation!));
        Assert.True(decision.Allowed);
    }
}
