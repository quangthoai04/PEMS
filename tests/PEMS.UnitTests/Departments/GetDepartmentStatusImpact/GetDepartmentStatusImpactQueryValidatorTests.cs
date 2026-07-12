using FluentValidation.TestHelper;
using PEMS.Application.Departments.Queries.GetDepartmentStatusImpact;

namespace PEMS.UnitTests.Departments.GetDepartmentStatusImpact;

/// <summary>
/// Unit tests for <see cref="GetDepartmentStatusImpactQueryValidator"/> (UC-106 impact preview).
/// Mirrors the ManageDepartmentStatus rules: DepartmentId &gt; 0, NewStatus ACTIVE/INACTIVE only.
/// </summary>
public class GetDepartmentStatusImpactQueryValidatorTests
{
    private readonly GetDepartmentStatusImpactQueryValidator _validator = new();

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("INACTIVE")]
    public void ValidQuery_NoErrors(string newStatus)
    {
        var query = new GetDepartmentStatusImpactQuery { DepartmentId = 10, NewStatus = newStatus };
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DepartmentId_Zero_HasError()
    {
        var query = new GetDepartmentStatusImpactQuery { DepartmentId = 0, NewStatus = "INACTIVE" };
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.DepartmentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("inactive")]
    [InlineData("UNKNOWN")]
    public void NewStatus_Invalid_HasError(string newStatus)
    {
        var query = new GetDepartmentStatusImpactQuery { DepartmentId = 10, NewStatus = newStatus };
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.NewStatus);
    }
}
