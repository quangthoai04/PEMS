using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.GetVisitInstanceContribution;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetAssignmentsProgressList;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.DepartmentReceptionTasks;

/// <summary>
/// Who may open "Đóng góp kết quả" — and, just as importantly, that the LIST and the ENDPOINT give the
/// same answer.
///
/// <para>
/// The rule: contribution is earned by taking part in the RECEPTION, never by a logistics assignment. A
/// department that supplies a room, an LED wall or a vehicle is responsible for that item; it is not a
/// member of the delegation's reception and has nothing to report on the contribution page.
/// </para>
/// <para>
/// These tests exist because the two halves of that rule had drifted apart. The assignments list and
/// <c>StaffTasksTab</c> already refused a logistics-only user, but
/// <c>GetVisitInstanceContributionQueryHandler</c> still admitted one through
/// <c>ContributionAccess.IsDepartmentContributorForLogistics</c> — so the button was hidden while the
/// endpoint stayed open, and the rule held only for people who did not know the URL. Every case below
/// therefore asserts the list flag AND the endpoint together; a future change that moves one without the
/// other fails here.
/// </para>
/// </summary>
public sealed class ContributionAuthorizationScopeTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-CONTRIB-SCOPE] ";

    private readonly PemsWebApplicationFactory _factory;
    private ulong _campusId;
    private ulong _otherCampusId;
    private ulong _departmentId;
    private ulong _leaderUserId;
    private ulong _staffUserId;

    public ContributionAuthorizationScopeTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var departmentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department).Select(r => r.RoleId).FirstAsync();

        var campuses = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active)
            .OrderBy(c => c.CampusId).Select(c => c.CampusId).Take(2).ToListAsync();
        _campusId = campuses[0];
        _otherCampusId = campuses.Count > 1 ? campuses[1] : campuses[0];

        _departmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == _campusId && d.DepartmentType == "GENERAL" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).Select(d => d.DepartmentId).FirstAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];

        User Make(string name, string sub, string mail) => new()
        {
            FullName = $"{TestPrefix}{name}",
            Email = $"{mail}_{suffix}@pems.test",
            RoleId = departmentRoleId,
            SubRole = sub,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };

        var leader = Make("Leader", UserSubRoles.Leader, "clead");
        var staff = Make("Staff", UserSubRoles.Staff, "cstaff");
        db.Users.AddRange(leader, staff);
        await db.SaveChangesAsync();

        _leaderUserId = leader.UserId;
        _staffUserId = staff.UserId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Fixture ─────────────────────────────────────────────────────────────────────────────────

    private async Task<(ulong VisitRequestId, ulong VisitInstanceId)> CreateVisitInstance(
        ApplicationDbContext db, ulong? campusId = null)
    {
        var visit = new VisitRequest
        {
            RequestCode = $"VR-{DateTime.Now.Ticks}-{Guid.NewGuid().ToString("N")[..4]}",
            Status = VisitRequestStatuses.PendingApproval,
            RegistrantFullName = "Registrant",
            RegistrantEmail = "r@pems.test",
            RegistrantPhone = "0123",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantNationality = "VN",
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync();

        var instance = new VisitRequestCampus
        {
            VisitRequestId = visit.VisitRequestId,
            CampusId = campusId ?? _campusId,
            OperationalContactUserId = _leaderUserId,
            PlannedStartAt = DateTime.Now,
            PlannedEndAt = DateTime.Now.AddHours(2),
            Status = VisitInstanceStatuses.WaitingRequestApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();

        // Every campus instance owns exactly one form detail — the canonical schema asserts it on import,
        // and the contribution page reads the per-campus form rather than falling back to anything. A
        // fixture without it gets past authorization and then dies on "Dữ liệu chuyến thăm theo cơ sở
        // đang thiếu", which would look like an access failure while being nothing of the kind.
        db.VisitInstanceFormDetails.Add(new VisitInstanceFormDetail
        {
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = "Đoàn kiểm thử",
            VisitType = "MEETING",
            Purpose = "Thăm và làm việc",
            WorkingContent = "Nội dung",
            OperationalContactFullName = "Op Contact",
            OperationalContactOrganization = "OpOrg",
            OperationalContactJobTitle = "Trưởng phòng Hợp tác",
            OperationalContactPhone = "+8410",
            OperationalContactEmail = "op@pems.test",
            WorkingLanguage = "EN",
            MediaConsentStatus = "DECLINED",
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        return (visit.VisitRequestId, instance.VisitInstanceId);
    }

    /// <summary>An ACCEPTED logistics item on this instance, assigned to the staff member.</summary>
    private async Task<VisitLogisticsItem> GiveAcceptedLogisticsAsync(ApplicationDbContext db, ulong instanceId)
    {
        var li = new VisitLogisticsItem
        {
            VisitInstanceId = instanceId,
            RequestedToDepartmentId = _departmentId,
            Status = "ACCEPTED",
            AssignedToUserId = _staffUserId,
            AssignedAt = DateTime.Now,
            Title = "Phòng họp + màn LED",
            ItemType = "OTHER",
            CreatedAt = DateTime.Now,
        };
        db.VisitLogisticsItems.Add(li);
        await db.SaveChangesAsync();
        return li;
    }

    /// <summary>A reception participant row for the staff member, in the given status.</summary>
    private async Task<VisitParticipant> GiveParticipantAsync(ApplicationDbContext db, ulong instanceId, string status)
    {
        var p = new VisitParticipant
        {
            VisitInstanceId = instanceId,
            UserId = _staffUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            IsHost = false,
            Status = status,
            AssignedBy = _leaderUserId,
            AssignedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
            CreatedBy = _leaderUserId,
        };
        db.VisitParticipants.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    private FakeCurrentUser StaffUser() => new()
    {
        UserId = _staffUserId,
        RoleCode = RoleCodes.Department,
        SubRole = UserSubRoles.Staff,
        PrimaryCampusId = _campusId,
        DepartmentId = _departmentId,
    };

    /// <summary>Calls the real Contribution query for the staff member.</summary>
    private async Task<ContributionPageDto> OpenContributionAsync(
        IServiceScope scope, ApplicationDbContext db, ulong instanceId)
    {
        var handler = new GetVisitInstanceContributionQueryHandler(
            db, StaffUser(), scope.ServiceProvider.GetRequiredService<IVisitFormReadService>());
        return await handler.Handle(new GetVisitInstanceContributionQuery(instanceId), CancellationToken.None);
    }

    /// <summary>
    /// The list's flag for a row of the given type on the given instance, or null when absent.
    /// <paramref name="visitRequestId"/> scopes the query to one request — this department is shared with
    /// every suite in the run and the list paginates, so an unscoped read can drop the row off page 1 and
    /// look like "no row produced".
    /// </summary>
    private async Task<bool?> ListFlagAsync(
        ApplicationDbContext db, ulong instanceId, string itemType, ulong? visitRequestId = null)
    {
        var handler = new GetAssignmentsProgressListQueryHandler(db, StaffUser());
        var result = await handler.Handle(
            new GetAssignmentsProgressListQuery { ItemType = itemType, VisitRequestId = visitRequestId },
            CancellationToken.None);
        return result.Items.FirstOrDefault(x => x.VisitInstanceId == instanceId)?.CanOpenContribution;
    }

    // ── 1. Logistics alone opens nothing — including through the API ─────────────────────────────

    /// <summary>
    /// The hole this closes: the list said false and the button was hidden, but the endpoint let the same
    /// user straight in. Hiding a control is not authorization — anyone who kept the URL, or read the
    /// network tab once, had the page.
    /// </summary>
    [Fact]
    public async Task Logistics_alone_is_refused_by_the_endpoint_not_only_hidden_in_the_list()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (_, vi) = await CreateVisitInstance(db);
        var li = await GiveAcceptedLogisticsAsync(db, vi);

        // ACCEPTED is precisely the status the removed rule turned into a contributor.
        Assert.Equal("ACCEPTED", li.Status);
        Assert.Equal(_staffUserId, li.AssignedToUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => OpenContributionAsync(scope, db, vi));

        // …and the list agrees, so the two halves cannot drift apart again unnoticed.
        Assert.False(await ListFlagAsync(db, vi, "REQUEST"));
    }

    // ── 2. Taking part in the reception is what opens it ─────────────────────────────────────────

    [Fact]
    public async Task An_accepted_reception_participant_may_open_the_contribution_page()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (_, vi) = await CreateVisitInstance(db);
        await GiveParticipantAsync(db, vi, ParticipantStatuses.Accepted);

        var page = await OpenContributionAsync(scope, db, vi);
        Assert.NotNull(page);

        Assert.True(await ListFlagAsync(db, vi, "INVITATION"));
    }

    // ── 3. Both relations: the invitation is the one that counts ─────────────────────────────────

    /// <summary>
    /// Holding a logistics item as well changes nothing, and the proof is that the access SURVIVES the
    /// logistics row being deleted. Without that second half the test would pass just as happily if the
    /// logistics rule were still doing the work.
    /// </summary>
    [Fact]
    public async Task With_both_relations_the_access_comes_from_the_invitation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (_, vi) = await CreateVisitInstance(db);
        var li = await GiveAcceptedLogisticsAsync(db, vi);
        await GiveParticipantAsync(db, vi, ParticipantStatuses.Accepted);

        Assert.NotNull(await OpenContributionAsync(scope, db, vi));

        db.VisitLogisticsItems.Remove(li);
        await db.SaveChangesAsync();

        Assert.NotNull(await OpenContributionAsync(scope, db, vi));
    }

    // ── 4. An unanswered invitation is not participation ─────────────────────────────────────────

    [Fact]
    public async Task An_invitation_that_has_not_been_accepted_opens_nothing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (_, vi) = await CreateVisitInstance(db);
        await GiveParticipantAsync(db, vi, ParticipantStatuses.Invited);

        await Assert.ThrowsAsync<ForbiddenException>(() => OpenContributionAsync(scope, db, vi));
        Assert.False(await ListFlagAsync(db, vi, "INVITATION"));
    }

    /// <summary>
    /// A declined invitation is not a lingering key either — the row still exists, so this is a different
    /// case from having none.
    /// </summary>
    [Fact]
    public async Task A_declined_invitation_opens_nothing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (_, vi) = await CreateVisitInstance(db);
        await GiveParticipantAsync(db, vi, ParticipantStatuses.Declined);

        await Assert.ThrowsAsync<ForbiddenException>(() => OpenContributionAsync(scope, db, vi));
    }

    // ── 4b. The Leader who delegated is still a participant ─────────────────────────────────────

    /// <summary>
    /// The list and the endpoint asked about the SAME person and disagreed. Once the Leader accepted and
    /// handed the work to a staff member, the assignments list showed the staff member's row and computed
    /// the Leader's rights from it — so the button vanished — while the Contribution endpoint, reading
    /// the Leader's own row, let them straight in by URL. Both are asserted here, on both callers, so
    /// neither half can move without the other.
    /// </summary>
    [Fact]
    public async Task A_leader_who_delegated_still_opens_contribution_in_the_list_and_the_endpoint()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db);

        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = vi,
            UserId = _leaderUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            IsHost = false,
            Status = ParticipantStatuses.Accepted,
            InvitedAt = DateTime.Now,
            RespondedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        });
        await GiveParticipantAsync(db, vi, ParticipantStatuses.Assigned);   // the delegated staff member

        var leaderUser = new FakeCurrentUser
        {
            UserId = _leaderUserId,
            RoleCode = RoleCodes.Department,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
        };

        // Scoped by request: this department is shared with every other suite in the run, and the list
        // paginates — an unscoped query can drop the row off page 1 and read as "no row produced".
        var listHandler = new GetAssignmentsProgressListQueryHandler(db, leaderUser);
        var list = await listHandler.Handle(
            new GetAssignmentsProgressListQuery { ItemType = "INVITATION", VisitRequestId = vr },
            CancellationToken.None);
        var row = list.Items.FirstOrDefault(x => x.VisitInstanceId == vi);
        Assert.NotNull(row);
        Assert.True(row!.CanOpenContribution);

        var endpoint = new GetVisitInstanceContributionQueryHandler(
            db, leaderUser, scope.ServiceProvider.GetRequiredService<IVisitFormReadService>());
        Assert.NotNull(await endpoint.Handle(new GetVisitInstanceContributionQuery(vi), CancellationToken.None));

        // The staff member holds their own relation and their own verdict — unchanged by any of this.
        Assert.True(await ListFlagAsync(db, vi, "INVITATION", vr));
        Assert.NotNull(await OpenContributionAsync(scope, db, vi));
    }

    // ── 5. A relation is to ONE campus instance, never to its neighbours ─────────────────────────

    [Fact]
    public async Task A_relation_on_one_instance_does_not_open_another_campus()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (_, mine) = await CreateVisitInstance(db);
        await GiveParticipantAsync(db, mine, ParticipantStatuses.Accepted);
        await GiveAcceptedLogisticsAsync(db, mine);

        var (_, theirs) = await CreateVisitInstance(db, _otherCampusId);

        Assert.NotNull(await OpenContributionAsync(scope, db, mine));
        await Assert.ThrowsAsync<ForbiddenException>(() => OpenContributionAsync(scope, db, theirs));
    }
}
