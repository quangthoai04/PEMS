using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Minutes;
using PEMS.Application.MeetingMinutes.Queries.SearchAndFilterMinutes;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Minutes;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.Minutes;

/// <summary>
/// SEC-10/11 remediation. Export (PDF/Excel) and Search used to bypass <see cref="MinuteAccess"/>
/// entirely — Export compared only <c>PrimaryCampusId</c> (skipped completely for any caller with
/// none), Search applied a bare own-campus filter (or none, for HO) with no relationship check at
/// all. Both now share the same canonical policy every other Minutes surface already used
/// (<c>Evaluate</c> for the single-record Export handlers, the new <c>WhereAuthorizedFor</c> for the
/// list). Also covers the Admin-exclusion gap: <c>IRoleAccessPolicy.CanAccessVisitManagement</c>
/// already denies Admin across the whole Visit/Delegation domain, and Minutes was the one surface
/// that had not been closed to match.
/// </summary>
public class MinuteAccessAuthorizationTests
{
    private const ulong CampusId = DelegationsTestData.CampusId;
    private const ulong HostUserId = DelegationsTestData.HostUserId;
    private const ulong VisitInstanceId = DelegationsTestData.VisitInstanceId;
    private const ulong VisitRequestId = DelegationsTestData.VisitRequestId;
    private const ulong AdminId = 777;
    private const ulong HoId = 778;
    private const ulong UnrelatedStaffId = 779;

    private static (VisitRequest Visit, VisitRequestCampus Instance) Fixture()
    {
        var visit = DelegationsTestData.CreateVisitRequest();
        var instance = DelegationsTestData.CreateVisitInstance(currentHostUserId: HostUserId);
        return (visit, instance);
    }

    // ── MinuteAccess.Evaluate — Admin exclusion (backs Export + Detail/Lock/Save) ───────────────

    [Fact]
    public void Admin_with_no_relationship_is_out_of_scope()
    {
        var (visit, instance) = Fixture();
        var admin = new FakeCurrentUserService { UserId = AdminId, RoleCode = RoleCodes.Admin, SubRole = null };

        var (inScope, canEdit) = MinuteAccess.Evaluate(instance, visit, admin, acceptedParticipantRole: null);

        Assert.False(inScope);
        Assert.False(canEdit);
    }

    [Fact]
    public void Admin_who_is_the_historical_host_is_still_denied()
    {
        var (visit, instance) = Fixture();
        instance.CurrentHostUserId = AdminId; // the account later became Admin but was once recorded as Host
        var admin = new FakeCurrentUserService { UserId = AdminId, RoleCode = RoleCodes.Admin, SubRole = null };

        var (inScope, _) = MinuteAccess.Evaluate(instance, visit, admin, acceptedParticipantRole: null);

        Assert.False(inScope); // Admin-deny is checked FIRST, before the Host relationship
    }

    [Fact]
    public void Admin_who_holds_an_accepted_participant_row_is_still_denied()
    {
        var (visit, instance) = Fixture();
        var admin = new FakeCurrentUserService { UserId = AdminId, RoleCode = RoleCodes.Admin, SubRole = null };

        var (inScope, _) = MinuteAccess.Evaluate(instance, visit, admin, acceptedParticipantRole: ParticipantRoles.IcSupport);

        Assert.False(inScope);
    }

    [Fact]
    public void Ho_with_no_relationship_is_still_unconditionally_in_scope()
    {
        // Regression: the new Admin-deny check sits before HO's own unconditional branch and must
        // never accidentally catch HO too.
        var (visit, instance) = Fixture();
        var ho = new FakeCurrentUserService { UserId = HoId, RoleCode = RoleCodes.Ho, SubRole = null, PrimaryCampusId = null };

        var (inScope, _) = MinuteAccess.Evaluate(instance, visit, ho, acceptedParticipantRole: null);

        Assert.True(inScope);
    }

    // ── MinuteAccess.WhereAuthorizedFor (SearchAndFilterMinutes) ────────────────────────────────

    private static DelegationsTestDbContext SeedDbWithOneMinute(out ulong minutesId)
    {
        var db = DelegationsTestDbContext.Create();
        db.Roles.Add(DelegationsTestData.CreateRole(DelegationsTestData.StaffRoleId, RoleCodes.Staff));
        db.Campuses.Add(DelegationsTestData.CreateCampus());
        var visit = DelegationsTestData.CreateVisitRequest();
        db.VisitRequests.Add(visit);
        var instance = DelegationsTestData.CreateVisitInstance(currentHostUserId: HostUserId);
        db.VisitRequestCampuses.Add(instance);
        db.SaveChanges();

        var minute = new Minute
        {
            VisitInstanceId = VisitInstanceId,
            Title = "Biên bản kiểm thử",
            Status = MinuteAccess.StatusDraft,
            RowVersion = 0,
            CreatedAt = new DateTime(2026, 6, 1),
        };
        ((IApplicationDbContext)db).Minutes.Add(minute);
        db.SaveChanges();
        minutesId = minute.MinutesId;
        return db;
    }

    private static async Task<SearchAndFilterMinutesDto> RunSearchAsync(
        DelegationsTestDbContext db, ICurrentUserService actor)
        => await new SearchAndFilterMinutesQueryHandler(db, actor)
            .Handle(new SearchAndFilterMinutesQuery { Page = 1, PageSize = 50 }, CancellationToken.None);

    [Fact]
    public async Task Search_UnrelatedSameCampusStaff_SeesNothing()
    {
        // The actual SEC-11 IDOR: same campus, zero relationship to this specific visit.
        using var db = SeedDbWithOneMinute(out _);
        var stranger = new FakeCurrentUserService
        {
            UserId = UnrelatedStaffId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = CampusId,
        };

        var result = await RunSearchAsync(db, stranger);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.Summary.TotalMinutes); // the summary counters must be scoped too, not just the rows
    }

    [Fact]
    public async Task Search_Host_SeesTheirOwnMinute()
    {
        using var db = SeedDbWithOneMinute(out var minutesId);
        var host = new FakeCurrentUserService
        {
            UserId = HostUserId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = CampusId,
        };

        var result = await RunSearchAsync(db, host);

        Assert.Equal(minutesId, Assert.Single(result.Items).MinutesId);
    }

    [Fact]
    public async Task Search_Ho_WithNoRelationship_StillSeesEverything()
    {
        using var db = SeedDbWithOneMinute(out var minutesId);
        var ho = new FakeCurrentUserService { UserId = HoId, RoleCode = RoleCodes.Ho, SubRole = null, PrimaryCampusId = null };

        var result = await RunSearchAsync(db, ho);

        Assert.Equal(minutesId, Assert.Single(result.Items).MinutesId);
    }

    [Fact]
    public async Task Search_Admin_SeesNothingEvenAsHistoricalHost()
    {
        using var db = SeedDbWithOneMinute(out _);
        var instance = await db.VisitRequestCampuses.SingleAsync(v => v.VisitInstanceId == VisitInstanceId);
        instance.CurrentHostUserId = AdminId;
        db.SaveChanges();
        var admin = new FakeCurrentUserService { UserId = AdminId, RoleCode = RoleCodes.Admin, SubRole = null, PrimaryCampusId = CampusId };

        var result = await RunSearchAsync(db, admin);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Search_RegistrantWithNoPrimaryCampus_IsNotBlockedByTheRemovedGuard()
    {
        // The removed "!isHo && PrimaryCampusId == null => Forbidden" guard used to refuse this
        // caller outright, even though the registrant relationship alone should grant access.
        using var db = SeedDbWithOneMinute(out var minutesId);
        var visit = await db.VisitRequests.SingleAsync(v => v.VisitRequestId == VisitRequestId);
        visit.RegistrantUserId = 900;
        db.SaveChanges();
        var registrant = new FakeCurrentUserService
        {
            UserId = 900, RoleCode = RoleCodes.Visitor, SubRole = null, PrimaryCampusId = null,
        };

        var result = await RunSearchAsync(db, registrant);

        Assert.Equal(minutesId, Assert.Single(result.Items).MinutesId);
    }

    [Fact]
    public async Task Search_OperationalContact_Sees_Only_Their_Own_Instance()
    {
        using var db = SeedDbWithOneMinute(out var minutesId);
        var instance = await db.VisitRequestCampuses.SingleAsync(v => v.VisitInstanceId == VisitInstanceId);
        instance.OperationalContactUserId = 901;
        db.SaveChanges();
        var contact = new FakeCurrentUserService
        {
            UserId = 901, RoleCode = RoleCodes.Visitor, SubRole = null, PrimaryCampusId = null,
        };

        var result = await RunSearchAsync(db, contact);

        Assert.Equal(minutesId, Assert.Single(result.Items).MinutesId);
    }
}
