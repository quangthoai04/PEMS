using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Queries.GetSubmittedVisitRequestFormDetail;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.GetSubmittedVisitRequestFormDetail;

/// <summary>
/// SEC-12 remediation. The visible-instance computation used to be a nested ternary that picked
/// exactly ONE branch per caller, falling through to "every campus instance" for anyone who was
/// none of Staff Leader/Host/Department-Staff — including a plain Participant or Operational
/// Contact confirmed on only ONE campus, leaking every sibling campus of the request. It is now a
/// union of independently-computed relationship sets, with Staff Leader as its own exclusive
/// campus-jurisdiction branch (never widened by a separately-held Host/Participant/Operational-
/// Contact relationship elsewhere) — except when the Staff Leader is ALSO the registrant, who still
/// sees the whole request via that separate "sees everything" relation.
///
/// Also covers: <c>CreatedBy</c> is no longer read as a request-level ownership signal —
/// <c>RegistrantUserId</c> is the only one.
/// </summary>
public class GetSubmittedVisitRequestFormDetailVisibilityTests
{
    private const ulong CampusA = DelegationsTestData.CampusId;      // 1
    private const ulong CampusB = DelegationsTestData.OtherCampusId; // 2
    private const ulong RequestId = DelegationsTestData.VisitRequestId;
    private const ulong InstanceA = DelegationsTestData.VisitInstanceId; // 10
    private const ulong InstanceB = 20;

    private static (DelegationsTestDbContext Db, GetSubmittedVisitRequestFormDetailQueryHandler Handler)
        BuildHandler(FakeDelegationsCurrentUser actor)
    {
        var db = DelegationsTestDbContext.Create();
        db.Campuses.AddRange(DelegationsTestData.CreateCampus(CampusA), DelegationsTestData.CreateCampus(CampusB));
        db.Roles.AddRange(
            DelegationsTestData.CreateRole(DelegationsTestData.StaffRoleId, RoleCodes.Staff),
            DelegationsTestData.CreateRole(DelegationsTestData.DepartmentRoleId, RoleCodes.Department),
            DelegationsTestData.CreateRole(DelegationsTestData.StudentRoleId, RoleCodes.Student));

        var visit = DelegationsTestData.CreateVisitRequest(RequestId);
        visit.VisitScope = VisitScopes.MultiCampus;
        visit.Status = VisitRequestStatuses.Approved;
        db.VisitRequests.Add(visit);

        var instanceA = DelegationsTestData.CreateVisitInstance(
            InstanceA, VisitInstanceStatus.Assigned, CampusA, currentHostUserId: null, RequestId);
        var instanceB = DelegationsTestData.CreateVisitInstance(
            InstanceB, VisitInstanceStatus.Assigned, CampusB, currentHostUserId: null, RequestId);
        // Identical content on both instances (the seed default) — content differing per campus is
        // its own case (HasMixedVisibleContent, exercised separately) and is orthogonal to what these
        // visibility tests check: WHICH instances are returned, proven by VisitInstanceId/Count.
        db.VisitRequestCampuses.AddRange(instanceA, instanceB);
        db.SaveChanges();

        var formRead = new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance);
        var handler = new GetSubmittedVisitRequestFormDetailQueryHandler(
            db, actor, NullLogger<GetSubmittedVisitRequestFormDetailQueryHandler>.Instance, formRead);
        return (db, handler);
    }

    private static Task<SubmittedVisitRequestFormDetailDto> RunAsync(
        GetSubmittedVisitRequestFormDetailQueryHandler handler)
        => handler.Handle(new GetSubmittedVisitRequestFormDetailQuery(RequestId), CancellationToken.None);

    // ── Regression: single-relationship callers stay scoped to their own campus ────────────────

    [Fact]
    public async Task OperationalContactOnly_SeesOnlyTheirCampus()
    {
        // Operational Contact is a guest-side relation — the entry gate recognizes it only through
        // the isVisitor branch (VisitRequestOwnership.IsOperationalContactOfAny), matching how this
        // relationship is actually held in PEMS (the confirmed contact of a campus, not an internal
        // Staff account).
        const ulong contactId = 501;
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = contactId, RoleId = 0, RoleCode = RoleCodes.Visitor, SubRole = null,
        });
        db.VisitRequestCampuses.Single(c => c.VisitInstanceId == InstanceA).OperationalContactUserId = contactId;
        db.SaveChanges();

        var dto = await RunAsync(handler);

        var campus = Assert.Single(dto.Campuses);
        Assert.Equal((long)InstanceA, campus.VisitInstanceId);
    }

    [Fact]
    public async Task ParticipantOnly_SeesOnlyTheirCampus()
    {
        const ulong participantId = 502;
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = participantId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff,
        });
        db.Users.Add(DelegationsTestData.CreateUser(participantId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            801, participantId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted, InstanceA));
        db.SaveChanges();

        var dto = await RunAsync(handler);

        var campus = Assert.Single(dto.Campuses);
        Assert.Equal((long)InstanceA, campus.VisitInstanceId);
    }

    [Fact]
    public async Task RegistrantWhoAlsoParticipates_SeesTheWholeRequest()
    {
        const ulong registrantId = 503;
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = registrantId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff,
        });
        db.Users.Add(DelegationsTestData.CreateUser(registrantId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.VisitRequests.Single().RegistrantUserId = registrantId;
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            802, registrantId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted, InstanceA));
        db.SaveChanges();

        var dto = await RunAsync(handler);

        Assert.Equal(2, dto.Campuses.Count);
    }

    // ── Union: multiple simultaneous relationships across different campuses ───────────────────

    [Fact]
    public async Task ParticipantOnA_AndOperationalContactOnB_SeesBoth()
    {
        const ulong userId = 504;
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = userId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff,
        });
        db.Users.Add(DelegationsTestData.CreateUser(userId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            803, userId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted, InstanceA));
        db.VisitRequestCampuses.Single(c => c.VisitInstanceId == InstanceB).OperationalContactUserId = userId;
        db.SaveChanges();

        var dto = await RunAsync(handler);

        Assert.Equal(2, dto.Campuses.Count);
    }

    // ── Staff Leader: exclusive campus-jurisdiction branch, never unioned ──────────────────────

    [Fact]
    public async Task StaffLeaderOfA_AlsoHostOfB_NotRegistrant_SeesOnlyA()
    {
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = 505, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = CampusA,
        });
        db.VisitRequestCampuses.Single(c => c.VisitInstanceId == InstanceB).CurrentHostUserId = 505;
        db.SaveChanges();

        var dto = await RunAsync(handler);

        var campus = Assert.Single(dto.Campuses);
        Assert.Equal((long)InstanceA, campus.VisitInstanceId); // Campus B stays hidden despite the Host relationship
    }

    [Fact]
    public async Task StaffLeaderOfA_RequestOnlyHasB_NotRegistrant_IsForbidden()
    {
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = 506, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = CampusA,
        });
        // This Staff Leader is ALSO a participant of Campus B — proves the branch does not fall
        // through to the union even when the caller holds an unrelated relationship elsewhere.
        db.Users.Add(DelegationsTestData.CreateUser(506, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, null, CampusA));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            804, 506, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted, InstanceB));
        db.SaveChanges();
        // Remove Campus A from the request entirely so it genuinely never touches this leader's campus.
        db.VisitRequestCampuses.Remove(db.VisitRequestCampuses.Single(c => c.VisitInstanceId == InstanceA));
        db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(handler));
    }

    [Fact]
    public async Task StaffLeaderOfA_WhoIsAlsoTheRegistrant_SeesBothCampuses()
    {
        const ulong leaderId = 507;
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = leaderId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = CampusA,
        });
        db.VisitRequests.Single().RegistrantUserId = leaderId;
        db.SaveChanges();

        var dto = await RunAsync(handler);

        Assert.Equal(2, dto.Campuses.Count); // isRegistrant is checked before the Staff Leader branch
    }

    [Fact]
    public async Task StaffLeaderOfA_NotRegistrant_RequestSpansAandB_SeesOnlyA()
    {
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = 508, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = CampusA,
        });
        db.VisitRequests.Single().RegistrantUserId = 999; // someone else registered it
        db.SaveChanges();

        var dto = await RunAsync(handler);

        var campus = Assert.Single(dto.Campuses);
        Assert.Equal((long)InstanceA, campus.VisitInstanceId);
    }

    // ── CreatedBy is not an ownership signal ────────────────────────────────────────────────────

    // ── Mixedness is judged only across the VISIBLE set, never the hidden sibling ───────────────

    [Fact]
    public async Task HoWithBothCampusesVisible_DifferentContent_IsConflict()
    {
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = 510, RoleId = 0, RoleCode = RoleCodes.Ho, SubRole = null,
        });
        db.VisitRequestCampuses.Single(c => c.VisitInstanceId == InstanceB).FormDetail!.DelegationName = "Khác hẳn";
        db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => RunAsync(handler));
    }

    [Fact]
    public async Task StaffLeaderOwnCampusOnly_HiddenSiblingHasDifferentContent_StillSucceeds()
    {
        // The Staff Leader only ever sees Campus A; Campus B's content differing must never surface
        // as a mixed-content 409 for a viewer who cannot see B at all.
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = 511, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = CampusA,
        });
        db.VisitRequestCampuses.Single(c => c.VisitInstanceId == InstanceB).FormDetail!.DelegationName = "Khác hẳn";
        db.SaveChanges();

        var dto = await RunAsync(handler);

        var campus = Assert.Single(dto.Campuses);
        Assert.Equal((long)InstanceA, campus.VisitInstanceId);
    }

    [Fact]
    public async Task CreatedByCallerWithDifferentRegistrant_IsForbidden_NotACrash()
    {
        const ulong creatorId = 509;
        var (db, handler) = BuildHandler(new FakeDelegationsCurrentUser
        {
            UserId = creatorId, RoleId = 0, RoleCode = RoleCodes.Visitor, SubRole = null,
        });
        var visit = db.VisitRequests.Single();
        visit.CreatedBy = creatorId;
        visit.RegistrantUserId = 999; // a different person is the real registrant
        db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(handler));
    }
}
