using System;
using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The one variable contract (G11-J).
///
/// <para>
/// The defect these tests pin was not subtle once you knew where to look: the template-management screen
/// carried its own list of eleven variables — five "common", six "logistics" — and validated whatever
/// template was open against it. <c>ACCOUNT_EMAIL_CONFIRMATION</c> declares fullName, roleName,
/// campusName and expiresInHours; none appeared in that list, so an untouched canonical template opened
/// with a warning on every variable it legitimately used, while the sidebar simultaneously offered
/// logistics variables it can never receive a value for.
/// </para>
/// </summary>
public sealed class EmailTemplateContractTests
{
    // ── The catalog must describe the whole registry ─────────────────────────

    /// <summary>
    /// Every variable any template declares must have a label and a sample. A missing entry is not
    /// cosmetic: the sidebar would show the raw name, and the preview would substitute the name in place
    /// of a value, so the operator would be looking at "fullName" where a person's name belongs.
    /// </summary>
    [Fact]
    public void Catalog_describes_every_variable_the_registry_declares()
    {
        var undescribed = SystemEmailTemplates.All
            .SelectMany(t => t.DeclaredVariables)
            .Distinct(StringComparer.Ordinal)
            .Where(name => EmailVariableCatalog.Find(name) is null)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(undescribed.Count == 0,
            "These declared variables have no label/sample in EmailVariableCatalog: " +
            string.Join(", ", undescribed));
    }

    /// <summary>
    /// And nothing extra: a catalog entry no template declares would be offered in a sidebar as though
    /// it were usable, and a save that used it would then be refused.
    /// </summary>
    [Fact]
    public void Catalog_describes_nothing_the_registry_does_not_declare()
    {
        var declared = SystemEmailTemplates.All
            .SelectMany(t => t.DeclaredVariables)
            .ToHashSet(StringComparer.Ordinal);

        var orphans = EmailVariableCatalog.AllNames
            .Where(name => !declared.Contains(name))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(orphans.Count == 0,
            "These catalog entries belong to no template: " + string.Join(", ", orphans));
    }

    [Fact]
    public void Every_registered_template_resolves_a_contract()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
            Assert.NotNull(EmailTemplateContracts.For(code));
    }

    [Fact]
    public void An_unregistered_code_resolves_no_contract()
    {
        Assert.Null(EmailTemplateContracts.For("NOT_A_SYSTEM_TEMPLATE"));
        Assert.Null(EmailTemplateContracts.For(null));
    }

    // ── The specific template the report named ───────────────────────────────

    /// <summary>
    /// The template whose editor was reported as showing false warnings. Its four declared variables
    /// must all be allowed, plus the action block — which is legal on every template because the
    /// backend, not the operator, supplies it. This template has no registry action spec, so the block
    /// is permitted but not demanded.
    /// </summary>
    [Fact]
    public void Account_email_confirmation_allows_its_declared_variables_and_the_action_block()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.AccountEmailConfirmation);

        Assert.NotNull(contract);

        // Data variables only. The action block is legal on this template but belongs to the block
        // lists, not here — mixing the two is what let a block be reported as an unknown VARIABLE.
        Assert.Equal(
            new[] { "campusName", "expiresInHours", "fullName", "roleName" },
            contract!.AllowedVariables.OrderBy(v => v, StringComparer.Ordinal).ToArray());

        // Registered on 2026-08-03: this mail's whole purpose is the confirm button, so the block is
        // required and the preview shows the real button rather than a neutral placeholder.
        Assert.True(contract.ActionRequired);
        Assert.DoesNotContain(EmailTrustedBlocks.ActionBlock, contract.AllowedVariables);
        Assert.Contains(EmailTrustedBlocks.ActionBlock, contract.RequiredSystemBlocks);
        Assert.DoesNotContain(EmailTrustedBlocks.ActionBlock, contract.OptionalSystemBlocks);
    }

    /// <summary>
    /// No trusted block may appear in a variable list, on any template. This is the invariant the
    /// content validator relies on to decide that a placeholder it is looking at is a variable at all.
    /// </summary>
    [Fact]
    public void No_variable_list_ever_contains_a_trusted_block()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code)!;

            foreach (var block in EmailTrustedBlocks.All)
            {
                Assert.DoesNotContain(block, contract.AllowedVariables);
                Assert.DoesNotContain(block, contract.RequiredVariables);
                Assert.DoesNotContain(block, contract.OptionalVariables);
            }
        }
    }

    /// <summary>Every block a template allows is reported in exactly one of the two block lists.</summary>
    [Fact]
    public void Required_and_optional_blocks_do_not_overlap()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code)!;

            Assert.Empty(contract.RequiredSystemBlocks.Intersect(contract.OptionalSystemBlocks));

            foreach (var block in contract.AllowedSystemBlocks)
                Assert.True(contract.AllowsSystemBlock(block));
        }
    }

    /// <summary>
    /// <c>{{actionBlock}}</c> is legal in a body only where the registry declares an action spec, and
    /// illegal in a SUBJECT everywhere — including on the templates whose body may not carry it either.
    ///
    /// <para>
    /// The two rules are asserted together because they were briefly conflated: deriving the
    /// forbidden-in-subject list from the blocks a body may carry silently dropped the subject rule from
    /// every template without a spec. "May not be stored in a subject" is about where a one-time link
    /// ends up, and has nothing to do with whether this template has buttons.
    /// </para>
    /// </summary>
    [Fact]
    public void The_action_block_is_a_legal_placeholder_ONLY_on_action_templates()
    {
        var withSpec = 0;
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code)!;
            if (contract.ActionSupported) withSpec++;

            Assert.Equal(contract.ActionSupported, contract.AllowsSystemBlock(EmailTrustedBlocks.ActionBlock));
            Assert.Equal(contract.ActionSupported, contract.ActionRequired);
            Assert.Contains(EmailTrustedBlocks.ActionBlock, contract.ForbiddenInSubject);
        }

        // Both sides of the rule are exercised: some templates have a spec and some do not.
        Assert.InRange(withSpec, 1, SystemEmailTemplates.AllCodes.Count - 1);
    }

    /// <summary>
    /// A shipped body carries <c>{{contactInformationBlock}}</c> in BOTH languages exactly where the
    /// reply-contact policy is REQUIRED, and nowhere the policy renders no block at all.
    ///
    /// <para>
    /// The failure this pins is asymmetric and both halves are real. Missing where REQUIRED: the
    /// renderer refuses the send outright, so the template cannot be used. Present where the policy
    /// renders nothing: the placeholder resolves to empty at best, and at worst an operator "fixes" a
    /// block the template was never meant to show.
    /// </para>
    /// <para>
    /// It is stated over the shipped defaults because those are what a restore, and the generated sync
    /// script, write into a database. The database-side half lives in EmailTemplateSyncScriptTests.
    /// </para>
    /// </summary>
    [Fact]
    public void A_shipped_body_carries_the_contact_block_exactly_where_the_policy_requires_one()
    {
        var marker = "{{" + EmailTrustedBlocks.ContactInformationBlock + "}}";
        var offenders = new List<string>();

        foreach (var code in EmailTemplateDefaults.AllCodes)
        {
            var shipped = EmailTemplateDefaults.For(code)!;
            var policy = PEMS.Application.Emails.Contact.EmailContactPolicyDefaults.For(code);

            var inVi = (shipped.BodyVi ?? "").Contains(marker, StringComparison.Ordinal);
            var inEn = (shipped.BodyEn ?? "").Contains(marker, StringComparison.Ordinal);

            if (policy.Requirement == PEMS.Domain.Enums.EmailContactRequirement.REQUIRED)
            {
                if (!inVi) offenders.Add($"{code}.body_vi: policy REQUIRED but no {marker}");
                if (!inEn) offenders.Add($"{code}.body_en: policy REQUIRED but no {marker}");
            }
            else if (!policy.RendersBlock && (inVi || inEn))
            {
                offenders.Add($"{code}: policy renders no contact block, body carries {marker}");
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The registry and the shipped wording agree, in both directions: a body writes
    /// <c>{{actionBlock}}</c> exactly when its template has an action spec.
    ///
    /// <para>
    /// Both halves are failures with a face. A body WITHOUT a spec asks for a block no send path fills,
    /// so the render is refused and the template cannot be saved. A spec WITHOUT the placeholder is
    /// worse and quieter: the send path builds a real block with a one-time link, the body has nowhere
    /// to put it, and the substitution is simply skipped — the recipient gets a mail asking them to
    /// confirm with no button, and nothing anywhere reports an error.
    /// </para>
    /// <para>
    /// Unit-level on purpose. The equivalent database check lives in EmailPreviewCoverageTests, but the
    /// shipped defaults are what a restore writes INTO the database, so catching it here is catching it
    /// before it can be installed.
    /// </para>
    /// </summary>
    [Fact]
    public void A_shipped_body_writes_the_action_block_exactly_when_the_registry_declares_one()
    {
        var offenders = EmailTemplateDefaults.AllCodes
            .Select(code =>
            {
                var shipped = EmailTemplateDefaults.For(code)!;
                var marker = "{{" + EmailTrustedBlocks.ActionBlock + "}}";
                var inVi = (shipped.BodyVi ?? "").Contains(marker, StringComparison.Ordinal);
                var inEn = (shipped.BodyEn ?? "").Contains(marker, StringComparison.Ordinal);
                var registered = EmailActionTemplates.For(code) is not null;

                if (inVi != inEn)
                    return $"{code}: the block is in {(inVi ? "body_vi" : "body_en")} only";

                return inVi == registered
                    ? null
                    : $"{code}: body {(inVi ? "writes" : "does not write")} the block, registry "
                      + $"{(registered ? "declares" : "declares no")} action";
            })
            .Where(x => x is not null)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Account_email_confirmation_offers_no_logistics_variables()
    {
        var contract = EmailTemplateContracts.Describe(
            SystemEmailTemplates.AccountEmailConfirmation, EmailLanguages.Vi);

        Assert.NotNull(contract);

        // The six the old hard-coded sidebar showed on every template.
        foreach (var logistics in new[]
                 {
                     "logisticsTitle", "departmentName", "departmentLeaderName",
                     "requesterName", "usageStartAt", "usageEndAt",
                 })
        {
            Assert.DoesNotContain(contract!.Variables, v => v.Name == logistics);
        }
    }

    [Fact]
    public void Account_email_confirmation_variables_all_carry_a_label_and_a_sample()
    {
        var contract = EmailTemplateContracts.Describe(
            SystemEmailTemplates.AccountEmailConfirmation, EmailLanguages.Vi);

        Assert.NotNull(contract);
        Assert.NotEmpty(contract!.Variables);

        foreach (var v in contract.Variables)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Label), $"{v.Name} has no label");
            Assert.False(string.IsNullOrWhiteSpace(v.Sample), $"{v.Name} has no sample");
            Assert.NotEqual(v.Name, v.Label);   // the label is a label, not the raw name
        }
    }

    // ── Sensitivity and copies ───────────────────────────────────────────────

    /// <summary>
    /// A message carrying a one-time code or a personal action link addresses exactly one person: a CC
    /// or BCC hands a second person a credential minted for the first.
    /// </summary>
    [Fact]
    public void No_secret_bearing_template_permits_copies()
    {
        // Lambda rather than a method group: `For` takes an optional contact-requirement argument, so a
        // method group would bind to Select's (item, index) overload and pass the index as a policy.
        var offenders = SystemEmailTemplates.AllCodes
            .Select(code => EmailTemplateContracts.For(code))
            .Where(c => c!.CarriesSecret && (c.AllowCc || c.AllowBcc))
            .Select(c => c!.TemplateCode)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These templates carry a secret but permit copies: " + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("AUTH_PASSWORD_RESET_OTP")]
    [InlineData("VISIT_REQUEST_OTP")]
    [InlineData("VISIT_CONTACT_CLAIM")]
    [InlineData("VISIT_CONTACT_TRANSFER")]
    [InlineData("VISIT_PARTICIPANT_INVITATION")]
    [InlineData("VISIT_STUDENT_INVITATION")]
    [InlineData("VISIT_DEPARTMENT_LEADER_INVITATION")]
    [InlineData("LOGISTICS_REQUEST_TO_DEPARTMENT")]
    [InlineData("LOGISTICS_ASSIGNEE_ASSIGNMENT")]
    public void Named_sensitive_templates_forbid_cc_and_bcc(string code)
    {
        var contract = EmailTemplateContracts.For(code);

        Assert.NotNull(contract);
        Assert.False(contract!.AllowCc, $"{code} must not allow CC");
        Assert.False(contract.AllowBcc, $"{code} must not allow BCC");
        Assert.Equal(EmailTemplateContracts.ClassificationSensitive, contract.SecurityClassification);
    }

    /// <summary>The report templates are operational documents; the caller owns their distribution list.</summary>
    [Theory]
    [InlineData("REPORT_CAMPUS_OPERATION")]
    [InlineData("REPORT_DEPARTMENT_COLLABORATION")]
    [InlineData("REPORT_DEPARTMENT_INVOICE")]
    [InlineData("REPORT_PERSONNEL_PERFORMANCE")]
    public void Report_templates_permit_copies(string code)
    {
        var contract = EmailTemplateContracts.For(code);

        Assert.NotNull(contract);
        Assert.True(contract!.AllowCc);
        Assert.True(contract.AllowBcc);
        Assert.Equal(EmailTemplateContracts.ClassificationStandard, contract.SecurityClassification);
    }

    [Fact]
    public void The_otp_variable_is_forbidden_in_every_subject_that_may_carry_it()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code)!;
            if (!contract.AllowedVariables.Contains("otpCode")) continue;

            Assert.Contains("otpCode", contract.ForbiddenInSubject);
            Assert.Contains("otpCode", contract.SensitiveVariables);
            Assert.Contains("otpCode", contract.RequiredVariables);
        }
    }

    // ── Action block ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("VISIT_PARTICIPANT_INVITATION")]
    [InlineData("VISIT_STUDENT_INVITATION")]
    [InlineData("VISIT_DEPARTMENT_LEADER_INVITATION")]
    [InlineData("LOGISTICS_ASSIGNEE_ASSIGNMENT")]
    [InlineData("LOGISTICS_REQUEST_TO_DEPARTMENT")]
    public void Action_templates_require_and_allow_the_action_block(string code)
    {
        var contract = EmailTemplateContracts.For(code);

        Assert.NotNull(contract);
        Assert.True(contract!.ActionRequired);
        Assert.True(contract.AllowsSystemBlock(EmailTrustedBlocks.ActionBlock));
        Assert.Contains(EmailTrustedBlocks.ActionBlock, contract.RequiredSystemBlocks);
    }

    /// <summary>
    /// The action block is a trusted block the backend injects — it must never appear in the sidebar as
    /// something an operator supplies, and a caller must never be able to pass one as a variable.
    /// </summary>
    [Fact]
    public void The_action_block_is_never_offered_as_an_editable_variable()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var described = EmailTemplateContracts.Describe(code, EmailLanguages.Vi)!;
            Assert.DoesNotContain(described.Variables, v => v.Name == EmailTrustedBlocks.ActionBlock);

            var sample = EmailTemplateContracts.PreviewSample(code, EmailLanguages.Vi);
            Assert.False(sample.ContainsKey(EmailTrustedBlocks.ActionBlock));
        }
    }

    // ── Preview samples ──────────────────────────────────────────────────────

    [Fact]
    public void Preview_sample_covers_every_variable_a_template_can_use()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code)!;
            var sample = EmailTemplateContracts.PreviewSample(code, EmailLanguages.Vi);

            foreach (var name in contract.AllowedVariables)
            {
                // No trusted block has a sample: the preview handler supplies them as inert markup,
                // and passing one as a variable is refused outright.
                if (EmailTrustedBlocks.All.Contains(name)) continue;
                Assert.True(sample.ContainsKey(name),
                    $"{code}: preview has no sample for {{{{{name}}}}}, so a preview would leave it unresolved.");
            }
        }
    }

    [Theory]
    [InlineData("VI")]
    [InlineData("EN")]
    public void No_preview_sample_looks_like_a_real_credential(string language)
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            foreach (var (name, value) in EmailTemplateContracts.PreviewSample(code, language))
            {
                Assert.DoesNotContain("://", value);          // no URL, real or fabricated
                Assert.DoesNotContain("javascript:", value, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("<", value);            // no markup

                if (name == "otpCode")
                {
                    // Fixed and obviously fake. A preview that minted a plausible code would be a
                    // preview that leaks one the moment somebody screenshots it.
                    Assert.Equal("000000", value);
                }
            }
        }
    }

    [Fact]
    public void Samples_differ_between_languages_where_the_wording_does()
    {
        var vi = EmailTemplateContracts.PreviewSample("REPORT_CAMPUS_OPERATION", EmailLanguages.Vi);
        var en = EmailTemplateContracts.PreviewSample("REPORT_CAMPUS_OPERATION", EmailLanguages.En);

        Assert.Equal(vi.Keys.OrderBy(k => k), en.Keys.OrderBy(k => k));
        Assert.NotEqual(vi["campusName"], en["campusName"]);
    }

    // ── Editable fields ──────────────────────────────────────────────────────

    /// <summary>
    /// The whitelist, asserted as data. Anything not here is registry-owned; the update command does not
    /// carry it and the screen renders it as read-only.
    /// </summary>
    [Fact]
    public void Only_content_fields_are_editable()
    {
        Assert.Equal(
            new[] { "bodyEn", "bodyVi", "description", "name", "subjectEn", "subjectVi" },
            EmailTemplateContracts.EditableFieldNames.OrderBy(f => f, StringComparer.Ordinal).ToArray());
    }
}
