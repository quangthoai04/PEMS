using PEMS.Application.Emails.Commands.CreateEmailTemplate;
using PEMS.Application.Emails.Commands.UpdateEmailTemplate;
using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// GAP-017: <c>email_templates.purpose</c> is <c>ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')
/// NOT NULL</c>, but the entity mapped it as optional and neither command validated it. A create or update
/// without a purpose therefore reached MySQL and came back as a 500 with no usable message.
///
/// These tests pin both halves of the contract: the two legal values are accepted, and everything the
/// database would reject is refused up front instead.
/// </summary>
public sealed class EmailTemplatePurposeValidationTests
{
    private static CreateEmailTemplateCommand NewCreate(string purpose) => new()
    {
        TemplateCode = "TPL_TEST",
        Name = "Template",
        Purpose = purpose,
    };

    private static UpdateEmailTemplateCommand NewUpdate() => new()
    {
        EmailTemplateId = 1,
        Name = "Template",
        SubjectVi = "Tiêu đề",
        BodyVi = "<p>Nội dung</p>",
        ExpectedRevision = 1,
    };

    [Theory]
    [InlineData(OtpPurpose.VisitRequestVerify)]
    [InlineData(OtpPurpose.ChangeSensitiveAction)]
    public void Create_accepts_the_two_storable_purposes(string purpose)
    {
        var result = new CreateEmailTemplateCommandValidator().Validate(NewCreate(purpose));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]        // the null the column forbids
    [InlineData("")]          // empty is not a member of the ENUM either
    [InlineData("   ")]
    [InlineData("SOMETHING_ELSE")]
    public void Create_rejects_a_purpose_the_column_cannot_store(string? purpose)
    {
        var result = new CreateEmailTemplateCommandValidator().Validate(NewCreate(purpose!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmailTemplateCommand.Purpose));
    }

    /// <summary>
    /// The update side of GAP-017 is now closed more strongly than by validation (G11-I): an update
    /// cannot set <c>purpose</c> at all, because the property no longer exists on the command. The module
    /// a template belongs to is registry-owned, so a request that tries to move a template between
    /// modules has nothing to bind to and the stored value is untouched.
    ///
    /// <para>
    /// Asserted through reflection rather than by "the code does not compile if you try": a property
    /// re-added in the future would silently restore mass assignment, and this is the test that would
    /// fail. The two cases the old rule covered — blanking the NOT NULL column, and writing a value
    /// outside the ENUM — are both unreachable now, which is why they are not re-tested here.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Purpose")]
    [InlineData("CampusId")]
    [InlineData("Status")]
    [InlineData("TemplateCode")]
    [InlineData("BodyFormat")]
    [InlineData("VariablesText")]
    public void Update_command_does_not_expose_a_registry_owned_field(string propertyName)
    {
        Assert.Null(typeof(UpdateEmailTemplateCommand).GetProperty(propertyName));
    }

    [Fact]
    public void Update_accepts_a_content_only_edit()
    {
        var result = new UpdateEmailTemplateCommandValidator().Validate(NewUpdate());

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }
}
