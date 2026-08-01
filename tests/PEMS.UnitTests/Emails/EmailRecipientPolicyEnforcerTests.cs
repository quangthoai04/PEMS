using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The gate that stops a one-time credential reaching a second person.
///
/// A CC or BCC on an OTP, a password-reset code or an invitation with a personal accept link does not
/// merely copy someone in — it gives them a token minted for somebody else. These tests assert the rule
/// holds for every registered template, not just the handful a developer remembered.
/// </summary>
public class EmailRecipientPolicyEnforcerTests
{
    private static EmailRecipient R(string email) => new(email);

    private static ValidatedEnvelope Envelope(
        int to = 1, int cc = 0, int bcc = 0)
        => new(
            Enumerable.Range(1, to).Select(i => R($"to{i}@fpt.edu.vn")).ToList(),
            Enumerable.Range(1, cc).Select(i => R($"cc{i}@fpt.edu.vn")).ToList(),
            Enumerable.Range(1, bcc).Select(i => R($"bcc{i}@fpt.edu.vn")).ToList());

    /// <summary>Every template whose policy is one recipient, no copies.</summary>
    public static TheoryData<string> SingleRecipientCodes()
    {
        var data = new TheoryData<string>();
        foreach (var t in SystemEmailTemplates.All.Where(t => !t.AllowsCopies))
            data.Add(t.TemplateCode);
        return data;
    }

    /// <summary>Every template whose recipient list the caller owns.</summary>
    public static TheoryData<string> CallerControlledCodes()
    {
        var data = new TheoryData<string>();
        foreach (var t in SystemEmailTemplates.All.Where(t => t.AllowsCopies))
            data.Add(t.TemplateCode);
        return data;
    }

    [Theory]
    [MemberData(nameof(SingleRecipientCodes))]
    public void Single_recipient_template_rejects_CC(string code)
    {
        var ex = Assert.Throws<BusinessRuleException>(
            () => EmailRecipientPolicyEnforcer.Assert(code, Envelope(cc: 1)));

        Assert.Equal(EmailErrorCodes.RecipientTypeNotAllowed, ex.ErrorCode);
    }

    [Theory]
    [MemberData(nameof(SingleRecipientCodes))]
    public void Single_recipient_template_rejects_BCC(string code)
    {
        var ex = Assert.Throws<BusinessRuleException>(
            () => EmailRecipientPolicyEnforcer.Assert(code, Envelope(bcc: 1)));

        Assert.Equal(EmailErrorCodes.RecipientTypeNotAllowed, ex.ErrorCode);
    }

    [Theory]
    [MemberData(nameof(SingleRecipientCodes))]
    public void Single_recipient_template_rejects_more_than_one_TO(string code)
    {
        var ex = Assert.Throws<BusinessRuleException>(
            () => EmailRecipientPolicyEnforcer.Assert(code, Envelope(to: 2)));

        Assert.Equal(EmailErrorCodes.RecipientTypeNotAllowed, ex.ErrorCode);
    }

    [Theory]
    [MemberData(nameof(SingleRecipientCodes))]
    public void Single_recipient_template_accepts_exactly_one_TO(string code)
        => EmailRecipientPolicyEnforcer.Assert(code, Envelope(to: 1));

    [Theory]
    [MemberData(nameof(CallerControlledCodes))]
    public void Caller_controlled_template_accepts_copies(string code)
        => EmailRecipientPolicyEnforcer.Assert(code, Envelope(to: 2, cc: 1, bcc: 1));

    [Fact]
    public void User_authored_mail_has_no_template_and_is_unrestricted()
    {
        // Compose/draft/reply: the sender owns the envelope.
        EmailRecipientPolicyEnforcer.Assert(null, Envelope(to: 3, cc: 2, bcc: 2));
        EmailRecipientPolicyEnforcer.Assert(string.Empty, Envelope(to: 3, cc: 2, bcc: 2));
    }

    [Fact]
    public void An_unregistered_code_is_treated_as_user_authored_rather_than_blocking_the_send()
        => EmailRecipientPolicyEnforcer.Assert("NOT_A_REGISTERED_TEMPLATE", Envelope(to: 2, cc: 1));

    // ── Registry shape ───────────────────────────────────────────────────────

    [Fact]
    public void Every_template_carrying_a_sensitive_action_is_single_recipient()
    {
        var offenders = SystemEmailTemplates.All
            .Where(t => t.HasSensitiveAction && t.AllowsCopies)
            .Select(t => t.TemplateCode)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Templates carrying an OTP/one-time token must never allow copies: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_templates_carrying_a_secret_are_exactly_these_twelve()
    {
        // Listed by name on purpose. `HasSensitiveAction` decides two things at once — no copies, and
        // how much of the body the history may keep — so adding a token-bearing template without the
        // flag would quietly start writing a live credential into a column the history API serves to
        // every internal role. That mistake should show up here as a failing list, not in production.
        var omitted = SystemEmailTemplates.All
            .Where(t => t.HasSensitiveAction)
            .Select(t => t.TemplateCode)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                SystemEmailTemplates.AccountEmailConfirmation,
                SystemEmailTemplates.AuthPasswordResetOtp,
                SystemEmailTemplates.LogisticsAssigneeAssignment,
                SystemEmailTemplates.LogisticsChangeProposalToHost,
                SystemEmailTemplates.LogisticsRequestToDepartment,
                SystemEmailTemplates.VisitContactClaim,
                SystemEmailTemplates.VisitContactTransfer,
                SystemEmailTemplates.VisitDepartmentLeaderInvitation,
                SystemEmailTemplates.VisitDepartmentStaffAssignment,
                SystemEmailTemplates.VisitParticipantInvitation,
                SystemEmailTemplates.VisitRequestOtp,
                SystemEmailTemplates.VisitStudentInvitation,
            }.OrderBy(c => c, StringComparer.Ordinal).ToArray(),
            omitted);
    }

    [Fact]
    public void Every_declared_variable_is_classified_as_secret_or_not()
    {
        // The union of the two sets must cover the registry. This is the mechanism that makes a NEW
        // variable a deliberate decision: add one and this test fails until somebody says which side of
        // the line it falls on, rather than it defaulting to "safe" because nobody looked.
        var declared = SystemEmailTemplates.All
            .SelectMany(t => t.DeclaredVariables)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        var unclassified = declared
            .Where(v => !SensitiveEmailVariables.Names.Contains(v)
                        && !SensitiveEmailVariables.KnownNonSensitive.Contains(v))
            .ToList();

        Assert.True(unclassified.Count == 0,
            "Chưa phân loại bí mật/không-bí-mật cho biến: " + string.Join(", ", unclassified));

        // And nothing is claimed to be classified that no template actually declares.
        var orphans = SensitiveEmailVariables.Names
            .Concat(SensitiveEmailVariables.KnownNonSensitive)
            .Where(v => !declared.Contains(v, StringComparer.Ordinal))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        Assert.True(orphans.Count == 0,
            "Biến được phân loại nhưng không template nào khai báo: " + string.Join(", ", orphans));
    }

    [Fact]
    public void A_template_declaring_a_credential_variable_must_be_marked_sensitive()
    {
        var offenders = SystemEmailTemplates.All
            .Where(t => SensitiveEmailVariables.DeclaredBy(t).Count > 0 && !t.HasSensitiveAction)
            .Select(t => t.TemplateCode)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Template khai báo biến bí mật nhưng chưa đánh HasSensitiveAction: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_history_policy_follows_the_classification_and_nothing_else()
    {
        // Three cases, decided from the template's own data rather than a list of codes:
        //   • not sensitive                 → keep the body
        //   • sensitive, credential VARIABLE → keep nothing (the secret IS the text)
        //   • sensitive, credential in a LINK → keep the body with the action block removed
        foreach (var template in SystemEmailTemplates.All)
        {
            var expected =
                !SensitiveEmailVariables.CarriesSecret(template) ? HistoryBodyPolicy.Full
                : SensitiveEmailVariables.DeclaredBy(template).Count > 0 ? HistoryBodyPolicy.None
                : HistoryBodyPolicy.ActionBlockStripped;

            Assert.Equal(expected, SensitiveEmailHistory.PolicyFor(template.TemplateCode));
        }
    }

    [Fact]
    public void Only_the_two_code_bearing_templates_withhold_their_body_entirely()
    {
        // Named on purpose: these are the ones where a redacted record would be an empty one. Any new
        // template that interpolates a code lands here too — and this list is what says so out loud.
        var withheld = SystemEmailTemplates.All
            .Where(t => SensitiveEmailHistory.OmitsBody(t.TemplateCode))
            .Select(t => t.TemplateCode)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { SystemEmailTemplates.AuthPasswordResetOtp, SystemEmailTemplates.VisitRequestOtp }
                .OrderBy(c => c, StringComparer.Ordinal).ToArray(),
            withheld);
    }

    [Fact]
    public void User_authored_mail_keeps_its_body()
    {
        // An unregistered code is somebody's own message; the system has no template to redact it against.
        Assert.Equal(HistoryBodyPolicy.Full, SensitiveEmailHistory.PolicyFor(null));
        Assert.Equal(HistoryBodyPolicy.Full, SensitiveEmailHistory.PolicyFor("NOT_A_TEMPLATE"));
    }

    [Fact]
    public void A_template_that_declared_a_credential_without_the_flag_would_be_caught()
    {
        // The registry is a fixed set, so the guard is exercised on a hypothetical entry built the same
        // way a real one is. If somebody adds a template like this, the two invariants above go red.
        var careless = new SystemEmailTemplate(
            "AUTH_SOMETHING_NEW",
            EmailTemplatePurposes.Auth,
            EmailRecipientPolicy.SingleRecipientNoCopies,
            HasSensitiveAction: false,                       // ← the mistake
            new[] { "fullName", "otpCode" });

        Assert.NotEmpty(SensitiveEmailVariables.DeclaredBy(careless));
        Assert.True(SensitiveEmailVariables.CarriesSecret(careless));
        // …while the flag says otherwise, which is exactly the disagreement the invariants assert on.
        Assert.False(careless.HasSensitiveAction);
    }

    [Theory]
    [InlineData("otpCode", true)]
    [InlineData(EmailTrustedBlocks.ActionBlock, true)]   // a one-time URL by construction
    [InlineData("fullName", false)]
    [InlineData("requestCode", false)]                   // a reference people quote, not a credential
    [InlineData("expireMinutes", false)]
    public void The_subject_ban_covers_credentials_and_trusted_blocks_only(string placeholder, bool forbidden)
        => Assert.Equal(forbidden, SensitiveEmailVariables.ForbiddenInSubject(placeholder));

    [Fact]
    public void Every_registered_template_uses_a_purpose_from_the_email_catalog()
    {
        var offenders = SystemEmailTemplates.All
            .Where(t => !EmailTemplatePurposes.IsValid(t.Purpose))
            .Select(t => $"{t.TemplateCode}={t.Purpose}")
            .ToList();

        Assert.True(offenders.Count == 0, "Unknown purpose: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_declared_variable_is_lower_camel_case()
    {
        var offenders = SystemEmailTemplates.All
            .SelectMany(t => t.DeclaredVariables.Select(v => (t.TemplateCode, v)))
            .Where(x => !System.Text.RegularExpressions.Regex.IsMatch(x.v, "^[a-z][A-Za-z0-9]*$"))
            .Select(x => $"{x.TemplateCode}.{x.v}")
            .ToList();

        Assert.True(offenders.Count == 0, "Variables must be lower camelCase: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_declared_variable_is_an_action_url()
    {
        // Action URLs are minted by the backend and injected as a trusted block. Declaring one as an
        // editable variable would let a template author move, remove or fake the button carrying a token.
        var reserved = new[]
        {
            "acceptUrl", "declineUrl", "assignUrl", "detailUrl", "negotiateUrl",
            "approveProposalUrl", "rejectProposalUrl", "confirmBorrowUrl", "confirmReturnUrl",
        };

        var offenders = SystemEmailTemplates.All
            .SelectMany(t => t.DeclaredVariables.Select(v => (t.TemplateCode, v)))
            .Where(x => reserved.Contains(x.v, StringComparer.OrdinalIgnoreCase))
            .Select(x => $"{x.TemplateCode}.{x.v}")
            .ToList();

        Assert.True(offenders.Count == 0, "Action URLs must not be template variables: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Registry_holds_the_agreed_number_of_templates()
        // Catalog decision DL-02: every code has a real production caller. 26 + the 4 DEPT_* codes
        // added when the Department-Leader personnel module stopped composing its own HTML and moved
        // onto the dispatcher (disable / enable / leadership granted / leadership handed over)
        // + VISIT_SETUP_PROGRESS_UPDATE, the Host's manual preparation update to the guest.
        => Assert.Equal(31, SystemEmailTemplates.AllCodes.Count);

    [Fact]
    public void Registry_has_no_duplicate_codes()
        => Assert.Equal(SystemEmailTemplates.AllCodes.Count, SystemEmailTemplates.AllCodes.Distinct().Count());
}
