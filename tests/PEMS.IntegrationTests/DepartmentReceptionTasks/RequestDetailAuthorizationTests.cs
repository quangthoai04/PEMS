using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetRequestDetail;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.DepartmentReceptionTasks;

/// <summary>
/// DB-AUTHZ-001: <see cref="GetRequestDetailQueryHandler"/> used to have no
/// <see cref="ICurrentUserService"/> at all, so any authenticated user of any department could read
/// another department's logistics request detail by guessing the id — while every write sibling on the
/// same controller (ConfirmRequestCommand, RejectRequestCommand, ...) already checked
/// <c>RequestedToDepartmentId</c>. These tests prove the read now enforces the same boundary.
/// </summary>
public sealed class RequestDetailAuthorizationTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-REQUEST-DETAIL-AUTHZ] ";

    private readonly PemsWebApplicationFactory _factory;
    private ulong _campusId;
    private ulong _departmentAId;
    private ulong _departmentBId;
    private ulong _deptALeaderUserId;
    private ulong _deptBLeaderUserId;

    public RequestDetailAuthorizationTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var departmentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department).Select(r => r.RoleId).FirstAsync();

        _campusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active)
            .OrderBy(c => c.CampusId).Select(c => c.CampusId).FirstAsync();

        var deptIds = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == _campusId && d.DepartmentType == "GENERAL" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).Select(d => d.DepartmentId).Take(2).ToListAsync();
        Assert.True(deptIds.Count >= 2, "Test data needs at least 2 GENERAL departments on the same campus.");
        _departmentAId = deptIds[0];
        _departmentBId = deptIds[1];

        var suffix = Guid.NewGuid().ToString("N")[..8];
        User Make(string name, string mail, ulong departmentId) => new()
        {
            FullName = $"{TestPrefix}{name}",
            Email = $"{mail}_{suffix}@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = _campusId,
            DepartmentId = departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };

        var deptALeader = Make("DeptALeader", "dalead", _departmentAId);
        var deptBLeader = Make("DeptBLeader", "dblead", _departmentBId);
        db.Users.AddRange(deptALeader, deptBLeader);
        await db.SaveChangesAsync();

        _deptALeaderUserId = deptALeader.UserId;
        _deptBLeaderUserId = deptBLeader.UserId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ulong> CreateLogisticsItemAsync(ApplicationDbContext db, ulong departmentId)
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
            CampusId = _campusId,
            OperationalContactUserId = _deptALeaderUserId,
            PlannedStartAt = DateTime.Now.AddDays(5),
            PlannedEndAt = DateTime.Now.AddDays(5).AddHours(2),
            Status = VisitInstanceStatuses.WaitingRequestApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();

        // Pure V2: GetRequestDetail resolves content through IVisitFormReadService, which requires
        // exactly one detail row per instance.
        db.VisitInstanceFormDetails.Add(new VisitInstanceFormDetail
        {
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = "Đoàn khách test",
            VisitType = "MEETING",
            Purpose = "Purpose",
            WorkingContent = "Content",
            OperationalContactFullName = "Contact",
            OperationalContactJobTitle = "Job",
            OperationalContactEmail = "contact@pems.test",
            WorkingLanguage = "EN",
            MediaConsentStatus = "AGREED",
            FormRevision = 1,
            ApprovalRevision = 1,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        var li = new VisitLogisticsItem
        {
            VisitInstanceId = instance.VisitInstanceId,
            RequestedToDepartmentId = departmentId,
            Status = "REQUESTED",
            Title = "Cần máy chiếu",
            ItemType = "EQUIPMENT",
            CreatedAt = DateTime.Now,
        };
        db.VisitLogisticsItems.Add(li);
        await db.SaveChangesAsync();

        return li.LogisticsItemId;
    }

    private FakeCurrentUser AsUser(ulong userId, ulong departmentId) => new()
    {
        UserId = userId,
        RoleCode = RoleCodes.Department,
        SubRole = UserSubRoles.Leader,
        PrimaryCampusId = _campusId,
        DepartmentId = departmentId,
    };

    private GetRequestDetailQueryHandler Handler(ApplicationDbContext db, IServiceScope scope, ICurrentUserService user)
        => new(db, scope.ServiceProvider.GetRequiredService<IVisitFormReadService>(), user);

    [Fact]
    public async Task SameDepartment_IsAllowed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var itemId = await CreateLogisticsItemAsync(db, _departmentAId);

        var dto = await Handler(db, scope, AsUser(_deptALeaderUserId, _departmentAId))
            .Handle(new GetRequestDetailQuery { LogisticsItemId = itemId }, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(itemId, dto.LogisticsItemId);
    }

    /// <summary>The hole this closes: any authenticated department user could read another
    /// department's logistics request detail by guessing the id.</summary>
    [Fact]
    public async Task CrossDepartment_IsForbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var itemId = await CreateLogisticsItemAsync(db, _departmentAId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Handler(db, scope, AsUser(_deptBLeaderUserId, _departmentBId))
                .Handle(new GetRequestDetailQuery { LogisticsItemId = itemId }, CancellationToken.None));
    }

    [Fact]
    public async Task Unauthenticated_IsForbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var itemId = await CreateLogisticsItemAsync(db, _departmentAId);

        var anon = new FakeCurrentUser { IsAuthenticated = false };
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Handler(db, scope, anon)
                .Handle(new GetRequestDetailQuery { LogisticsItemId = itemId }, CancellationToken.None));
    }
}
