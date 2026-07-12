using FluentValidation.TestHelper;
using PEMS.Application.Departments.Commands.UpdateDepartment;
using Xunit;

namespace PEMS.UnitTests.Departments.UpdateDepartment;

/// <summary>
/// Unit tests for <see cref="UpdateDepartmentCommandValidator"/> (UC-102 Update Department).
///
/// Source-confirmed facts: <see cref="UpdateDepartmentCommand"/> only carries <c>DepartmentId</c>
/// and <c>Name</c> — no DepartmentType, HeadUserId or Status field exists on the command (campus,
/// type, head and status are never touched by this UC per the handler's own doc comment), so this
/// validator only has rules for DepartmentId and Name.
/// </summary>
public class UpdateDepartmentCommandValidatorTests
{
    private readonly UpdateDepartmentCommandValidator _validator = new();

    private static UpdateDepartmentCommand ValidCommand(ulong departmentId = 1, string name = "Phòng Công nghệ Thông tin") =>
        new() { DepartmentId = departmentId, Name = name };

    [Fact]
    public void ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // DepartmentId is ulong, so a negative value cannot even be constructed — zero is the only
    // real invalid boundary (RuleFor(x => x.DepartmentId).GreaterThan(0UL)).
    [Fact]
    public void DepartmentId_Zero_HasError()
    {
        var command = ValidCommand(departmentId: 0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.DepartmentId);
    }

    [Fact]
    public void Name_Null_HasError()
    {
        var command = ValidCommand(name: null!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Empty_HasError()
    {
        var command = ValidCommand(name: "");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Whitespace_HasError()
    {
        var command = ValidCommand(name: "   ");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_TooLong_HasError()
    {
        var command = ValidCommand(name: new string('A', 151));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_MaxLength_NoError()
    {
        // Validator checks Trim().Length <= 150, so surrounding whitespace must not count
        // against the limit — exactly 150 significant chars framed by extra spaces.
        var command = ValidCommand(name: "  " + new string('A', 150) + "  ");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
