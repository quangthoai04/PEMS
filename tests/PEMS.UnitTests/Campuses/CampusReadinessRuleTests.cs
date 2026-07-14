using PEMS.Application.Campuses.Common;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Campuses;

/// <summary>UC-86 §8 pure classification matrix (doc §28.2).</summary>
public class CampusReadinessRuleTests
{
    [Fact]
    public void ActiveCampus_WithOneIcDept_AndOneLeader_IsAvailable()
    {
        var r = CampusReadinessRule.Evaluate(EntityStatuses.Active, activeIcDepartmentCount: 1, validStaffLeaderCount: 1);

        Assert.True(r.IsAvailableForVisitRegistration);
        Assert.True(r.ActiveIcDepartmentExists);
        Assert.True(r.ActiveStaffLeaderExists);
        Assert.Empty(r.ReadinessIssues);
    }

    [Fact]
    public void InactiveCampus_IsNotAvailable()
    {
        var r = CampusReadinessRule.Evaluate(EntityStatuses.Inactive, 1, 1);

        Assert.False(r.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.CampusInactive, r.ReadinessIssues);
    }

    [Fact]
    public void NoActiveIcDepartment_IsNotAvailable()
    {
        var r = CampusReadinessRule.Evaluate(EntityStatuses.Active, 0, 1);

        Assert.False(r.IsAvailableForVisitRegistration);
        Assert.False(r.ActiveIcDepartmentExists);
        Assert.Contains(CampusReadinessIssues.ActiveIcDepartmentMissing, r.ReadinessIssues);
    }

    [Fact]
    public void MultipleActiveIcDepartments_IsConfigurationError_NotAvailable()
    {
        var r = CampusReadinessRule.Evaluate(EntityStatuses.Active, 2, 1);

        Assert.False(r.IsAvailableForVisitRegistration);
        Assert.False(r.ActiveIcDepartmentExists);
        Assert.Contains(CampusReadinessIssues.MultipleActiveIcDepartments, r.ReadinessIssues);
    }

    [Fact]
    public void NoValidLeader_IsNotAvailable()
    {
        var r = CampusReadinessRule.Evaluate(EntityStatuses.Active, 1, 0);

        Assert.False(r.IsAvailableForVisitRegistration);
        Assert.False(r.ActiveStaffLeaderExists);
        Assert.Contains(CampusReadinessIssues.ActiveStaffLeaderMissing, r.ReadinessIssues);
    }

    [Fact]
    public void MultipleValidLeaders_IsConfigurationError_NotAvailable()
    {
        var r = CampusReadinessRule.Evaluate(EntityStatuses.Active, 1, 2);

        Assert.False(r.IsAvailableForVisitRegistration);
        Assert.False(r.ActiveStaffLeaderExists);
        Assert.Contains(CampusReadinessIssues.MultipleActiveStaffLeaders, r.ReadinessIssues);
    }

    [Fact]
    public void IcHeadMappingInconsistent_IsWarningOnly_StaysAvailable()
    {
        var r = CampusReadinessRule.Evaluate(EntityStatuses.Active, 1, 1, icHeadMappingConsistent: false);

        Assert.True(r.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.IcHeadMappingInconsistent, r.ReadinessIssues);
    }
}
