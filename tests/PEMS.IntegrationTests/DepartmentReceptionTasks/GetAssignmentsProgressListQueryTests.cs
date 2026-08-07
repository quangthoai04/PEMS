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

    private async Task<(ulong VisitRequestId, ulong VisitInstanceId)> CreateVisitInstance(ApplicationDbContext db)
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
            PlannedStartAt = DateTime.Now,
            PlannedEndAt = DateTime.Now.AddHours(2),
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

    [Fact]
    public async Task LogisticsItem_AssignedAndAcceptedByStaff_ShouldHaveStaffResponsible_AndCanContribute()
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

        // 2. Staff views: Sees themselves responsible, CAN contribute
        var staffHandler = new GetAssignmentsProgressListQueryHandler(db, 
            new FakeCurrentUser { UserId = _staffUserId, RoleCode = RoleCodes.Department, SubRole = UserSubRoles.Staff, PrimaryCampusId = _campusId, DepartmentId = _departmentId });
        var staffResult = await staffHandler.Handle(new GetAssignmentsProgressListQuery { ItemType = "REQUEST" }, CancellationToken.None);
        var itemS = staffResult.Items.FirstOrDefault(x => x.LogisticsItemId == li.LogisticsItemId);
        Assert.NotNull(itemS);
        Assert.Equal(_staffUserId, itemS!.CurrentResponsibleUserId);
        Assert.True(itemS.CanOpenContribution);
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
}
