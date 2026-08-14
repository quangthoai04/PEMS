using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetAssignmentsProgressList;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;


namespace PEMS.IntegrationTests.DepartmentReceptionTasks;

public sealed class FakeCurrentUser : ICurrentUserService
{
    public ulong? UserId { get; set; }
    public string? Email { get; set; }
    public ulong? RoleId { get; set; }
    public string? RoleCode { get; set; }
    public string? SubRole { get; set; }
    public ulong? PrimaryCampusId { get; set; }
    public ulong? DepartmentId { get; set; }
    public ulong? SessionId { get; set; }
    public string? LoginPortal { get; set; }
    public bool IsAuthenticated { get; set; } = true;
    public string FullName { get; set; } = "Test User";
}

public sealed class GetAssignmentsProgressListQueryTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-ASSIGNMENTS-PROGRESS] ";

    private readonly PemsWebApplicationFactory _factory;
    private ulong _campusId;
    private ulong _departmentId;
    private ulong _leaderUserId;
    private ulong _staffUserId;
    private ulong _otherStaffUserId;

    public GetAssignmentsProgressListQueryTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var departmentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department).Select(r => r.RoleId).FirstAsync();

        _campusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active)
            .OrderBy(c => c.CampusId).Select(c => c.CampusId).FirstAsync();

        _departmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == _campusId && d.DepartmentType == "GENERAL" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).Select(d => d.DepartmentId).FirstAsync();

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var leader = new User
        {
            FullName = $"{TestPrefix}Leader",
            Email = $"leader_{uniqueSuffix}@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var staff = new User
        {
            FullName = $"{TestPrefix}Staff",
            Email = $"staff_{uniqueSuffix}@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Staff,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var otherStaff = new User
        {
            FullName = $"{TestPrefix}OtherStaff",
            Email = $"staff2_{uniqueSuffix}@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Staff,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.AddRange(leader, staff, otherStaff);
        await db.SaveChangesAsync();

        _leaderUserId = leader.UserId;
        _staffUserId = staff.UserId;
        _otherStaffUserId = otherStaff.UserId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private Task<(ulong VisitRequestId, ulong VisitInstanceId)> CreateVisitInstance(ApplicationDbContext db)
        => CreateVisitInstance(db, DateTime.Now, DateTime.Now.AddHours(2));

    private async Task<(ulong VisitRequestId, ulong VisitInstanceId)> CreateVisitInstance(
        ApplicationDbContext db, DateTime plannedStart, DateTime plannedEnd)
    {
        var visit = new VisitRequest
        {
            RequestCode = $"VR-{DateTime.Now.Ticks}",
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
            CampusId = _campusId,
            OperationalContactUserId = _leaderUserId,
            PlannedStartAt = plannedStart,
            PlannedEndAt = plannedEnd,
            Status = VisitInstanceStatuses.WaitingRequestApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();

        return (visit.VisitRequestId, instance.VisitInstanceId);
    }

    [Fact]
    public async Task LogisticsItem_Unassigned_ShouldHaveNullResponsible_AndCannotContribute()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db);

        var li = new VisitLogisticsItem
        {
            VisitInstanceId = vi,
            RequestedToDepartmentId = _departmentId,
            Status = "REQUESTED",
            Title = "Need something",
            ItemType = "OTHER",
            CreatedAt = DateTime.Now,
        };
        db.VisitLogisticsItems.Add(li);
        await db.SaveChangesAsync();

        var handler = new GetAssignmentsProgressListQueryHandler(db, 
            new FakeCurrentUser { UserId = _leaderUserId, RoleCode = RoleCodes.Department, SubRole = UserSubRoles.Leader, PrimaryCampusId = _campusId, DepartmentId = _departmentId });
        
        var result = await handler.Handle(new GetAssignmentsProgressListQuery { ItemType = "REQUEST" }, CancellationToken.None);
        var item = result.Items.FirstOrDefault(x => x.LogisticsItemId == li.LogisticsItemId);
        
        Assert.NotNull(item);
        Assert.Equal("REQUESTED", item!.UiStatus);
        Assert.Null(item.CurrentResponsibleUserId);
        Assert.Null(item.CurrentResponsibleName);
        Assert.Null(item.CurrentResponsibleRole);
        Assert.False(item.CanOpenContribution);
    }

    /// <summary>
    /// An accepted logistics request makes the staff member RESPONSIBLE for it — but it does not make
    /// them a contributor to the visit.
    ///
    /// <para>
    /// This test used to assert the opposite of its last line. Commit <c>4450eb3e</c> derived the flag
    /// from <c>ContributionAccess.IsDepartmentContributorForLogistics</c>, so an accepted request opened
    /// "Đóng góp kết quả"; commit <c>8e8628fe</c> replaced that with a constant <c>false</c> and wrote the
    /// reason into the projection: contribution is earned by accepting the INVITATION to help receive the
    /// delegation, and a department fulfilling a room/LED/vehicle request has not thereby taken part in
    /// the reception. The frontend documents the same rule where it renders the button
    /// (<c>StaffTasksTab.tsx</c>). The rule changed; this assertion was left behind, which is why it has
    /// been the suite's one red test since.
    /// </para>
    /// <para>
    /// Everything else the test proved still holds and is still asserted: the staff member is the current
    /// responsible party, and the Leader sees that without gaining the contribution entry point either.
    /// </para>
    /// </summary>
    [Fact]
    public async Task LogisticsItem_AssignedAndAcceptedByStaff_ShouldHaveStaffResponsible_ButNotContribute()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db);

        var li = new VisitLogisticsItem
        {
            VisitInstanceId = vi,
            RequestedToDepartmentId = _departmentId,
            Status = "ACCEPTED",
            AssignedToUserId = _staffUserId,
            AssignedAt = DateTime.Now,
            Title = "Need something",
            ItemType = "OTHER",
            CreatedAt = DateTime.Now,
        };
        db.VisitLogisticsItems.Add(li);
        await db.SaveChangesAsync();

        // 1. Leader views: Sees staff responsible, but CANNOT contribute
        var leaderHandler = new GetAssignmentsProgressListQueryHandler(db, 
            new FakeCurrentUser { UserId = _leaderUserId, RoleCode = RoleCodes.Department, SubRole = UserSubRoles.Leader, PrimaryCampusId = _campusId, DepartmentId = _departmentId });
        var leaderResult = await leaderHandler.Handle(new GetAssignmentsProgressListQuery { ItemType = "REQUEST" }, CancellationToken.None);
        var itemL = leaderResult.Items.FirstOrDefault(x => x.LogisticsItemId == li.LogisticsItemId);
        Assert.NotNull(itemL);
        Assert.Equal(_staffUserId, itemL!.CurrentResponsibleUserId);
        Assert.False(itemL.CanOpenContribution);

        // 2. Staff views: sees THEMSELVES responsible — the assignment landed on them and they accepted it.
        var staffHandler = new GetAssignmentsProgressListQueryHandler(db,
            new FakeCurrentUser { UserId = _staffUserId, RoleCode = RoleCodes.Department, SubRole = UserSubRoles.Staff, PrimaryCampusId = _campusId, DepartmentId = _departmentId });
        var staffResult = await staffHandler.Handle(new GetAssignmentsProgressListQuery { ItemType = "REQUEST" }, CancellationToken.None);
        var itemS = staffResult.Items.FirstOrDefault(x => x.LogisticsItemId == li.LogisticsItemId);
        Assert.NotNull(itemS);
        Assert.Equal(_staffUserId, itemS!.CurrentResponsibleUserId);

        // …but a logistics request is not a seat at the reception, so it opens no contribution — and the
        // acceptance is what makes this worth asserting: ACCEPTED is the status the OLD rule turned into
        // a contributor, so a revert to it would fail here rather than pass unnoticed.
        Assert.Equal("ACCEPTED", li.Status);
        Assert.False(itemS.CanOpenContribution);

        // The responsibility itself is real, which is what separates "no contribution" from "no row".
        Assert.True(itemS.CanSignBorrow);
    }

    [Fact]
    public async Task Invitation_Requested_ShouldHaveNullResponsible_AndCannotContribute()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db);

        // Leader is invited but hasn't accepted yet
        var p = new VisitParticipant
        {
            VisitInstanceId = vi,
            UserId = _leaderUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            Status = ParticipantStatuses.Invited,
            InvitedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        db.VisitParticipants.Add(p);
        await db.SaveChangesAsync();

        var handler = new GetAssignmentsProgressListQueryHandler(db, 
            new FakeCurrentUser { UserId = _leaderUserId, RoleCode = RoleCodes.Department, SubRole = UserSubRoles.Leader, PrimaryCampusId = _campusId, DepartmentId = _departmentId });
        
        var result = await handler.Handle(new GetAssignmentsProgressListQuery { ItemType = "INVITATION" }, CancellationToken.None);
        var item = result.Items.FirstOrDefault(x => x.ParticipantId == p.ParticipantId);
        
        Assert.NotNull(item);
        Assert.Equal("REQUESTED", item!.UiStatus);
        Assert.Null(item.CurrentResponsibleUserId);
        Assert.False(item.CanOpenContribution);
    }

    // ── Contribution: the CALLER's relation decides, never the row being displayed ───────────────

    /// <summary>A Leader who has accepted the invitation is a participant, and participants contribute.</summary>
    [Fact]
    public async Task Invitation_AcceptedByLeader_OpensContribution()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db);

        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = vi,
            UserId = _leaderUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            Status = ParticipantStatuses.Accepted,
            InvitedAt = DateTime.Now,
            RespondedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        var item = await LeaderRowAsync(db, vr, vi);
        Assert.NotNull(item);
        Assert.True(item!.CanOpenContribution);
    }

    /// <summary>
    /// The regression this file exists for. After the Leader accepts and then hands the work to a staff
    /// member, the row DISPLAYS the staff member — and the flag used to be computed from that same row,
    /// so the Leader was told they were not a participant of a visit they had accepted. The button
    /// disappeared from the list while the Contribution endpoint kept letting them in by URL.
    /// </summary>
    [Fact]
    public async Task Invitation_LeaderKeepsContribution_AfterDelegatingToStaff()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db);

        var leaderRow = new VisitParticipant
        {
            VisitInstanceId = vi,
            UserId = _leaderUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            Status = ParticipantStatuses.Accepted,
            InvitedAt = DateTime.Now,
            RespondedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        var staffRow = new VisitParticipant
        {
            VisitInstanceId = vi,
            UserId = _staffUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            Status = ParticipantStatuses.Assigned,
            AssignedBy = _leaderUserId,
            AssignedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        db.VisitParticipants.AddRange(leaderRow, staffRow);
        await db.SaveChangesAsync();

        var item = await LeaderRowAsync(db, vr, vi);
        Assert.NotNull(item);

        // The line still SHOWS the staff member — that part was never wrong.
        Assert.Equal(staffRow.ParticipantId, item!.ParticipantId);
        Assert.Equal(_staffUserId, item.CurrentResponsibleUserId);

        // …and the Leader's own rights come from the Leader's own row.
        Assert.True(item.CanOpenContribution);
        Assert.Equal(leaderRow.ParticipantId, item.CurrentUserParticipantId);
        Assert.Equal(ParticipantStatuses.Accepted, item.CurrentUserParticipantStatus);
    }

    /// <summary>A staff member who accepted the work they were given contributes on their own account.</summary>
    [Fact]
    public async Task Invitation_AcceptedByStaff_OpensContributionForThatStaff()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db);

        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = vi,
            UserId = _staffUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            Status = ParticipantStatuses.Accepted,
            AssignedBy = _leaderUserId,
            AssignedAt = DateTime.Now,
            RespondedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        var handler = new GetAssignmentsProgressListQueryHandler(db,
            new FakeCurrentUser { UserId = _staffUserId, RoleCode = RoleCodes.Department, SubRole = UserSubRoles.Staff, PrimaryCampusId = _campusId, DepartmentId = _departmentId });
        var result = await handler.Handle(
            new GetAssignmentsProgressListQuery { ItemType = "INVITATION", VisitRequestId = vr },
            CancellationToken.None);
        var item = result.Items.FirstOrDefault(x => x.VisitInstanceId == vi);

        Assert.NotNull(item);
        Assert.True(item!.CanOpenContribution);
        Assert.True(item.IsActedByCurrentUser);
    }

    // ── Lifecycle: an unanswered invitation on a past visit is not "Hoàn thành" ──────────────────

    /// <summary>
    /// Nobody answered, the visit came and went. Reporting that as DONE / "Hoàn thành" told the reader
    /// they had taken part and finished — while the row was still INVITED, with no acceptance and no
    /// contribution behind it. It ran out: EXPIRED. The stored status stays INVITED; only the derived
    /// one changes, because "was invited, never answered" is the row's real history.
    /// </summary>
    [Fact]
    public async Task Invitation_NeverAnswered_OnAPastVisit_ReadsExpiredNotDone()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db, DateTime.Now.AddDays(-3), DateTime.Now.AddDays(-3).AddHours(2));

        var p = new VisitParticipant
        {
            VisitInstanceId = vi,
            UserId = _leaderUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            Status = ParticipantStatuses.Invited,
            InvitedAt = DateTime.Now.AddDays(-10),
            CreatedAt = DateTime.Now.AddDays(-10),
        };
        db.VisitParticipants.Add(p);
        await db.SaveChangesAsync();

        var item = await LeaderRowAsync(db, vr, vi);
        Assert.NotNull(item);
        Assert.Equal("EXPIRED", item!.UiStatus);
        Assert.Equal("Hết hạn / Không phản hồi", item.StatusLabel);

        // The raw row is untouched — EXPIRED is derived, never written.
        Assert.Equal(ParticipantStatuses.Invited, item.RawStatus);

        // No participation, so nothing to report; and no button offering an answer the endpoint would
        // now refuse (the campus is long past its preparation window).
        Assert.False(item.CanOpenContribution);
        Assert.False(item.CanAccept);
        Assert.False(item.CanDecline);
    }

    /// <summary>An invitation still inside the visit's window stays actionable, not expired.</summary>
    [Fact]
    public async Task Invitation_NeverAnswered_BeforeTheVisit_StaysRequested()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (vr, vi) = await CreateVisitInstance(db, DateTime.Now.AddDays(5), DateTime.Now.AddDays(5).AddHours(2));

        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = vi,
            UserId = _leaderUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            Status = ParticipantStatuses.Invited,
            InvitedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        var item = await LeaderRowAsync(db, vr, vi);
        Assert.NotNull(item);
        Assert.Equal("REQUESTED", item!.UiStatus);
    }

    /// <summary>
    /// The invitation row this instance produces for the Department Leader.
    ///
    /// Scoped by visit request rather than filtered out of the default page: this department is shared
    /// with every other suite in the run, and the list paginates by soonest start — a fixture dated a few
    /// days out simply falls off page 1 once enough siblings exist, which looks exactly like the row not
    /// being produced at all.
    /// </summary>
    private async Task<AssignmentsProgressItemDto?> LeaderRowAsync(
        ApplicationDbContext db, ulong visitRequestId, ulong visitInstanceId)
    {
        var handler = new GetAssignmentsProgressListQueryHandler(db,
            new FakeCurrentUser { UserId = _leaderUserId, RoleCode = RoleCodes.Department, SubRole = UserSubRoles.Leader, PrimaryCampusId = _campusId, DepartmentId = _departmentId });
        var result = await handler.Handle(
            new GetAssignmentsProgressListQuery { ItemType = "INVITATION", VisitRequestId = visitRequestId },
            CancellationToken.None);
        return result.Items.FirstOrDefault(x => x.VisitInstanceId == visitInstanceId);
    }
}
