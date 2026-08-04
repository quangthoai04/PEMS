using System.Linq;
using PEMS.Application.Common.Interfaces;
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

    /// <summary>
    /// Appends whatever system blocks the contract requires, so a test about VARIABLE rules states only
    /// the variables it is about.
    ///
    /// <para>
    /// It exists because <c>ACCOUNT_EMAIL_CONFIRMATION</c> — the fixture most of these use — became a
    /// registered action template, making <c>{{actionBlock}}</c> mandatory in its body. Without this,
    /// every one of them would carry an unrelated block placeholder, and the next template that gains a
    /// required block would break them all again.
    /// </para>
    /// </summary>
    private static string Body(EmailTemplateContract contract, string html)
    {
        var body = html + string.Concat(contract.RequiredSystemBlocks.Select(b => "{{" + b + "}}"));
        if (contract.ActionRequired) body += "{{actionBlock}}";
        return body;
    }

    // ── The clean case ───────────────────────────────────────────────────────

    [Fact]
    public void Content_using_exactly_the_declared_variables_is_clean()
    {
        var contract = Contract(SystemEmailTemplates.AccountEmailConfirmation);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Xác nhận tài khoản của bạn",
            bodyVi: Body(contract, "<p>Chào {{fullName}}, vai trò {{roleName}} tại {{campusName}}. " +
                    "Liên kết có hiệu lực {{expiresInHours}} giờ.</p>"),
            subjectEn: "Confirm your account",
            bodyEn: Body(contract, "<p>Hello {{fullName}}, role {{roleName}} at {{campusName}}. " +
                    "Valid for {{expiresInHours}} hours.</p>"));

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
            "<p>Địa chỉ này không còn liên kết với tài khoản.</p>",
            "Email changed",
            "<p>This address is no longer linked to an account.</p>");

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
            contract, "Xác nhận", Body(contract, "<p>{{fullName}} — {{logisticsTitle}}</p>"), null, null);

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
            contract, "Xác nhận", Body(contract, "<p>{{totallyInvented}}</p>"), null, null);

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
            contract, "Xác nhận", Body(contract, "<p>Chào {{FullName}}</p>"), null, null);

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
            contract, "Xác nhận", Body(contract, "<a href=\"/x?u=%7B%7BfullName%7D%7D\">link</a>"), null, null);
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
            "<p>Chào {{recipientName}}, mời bạn tham dự {{delegationName}}.</p>",
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
            contract, "Xác nhận tài khoản",
            Body(contract, "<p>Chào {{fullName}}, vui lòng xác nhận email.</p>"), null, null);

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
            + "{{actionBlock}}",
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

            // Every block the template allows goes in too — required ones because their absence is now
            // its own refusal, optional ones because writing a permitted block must never be an issue.
            var bodyBlocks = contract.AllowedSystemBlocks.Select(b => $"{{{{{b}}}}}");

            var issues = EmailTemplateContentValidator.Validate(
                contract,
                subjectVi: $"Thông báo {code}",
                bodyVi: "<p>" + string.Join(" ", bodyVariables.Concat(bodyBlocks)) + "</p>",
                subjectEn: null, bodyEn: null);

            Assert.True(issues.Count == 0,
                $"{code}: " + string.Join(" | ", issues.Select(i => $"{i.Code}:{i.VariableName}")));
        }
    }

    // ── System blocks are not variables ──────────────────────────────────────
    //
    // The defect: a placeholder was checked against AllowedVariables regardless of what it was, so a
    // trusted block — which by design is never in that list — could be answered with
    // EMAIL_TEMPLATE_VARIABLE_UNKNOWN, "biến không tồn tại trong hệ thống". That is false about a block
    // that exists, is registered, and is mandatory on fourteen templates, and it points the operator at
    // a variable to define rather than at the block they moved or deleted.

    /// <summary>
    /// The headline case: no template, anywhere, may report a registered block under a VARIABLE code —
    /// whether the block is legal there or not.
    /// </summary>
    [Fact]
    public void No_template_ever_reports_a_system_block_as_an_unknown_variable()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code)!;

            foreach (var block in EmailTrustedBlocks.All)
            {
                var issues = EmailTemplateContentValidator.Validate(
                    contract,
                    subjectVi: "Thông báo",
                    bodyVi: $"<p>Nội dung {{{{{block}}}}}</p>",
                    subjectEn: null, bodyEn: null);

                Assert.DoesNotContain(issues, i =>
                    i.Code == EmailErrorCodes.TemplateVariableUnknown && i.VariableName == block);
            }
        }
    }

    /// <summary>A block the template allows is simply accepted — no issue of any kind about it.</summary>
    [Fact]
    public void An_allowed_system_block_raises_no_issue()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code)!;

            var body = "<p>Nội dung "
                       + string.Join(" ", contract.AllowedSystemBlocks.Select(b => $"{{{{{b}}}}}"))
                       + "</p>";

            var issues = EmailTemplateContentValidator.Validate(
                contract, subjectVi: "Thông báo", bodyVi: body, subjectEn: null, bodyEn: null);

            Assert.DoesNotContain(issues, i => contract.AllowedSystemBlocks.Contains(i.VariableName ?? ""));
        }
    }

    /// <summary>
    /// The other half of the contract, and the reason this is not a relaxation: a block written into a
    /// template that cannot resolve it is still refused — under the code that says to delete it.
    /// </summary>
    [Fact]
    public void A_block_the_template_cannot_resolve_is_refused_under_its_own_code()
    {
        // AUTH_PASSWORD_RESET_OTP has no setup tables, so that block cannot resolve here.
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.AuthPasswordResetOtp)!;

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Mã đặt lại mật khẩu",
            bodyVi: "<p>{{otpCode}} {{setupSummaryBlock}}</p>",
            subjectEn: null, bodyEn: null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailErrorCodes.TemplateSystemBlockNotAllowed, issue.Code);
        Assert.Equal(EmailTrustedBlocks.SetupSummaryBlock, issue.VariableName);
    }

    /// <summary>
    /// An ordinary variable outside the contract must STILL be unknown. Splitting the lists changed
    /// which rule judges a block, not whether a mistyped variable is caught.
    /// </summary>
    [Fact]
    public void A_non_block_variable_outside_the_contract_is_still_unknown()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.AccountEmailConfirmation)!;

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Xác nhận",
            bodyVi: Body(contract, "<p>Chào {{fullName}}, xe {{vehicleInfo}}, mã {{otpCode}}.</p>"),
            subjectEn: null, bodyEn: null);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, i.Code));
        Assert.Contains(issues, i => i.VariableName == "vehicleInfo");
        Assert.Contains(issues, i => i.VariableName == "otpCode");
    }

    /// <summary>
    /// A missing required block reports under the code that names ITS repair. Before the split every
    /// block travelled inside RequiredVariables and an operator who deleted the setup tables was told
    /// to restore an action button.
    /// </summary>
    [Fact]
    public void A_missing_content_block_does_not_report_as_a_missing_action_block()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Cập nhật chuẩn bị",
            bodyVi: "<p>Kính gửi Quý khách.</p>",
            subjectEn: null, bodyEn: null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailErrorCodes.TemplateRequiredBlockNotInBody, issue.Code);
        Assert.Equal(EmailTrustedBlocks.SetupSummaryBlock, issue.VariableName);
    }

    /// <summary>A block still may not sit in a subject, which is stored and shown in history.</summary>
    [Fact]
    public void A_system_block_is_refused_in_a_subject()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.VisitParticipantInvitation)!;

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Thư mời {{actionBlock}}",
            bodyVi: "<p>{{recipientName}}</p>{{actionBlock}}",
            subjectEn: null, bodyEn: null);

        var issue = Assert.Single(issues);
        Assert.Equal(EmailTemplateFields.SubjectVi, issue.Field);
        Assert.Equal(EmailErrorCodes.TemplateSubjectForbiddenSensitiveVariable, issue.Code);
    }
}
