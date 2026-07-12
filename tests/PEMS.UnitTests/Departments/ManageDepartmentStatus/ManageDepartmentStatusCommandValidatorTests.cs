using FluentValidation.TestHelper;
using PEMS.Application.Departments.Commands.ManageDepartmentStatus;

namespace PEMS.UnitTests.Departments.ManageDepartmentStatus;

/// <summary>
/// Unit tests for <see cref="ManageDepartmentStatusCommandValidator"/> (UC-106 Manage Department
/// Status). Source-confirmed facts: the command only carries <c>DepartmentId</c> and <c>Status</c>
/// (ACTIVE/INACTIVE); all DB-dependent rules (scope, IC lock, blockers, campus state) live in the
/// handler, so the validator never queries the database.
/// </summary>
public class ManageDepartmentStatusCommandValidatorTests
{
    private readonly ManageDepartmentStatusCommandValidator _validator = new();

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("INACTIVE")]
    public void ValidStatus_NoErrors(string status)
    {
        var command = new ManageDepartmentStatusCommand { DepartmentId = 10, Status = status };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DepartmentId_Zero_HasError()
    {
        var command = new ManageDepartmentStatusCommand { DepartmentId = 0, Status = "ACTIVE" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.DepartmentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("active")]
    [InlineData("LOCKED")]
    [InlineData("DELETED")]
    public void Status_Invalid_HasError(string status)
    {
        var command = new ManageDepartmentStatusCommand { DepartmentId = 10, Status = status };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}
