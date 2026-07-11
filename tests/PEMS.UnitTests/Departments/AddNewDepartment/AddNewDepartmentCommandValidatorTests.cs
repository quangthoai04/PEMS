using FluentValidation.TestHelper;
using PEMS.Application.Departments.Commands.AddNewDepartment;
using Xunit;

namespace PEMS.UnitTests.Departments.AddNewDepartment;

/// <summary>
/// Unit tests for <see cref="AddNewDepartmentCommandValidator"/> (UC-101 Add New Department).
///
/// Source-confirmed facts: <see cref="AddNewDepartmentCommand"/> only carries <c>Name</c> — no
/// CampusId, DepartmentType, HeadUserId or Status field exists on the command (they are all
/// server-populated in the handler), so this validator only has rules for Name. Rule: required
/// after trim, and <c>Trim().Length &lt;= 150</c>.
/// </summary>
public class AddNewDepartmentCommandValidatorTests
{
    private readonly AddNewDepartmentCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_NoErrors()
    {
        var command = new AddNewDepartmentCommand { Name = "Phòng Công nghệ Thông tin" };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Name_Null_HasError()
    {
        var command = new AddNewDepartmentCommand { Name = null! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Empty_HasError()
    {
        var command = new AddNewDepartmentCommand { Name = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Whitespace_HasError()
    {
        var command = new AddNewDepartmentCommand { Name = "   " };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_TooLong_HasError()
    {
        var command = new AddNewDepartmentCommand { Name = new string('A', 151) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_MaxLength_NoError()
    {
        // Validator checks Trim().Length <= 150, so surrounding whitespace must not count
        // against the limit — exactly 150 significant chars framed by extra spaces.
        var command = new AddNewDepartmentCommand { Name = "  " + new string('A', 150) + "  " };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
