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

    private static UpdateEmailTemplateCommand NewUpdate(string purpose) => new()
    {
        EmailTemplateId = 1,
        Name = "Template",
        Purpose = purpose,
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

    [Theory]
    [InlineData(OtpPurpose.VisitRequestVerify)]
    [InlineData(OtpPurpose.ChangeSensitiveAction)]
    public void Update_accepts_the_two_storable_purposes(string purpose)
    {
        var result = new UpdateEmailTemplateCommandValidator().Validate(NewUpdate(purpose));

        Assert.True(result.IsValid);
    }

    /// <summary>An update must not be able to blank out a column the database declares NOT NULL.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SOMETHING_ELSE")]
    public void Update_rejects_a_purpose_the_column_cannot_store(string? purpose)
    {
        var result = new UpdateEmailTemplateCommandValidator().Validate(NewUpdate(purpose!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateEmailTemplateCommand.Purpose));
    }
}
