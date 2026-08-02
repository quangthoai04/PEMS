using System.Linq;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// What an operator may and may not write into a template (G11-J).
///
/// <para>
/// The property that matters most here is the FIRST test: canonical content, unedited, must produce no
/// issues at all. The screen this replaces reported "Một số biến chưa được định nghĩa hoặc sai định
/// dạng" on templates nobody had touched, which is worse than an unhelpful message — it teaches
/// operators that the warning means nothing, and then a real one is ignored too.
/// </para>
/// </summary>
public sealed class EmailTemplateContentValidatorTests
{
    private static EmailTemplateContract Contract(string code)
        => EmailTemplateContracts.For(code)!;

    // ── The clean case ───────────────────────────────────────────────────────

    [Fact]
    public void Content_using_exactly_the_declared_variables_is_clean()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Xác nhận tài khoản của bạn",
            bodyVi: "<p>Chào {{fullName}}, vai trò {{roleName}} tại {{campusName}}. " +
                    "Liên kết có hiệu lực {{expiresInHours}} giờ.</p>",
            subjectEn: "Confirm your account",
            bodyEn: "<p>Hello {{fullName}}, role {{roleName}} at {{campusName}}. " +
                    "Valid for {{expiresInHours}} hours.</p>");

        Assert.Empty(issues);
    }

    /// <summary>A static notice with no variables at all is legitimate and must not be flagged.</summary>
    [Fact]
    public void Content_with_no_variables_is_clean_for_a_template_that_declares_none()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailChangedOldNotice);

        // "No variables" still includes the contact block: that is a trusted block the backend fills in,
        // not something an operator supplies. This notice's policy is REQUIRED — its text tells the
        // reader to contact support — so a body without it is a real defect, and the fixture carries it.
        var issues = EmailTemplateContentValidator.Validate(
            contract, "Email đã được thay đổi",
            "<p>Địa chỉ này không còn liên kết với tài khoản.</p>{{contactInformationBlock}}",
            "Email changed",
            "<p>This address is no longer linked to an account.</p>{{contactInformationBlock}}");

        Assert.Empty(issues);
    }

    /// <summary>
    /// A language left empty is not a partial edit; it means that language is not maintained for this
    /// template. Demanding required variables inside it would block every save.
    /// </summary>
    [Fact]
    public void An_empty_language_is_not_judged()
    {
        var contract = Contract(SystemEmailTemplates.AuthPasswordResetOtp);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Mã đặt lại mật khẩu",
            bodyVi: "<p>Chào {{fullName}}, mã của bạn là {{otpCode}}, hiệu lực {{expireMinutes}} phút.</p>",
            subjectEn: null,
            bodyEn: null);

        Assert.Empty(issues);
    }

    // ── Unknown variables ────────────────────────────────────────────────────

    [Fact]
    public void A_variable_from_another_module_is_refused()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        // One of the six the old hard-coded sidebar offered on every template.
        var issues = EmailTemplateContentValidator.Validate(
            contract, "Xác nhận", "<p>{{fullName}} — {{logisticsTitle}}</p>", null, null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, issue.Code);
        Assert.Equal("logisticsTitle", issue.VariableName);
        Assert.Equal(EmailTemplateFields.BodyVi, issue.Field);
        Assert.True(issue.IsError);
    }

    [Fact]
    public void A_variable_that_exists_nowhere_is_refused()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var issues = EmailTemplateContentValidator.Validate(
            contract, "Xác nhận", "<p>{{totallyInvented}}</p>", null, null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, issue.Code);
        Assert.Equal("totallyInvented", issue.VariableName);
    }

    /// <summary>
    /// Casing is significant, and deliberately so: the renderer matches case-sensitively, so
    /// <c>{{FullName}}</c> would reach a recipient as literal braces. Reporting it is the only way the
    /// operator finds out before the send does.
    /// </summary>
    [Fact]
    public void A_variable_with_the_wrong_casing_is_refused_rather_than_quietly_accepted()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var issues = EmailTemplateContentValidator.Validate(
            contract, "Xác nhận", "<p>Chào {{FullName}}</p>", null, null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailErrorCodes.TemplateVariableMalformed, issue.Code);
        Assert.Equal("FullName", issue.VariableName);
    }

    [Fact]
    public void A_snake_case_variable_is_refused()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var issues = EmailTemplateContentValidator.Validate(
            contract, "Xác nhận", "<p>Chào {{full_name}}</p>", null, null);

        Assert.Contains(issues, i => i.Code == EmailErrorCodes.TemplateVariableMalformed);
    }

    /// <summary>A rich editor stores placeholders URL-encoded inside an href; those count too.</summary>
    [Fact]
    public void Url_encoded_placeholders_are_read_the_same_way()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var clean = EmailTemplateContentValidator.Validate(
            contract, "Xác nhận", "<a href=\"/x?u=%7B%7BfullName%7D%7D\">link</a>", null, null);
        Assert.Empty(clean);

        var dirty = EmailTemplateContentValidator.Validate(
            contract, "Xác nhận", "<a href=\"/x?u=%7B%7BlogisticsTitle%7D%7D\">link</a>", null, null);
        Assert.Contains(dirty, i => i.VariableName == "logisticsTitle");
    }

    // ── Required variables ───────────────────────────────────────────────────

    [Fact]
    public void Removing_the_otp_from_an_otp_email_is_refused()
    {
        var contract = Contract(SystemEmailTemplates.AuthPasswordResetOtp);

        var issues = EmailTemplateContentValidator.Validate(
            contract, "Đặt lại mật khẩu", "<p>Chào {{fullName}}, vui lòng kiểm tra ứng dụng.</p>", null, null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailErrorCodes.TemplateRequiredVariableMissing, issue.Code);
        Assert.Equal("otpCode", issue.VariableName);
    }

    [Fact]
    public void Removing_the_action_block_from_an_invitation_is_refused()
    {
        var contract = Contract(SystemEmailTemplates.VisitParticipantInvitation);

        // The contact block stays in the fixture so this test still isolates ONE fault. Dropping both
        // would raise two issues and stop saying anything about the action block in particular.
        var issues = EmailTemplateContentValidator.Validate(
            contract, "Thư mời",
            "<p>Chào {{recipientName}}, mời bạn tham dự {{delegationName}}.</p>{{contactInformationBlock}}",
            null, null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailErrorCodes.TemplateActionBlockRequired, issue.Code);
        Assert.Equal("actionBlock", issue.VariableName);
    }

    /// <summary>
    /// The other half, and the reason "required" is defined narrowly: an operator rewording a sentence
    /// so that it no longer mentions the campus is ordinary editing, not a defect, and must save.
    /// </summary>
    [Fact]
    public void Removing_an_optional_variable_is_allowed()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var issues = EmailTemplateContentValidator.Validate(
            contract, "Xác nhận tài khoản", "<p>Chào {{fullName}}, vui lòng xác nhận email.</p>", null, null);

        Assert.Empty(issues);
    }

    [Fact]
    public void An_action_block_kept_in_the_body_satisfies_the_requirement()
    {
        var contract = Contract(SystemEmailTemplates.VisitParticipantInvitation);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            "Thư mời tham dự",
            "<p>Chào {{recipientName}}, mời bạn tham dự {{delegationName}}.</p>"
            + "{{contactInformationBlock}}{{actionBlock}}",
            null, null);

        Assert.Empty(issues);
    }

    // ── Subjects ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A subject IS stored in <c>sent_emails</c> and shown in the history screen, so a code placed
    /// there is persisted, backed up and readable long afterwards by anyone with history access.
    /// </summary>
    [Fact]
    public void An_otp_in_the_subject_is_refused()
    {
        var contract = Contract(SystemEmailTemplates.AuthPasswordResetOtp);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Mã của bạn: {{otpCode}}",
            bodyVi: "<p>Chào {{fullName}}, mã {{otpCode}} hiệu lực {{expireMinutes}} phút.</p>",
            subjectEn: null, bodyEn: null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailErrorCodes.TemplateSubjectForbiddenSensitiveVariable, issue.Code);
        Assert.Equal(EmailTemplateFields.SubjectVi, issue.Field);
        Assert.Equal("otpCode", issue.VariableName);
    }

    [Fact]
    public void An_action_block_in_the_subject_is_refused()
    {
        var contract = Contract(SystemEmailTemplates.VisitParticipantInvitation);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Thư mời {{actionBlock}}",
            bodyVi: "<p>{{recipientName}}</p>{{actionBlock}}",
            subjectEn: null, bodyEn: null);

        Assert.Contains(issues, i =>
            i.Code == EmailErrorCodes.TemplateSubjectForbiddenSensitiveVariable &&
            i.Field == EmailTemplateFields.SubjectVi);
    }

    // ── Field addressing ─────────────────────────────────────────────────────

    /// <summary>
    /// Each issue names the field it belongs to. The screen has four content inputs; an issue that does
    /// not say which one leaves the operator searching the whole template.
    /// </summary>
    [Fact]
    public void Issues_are_addressed_to_the_field_that_carries_them()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Xin chào {{logisticsTitle}}",
            bodyVi: "<p>{{fullName}} {{dueAt}}</p>",
            subjectEn: "Hello {{quantity}}",
            bodyEn: "<p>{{fullName}}</p>");

        Assert.Equal(EmailTemplateFields.SubjectVi,
            Assert.Single(issues, i => i.VariableName == "logisticsTitle").Field);
        Assert.Equal(EmailTemplateFields.BodyVi,
            Assert.Single(issues, i => i.VariableName == "dueAt").Field);
        Assert.Equal(EmailTemplateFields.SubjectEn,
            Assert.Single(issues, i => i.VariableName == "quantity").Field);
    }

    [Fact]
    public void Every_issue_carries_both_languages_and_a_stable_code()
    {
        var contract = Contract(SystemEmailTemplates.AuthPasswordResetOtp);

        var issues = EmailTemplateContentValidator.Validate(
            contract, "Mã {{otpCode}}", "<p>{{unknownThing}}</p>", null, null);

        Assert.NotEmpty(issues);
        foreach (var issue in issues)
        {
            Assert.False(string.IsNullOrWhiteSpace(issue.Code));
            Assert.False(string.IsNullOrWhiteSpace(issue.MessageVi));
            Assert.False(string.IsNullOrWhiteSpace(issue.MessageEn));
            Assert.False(string.IsNullOrWhiteSpace(issue.Field));
            Assert.StartsWith("EMAIL_TEMPLATE_", issue.Code);
        }
    }

    /// <summary>
    /// Every problem at once, not the first. An operator fixing four things one round trip at a time is
    /// how a five-minute edit becomes a twenty-minute one.
    /// </summary>
    [Fact]
    public void All_problems_are_reported_together()
    {
        var contract = Contract(SystemEmailTemplates.AuthPasswordResetOtp);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Mã {{otpCode}}",                        // sensitive in subject
            bodyVi: "<p>{{logisticsTitle}} {{FullName}}</p>",   // unknown + wrong casing, and no otpCode
            subjectEn: null, bodyEn: null);

        Assert.Contains(issues, i => i.Code == EmailErrorCodes.TemplateSubjectForbiddenSensitiveVariable);
        Assert.Contains(issues, i => i.Code == EmailErrorCodes.TemplateVariableUnknown);
        Assert.Contains(issues, i => i.Code == EmailErrorCodes.TemplateVariableMalformed);
        Assert.True(issues.Count >= 3, $"expected several issues, got {issues.Count}");
    }

    [Fact]
    public void The_exception_carries_every_issue_and_the_first_error_code()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var issues = EmailTemplateContentValidator.Validate(
            contract, "Xin chào", "<p>{{logisticsTitle}}</p>", null, null);

        var ex = new EmailTemplateContentException(issues);

        Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, ex.ErrorCode);
        Assert.Equal(issues.Count, ex.Issues.Count);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    // ── The whole canonical catalog ──────────────────────────────────────────

    /// <summary>
    /// Content built from a template's OWN contract must validate for every template in the catalog.
    /// A template whose declared variables cannot survive its own validator is a template whose editor
    /// nobody can use.
    /// </summary>
    [Fact]
    public void Every_template_accepts_content_built_from_its_own_contract()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code)!;

            var bodyVariables = contract.AllowedVariables
                .Where(v => !contract.ForbiddenInSubject.Contains(v) || contract.RequiredVariables.Contains(v))
                .Select(v => $"{{{{{v}}}}}");

            var issues = EmailTemplateContentValidator.Validate(
                contract,
                subjectVi: $"Thông báo {code}",
                bodyVi: "<p>" + string.Join(" ", bodyVariables) + "</p>",
                subjectEn: null, bodyEn: null);

            Assert.True(issues.Count == 0,
                $"{code}: " + string.Join(" | ", issues.Select(i => $"{i.Code}:{i.VariableName}")));
        }
    }
}
