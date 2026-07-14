using PEMS.Application.Campuses.Common;
using PEMS.Application.Campuses.Queries.GetCampusStatusImpact;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Campuses;

/// <summary>
/// UC-86 §18 status-impact preview: blocker counts/examples come from the SAME calculator as
/// the command guard; enable previews report activation issues + would-be readiness.
/// </summary>
public class GetCampusStatusImpactQueryHandlerTests
{
    private const ulong CampusId = 1;
    private const ulong IcDeptId = 10;

    private readonly FakeCurrentUserService _currentUser = new()
    {
        UserId = 900,
        RoleCode = RoleCodes.Ho,
        SubRole = null,
        PrimaryCampusId = 999,
    };

    private GetCampusStatusImpactQueryHandler CreateHandler(CampusTestDbContext db) =>
        new(db, _currentUser, new RoleAccessPolicy());

    private static CampusTestDbContext CreateContext(string campusStatus = EntityStatuses.Active)
    {
        var db = CampusTestDbContext.Create();
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.StaffRoleId, RoleCodes.Staff));
        db.Campuses.Add(CampusUcTestData.CreateCampus(CampusId, campusStatus));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(IcDeptId, CampusId));
        db.SaveChanges();
        return db;
    }

    private static GetCampusStatusImpactQuery Query(string target, ulong campusId = CampusId) =>
        new() { CampusId = campusId, TargetStatus = target };

    [Fact]
    public async Task DisablePreview_WithBlockers_ReportsCountsByStatusAndExamples()
    {
        using var db = CreateContext();
        db.VisitRequests.Add(CampusUcTestData.CreateVisitRequest(50, "Đoàn A"));
        db.VisitRequestCampuses.Add(CampusUcTestData.CreateVisitInstance(51, 50, CampusId, VisitInstanceStatuses.WaitingRequestApproval));
        db.VisitRequestCampuses.Add(CampusUcTestData.CreateVisitInstance(52, 50, CampusId, VisitInstanceStatuses.Assigned));
        db.VisitRequestCampuses.Add(CampusUcTestData.CreateVisitInstance(53, 50, CampusId, VisitInstanceStatuses.Closed)); // terminal: not counted
        db.SaveChanges();
        var handler = CreateHandler(db);

        var result = await handler.Handle(Query(EntityStatuses.Inactive), CancellationToken.None);

        Assert.False(result.CanChange);
        Assert.Equal(2, result.BlockerCount);
        Assert.Equal(1, result.BlockersByStatus[VisitInstanceStatuses.WaitingRequestApproval]);
        Assert.Equal(1, result.BlockersByStatus[VisitInstanceStatuses.Assigned]);
        Assert.DoesNotContain(VisitInstanceStatuses.Closed, result.BlockersByStatus.Keys);
        var example = result.BlockerExamples[0];
        Assert.Equal("Đoàn A", example.DelegationName);
        Assert.Equal(50UL, example.RequestId);
        Assert.NotNull(example.RequestCode);
    }

    [Fact]
    public async Task DisablePreview_WithOnlyTerminalInstances_CanChange()
    {
        using var db = CreateContext();
        db.VisitRequests.Add(CampusUcTestData.CreateVisitRequest(50));
        db.VisitRequestCampuses.Add(CampusUcTestData.CreateVisitInstance(51, 50, CampusId, VisitInstanceStatuses.Rejected));
        db.SaveChanges();
        var handler = CreateHandler(db);

        var result = await handler.Handle(Query(EntityStatuses.Inactive), CancellationToken.None);

        Assert.True(result.CanChange);
        Assert.Equal(0, result.BlockerCount);
    }

    [Fact]
    public async Task DisablePreview_ExamplesAreCapped()
    {
        using var db = CreateContext();
        db.VisitRequests.Add(CampusUcTestData.CreateVisitRequest(50));
        for (ulong i = 0; i < 8; i++)
            db.VisitRequestCampuses.Add(CampusUcTestData.CreateVisitInstance(60 + i, 50, CampusId, VisitInstanceStatuses.Assigned));
        db.SaveChanges();
        var handler = CreateHandler(db);

        var result = await handler.Handle(Query(EntityStatuses.Inactive), CancellationToken.None);

        Assert.Equal(8, result.BlockerCount);
        Assert.Equal(CampusStatusImpactCalculator.MaxBlockerExamples, result.BlockerExamples.Count);
    }

    [Fact]
    public async Task EnablePreview_MissingMasterDataAndIcDept_ListsIssues()
    {
        using var db = CreateContext(EntityStatuses.Inactive);
        var campus = db.Campuses.Single();
        campus.Phone = null;
        db.Departments.Single().Status = EntityStatuses.Inactive;
        db.SaveChanges();
        var handler = CreateHandler(db);

        var result = await handler.Handle(Query(EntityStatuses.Active), CancellationToken.None);

        Assert.False(result.CanChange);
        Assert.Contains("MASTER_DATA_INCOMPLETE:phone", result.EnableIssues);
        Assert.Contains(CampusReadinessIssues.ActiveIcDepartmentMissing, result.EnableIssues);
    }

    [Fact]
    public async Task EnablePreview_NoLeader_CanChange_ButWouldBeReadinessIsFalse()
    {
        using var db = CreateContext(EntityStatuses.Inactive);
        var handler = CreateHandler(db);

        var result = await handler.Handle(Query(EntityStatuses.Active), CancellationToken.None);

        // BR-86-15: a Staff Leader never gates enable — only the would-be readiness warns.
        Assert.True(result.CanChange);
        Assert.Empty(result.EnableIssues);
        Assert.False(result.Readiness!.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.ActiveStaffLeaderMissing, result.Readiness.ReadinessIssues);
    }

    [Fact]
    public async Task Preview_SameStatus_IsNoOp_CannotChange()
    {
        using var db = CreateContext(); // already ACTIVE
        var handler = CreateHandler(db);

        var result = await handler.Handle(Query(EntityStatuses.Active), CancellationToken.None);

        Assert.False(result.CanChange);
    }

    [Fact]
    public async Task NonHo_IsForbidden()
    {
        using var db = CreateContext();
        _currentUser.RoleCode = RoleCodes.Visitor;
        var handler = CreateHandler(db);

        await Assert.ThrowsAsync<AuthBusinessException>(
            () => handler.Handle(Query(EntityStatuses.Inactive), CancellationToken.None));
    }

    [Fact]
    public async Task UnknownCampus_Is404()
    {
        using var db = CreateContext();
        var handler = CreateHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(Query(EntityStatuses.Inactive, 12345), CancellationToken.None));
    }
}
