using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Email;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Batch 10 — the closure contract for Gate G4: the catalog is exactly 31 templates, the code registry
/// and the database seed agree in both directions, and every one of the 31 renders in both languages
/// from the database alone.
///
/// <para>
/// These are the tests that make G4 hold over time rather than on the day it was declared. Each earlier
/// batch proved its own callers; nothing until now proved the SET — that no template was quietly added,
/// dropped, renamed, half-translated or left without a caller. That is the drift this file exists to
/// catch, which is why it asserts on the whole catalog instead of on a list of files.
/// </para>
/// </summary>
public sealed class SystemEmailG4ClosureTests
{
    // 31 since VISIT_SETUP_PROGRESS_UPDATE joined the REPORT group: the Host's manual preparation
    // update to the guest side, which carries the Schedule Report as its attachment.
    private const int CatalogSize = 31;

    /// <summary>The catalog as the plan fixed it. Written out so a rename cannot pass silently.</summary>
    private static readonly string[] Catalog =
    {
        "ACCOUNT_EMAIL_CONFIRMATION", "ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE", "ACCOUNT_ACTIVATED",
        "ACCOUNT_EMAIL_CHANGED_OLD_NOTICE", "ACCOUNT_EMAIL_CHANGED_NEW_NOTICE", "ACCOUNT_ROLE_CHANGED",
        "ACCOUNT_STAFF_LEADER_ASSIGNED", "ACCOUNT_STAFF_LEADER_REPLACED",
        // Added when the Department-Leader personnel module stopped composing its own HTML and moved
        // onto the dispatcher, so its six notices became templates like every other system email.
        "DEPT_PERSONNEL_ACCOUNT_DISABLED", "DEPT_PERSONNEL_ACCOUNT_ENABLED",
        "DEPT_LEADERSHIP_GRANTED", "DEPT_LEADERSHIP_HANDED_OVER",
        "AUTH_PASSWORD_RESET_OTP",
        "VISIT_REQUEST_OTP", "VISIT_CONTACT_CLAIM", "VISIT_CONTACT_TRANSFER",
        "VISIT_PARTICIPANT_INVITATION", "VISIT_STUDENT_INVITATION",
        "VISIT_DEPARTMENT_LEADER_INVITATION", "VISIT_DEPARTMENT_STAFF_ASSIGNMENT",
        "LOGISTICS_REQUEST_TO_DEPARTMENT", "LOGISTICS_ASSIGNEE_ASSIGNMENT",
        "LOGISTICS_CHANGE_PROPOSAL_TO_HOST", "LOGISTICS_EXPENSE_REPORT_REMINDER",
        "VISIT_REMINDER_HOST", "VISIT_REMINDER_PARTICIPANTS",
        "REPORT_CAMPUS_OPERATION", "REPORT_DEPARTMENT_COLLABORATION",
        "REPORT_DEPARTMENT_INVOICE", "REPORT_PERSONNEL_PERFORMANCE",
        // The Host's manual "cập nhật chuẩn bị" to the guest side. REPORT rather than
        // VISIT_PARTICIPANT because it distributes a document to a list the Host controls; it bears no
        // token, which is what makes CC legal on it at all.
        "VISIT_SETUP_PROGRESS_UPDATE",
    };

    /// <summary>
    /// Template codes retired before this programme. They must stay out of the ACTIVE set — an operator
    /// re-activating one would give a caller-less template a live edit surface.
    /// </summary>
    private static readonly string[] Legacy =
    {
        "ACCOUNT_CREATED_INTERNAL", "VISIT_REQUEST_APPROVED", "VISIT_REQUEST_REJECTED", "VISIT_CANCELLED",
        "HOST_ASSIGNMENT", "LOGISTICS_REQUEST", "OTP_VISIT_REQUEST", "VISIT_REQUEST_SUBMITTED_NOTIFY",
        "LOGISTICS_REQUEST_SUBMITTED_NOTIFY", "VISIT_INVITATION", "NEWS_REVIEW",
        "VISIT_STUDENT_SUPPORT_INVITATION",
    };

    private static async Task<List<PEMS.Domain.Entities.Emails.EmailTemplate>> ActiveAsync()
    {
        using var db = EmailEvidenceHarness.NewContext();
        return await db.EmailTemplates.AsNoTracking().Where(t => t.Status == "ACTIVE").ToListAsync();
    }

    // ── The set ─────────────────────────────────────────────────────────────

    [Fact]
    public void The_registry_holds_exactly_the_agreed_catalog()
    {
        Assert.Equal(CatalogSize, SystemEmailTemplates.AllCodes.Count);
        Assert.Equal(CatalogSize, SystemEmailTemplates.AllCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Catalog.OrderBy(c => c, StringComparer.Ordinal),
            SystemEmailTemplates.AllCodes.OrderBy(c => c, StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_active_seed_holds_exactly_the_agreed_catalog()
    {
        EmailEvidenceHarness.RequireDb();
        var active = await ActiveAsync();

        Assert.Equal(CatalogSize, active.Count);
        Assert.Equal(
            Catalog.OrderBy(c => c, StringComparer.Ordinal),
            active.Select(t => t.TemplateCode).OrderBy(c => c, StringComparer.Ordinal));
    }

    /// <summary>
    /// Both directions. One direction alone would let an orphan through: a registry entry with no seed
    /// row fails at send time, and a seeded ACTIVE row with no registry entry is a template the operator
    /// can edit while nothing ever sends it.
    /// </summary>
    [Fact]
    public async Task The_registry_and_the_active_seed_agree_in_both_directions()
    {
        EmailEvidenceHarness.RequireDb();
        var seeded = (await ActiveAsync()).Select(t => t.TemplateCode).ToHashSet(StringComparer.Ordinal);
        var registered = SystemEmailTemplates.AllCodes.ToHashSet(StringComparer.Ordinal);

        Assert.Empty(registered.Except(seeded));   // registered but never seeded
        Assert.Empty(seeded.Except(registered));   // seeded and active but unregistered
    }

    [Fact]
    public async Task No_retired_template_is_active()
    {
        EmailEvidenceHarness.RequireDb();
        var active = (await ActiveAsync()).Select(t => t.TemplateCode).ToHashSet(StringComparer.Ordinal);

        foreach (var legacy in Legacy)
        {
            Assert.False(active.Contains(legacy), $"Retired template '{legacy}' is ACTIVE again.");
            Assert.Null(SystemEmailTemplates.Find(legacy));
        }
    }

    // ── Every template's content ────────────────────────────────────────────

    [Fact]
    public async Task Every_template_carries_both_languages_and_declares_its_variables_cleanly()
    {
        EmailEvidenceHarness.RequireDb();

        foreach (var row in await ActiveAsync())
        {
            var code = row.TemplateCode;
            Assert.False(string.IsNullOrWhiteSpace(row.SubjectVi), $"{code}: missing VI subject");
            Assert.False(string.IsNullOrWhiteSpace(row.BodyVi), $"{code}: missing VI body");
            Assert.False(string.IsNullOrWhiteSpace(row.SubjectEn), $"{code}: missing EN subject");
            Assert.False(string.IsNullOrWhiteSpace(row.BodyEn), $"{code}: missing EN body");

            var declared = SystemEmailTemplates.Find(code)!.DeclaredVariables;
            Assert.Equal(declared.Count, declared.Distinct(StringComparer.Ordinal).Count());
            foreach (var name in declared)
            {
                Assert.Matches("^[a-z][A-Za-z0-9]*$", name);   // lower camelCase, no snake/Pascal drift
            }

            // Every placeholder in either language is either a declared variable or a trusted block —
            // never a name only one side knows about.
            foreach (var language in new[] { row.SubjectVi + row.BodyVi, row.SubjectEn + row.BodyEn })
            {
                foreach (Match m in Regex.Matches(language ?? string.Empty, @"\{\{\s*(\w+)\s*\}\}"))
                {
                    var placeholder = m.Groups[1].Value;
                    Assert.True(
                        declared.Contains(placeholder) || EmailTrustedBlocks.All.Contains(placeholder),
                        $"{code}: placeholder '{placeholder}' is neither declared nor a trusted block.");
                }
            }
        }
    }

    /// <summary>
    /// Renders all 31 in both languages from the database, with exactly their declared variables. This
    /// is the test that would have caught the reminder templates shipping "{{plannedEnd}}" as literal
    /// text to recipients.
    /// </summary>
    [Theory]
    [InlineData(EmailLanguages.Vi)]
    [InlineData(EmailLanguages.En)]
    public async Task Every_template_renders_with_no_placeholder_left_behind(string language)
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var renderer = new EmailTemplateRenderer(db);

        foreach (var template in SystemEmailTemplates.All)
        {
            var variables = template.DeclaredVariables.ToDictionary(
                v => v, v => $"[{v}]", StringComparer.Ordinal);
            // Every trusted block, for every template. Which blocks a template writes is content the
            // catalog owns and this test does not track; supplying them all keeps "no placeholder left
            // behind" a statement about the CATALOG rather than about this dictionary being up to date.
            // A block a template does not use is simply never substituted.
            var trusted = EmailTrustedBlocks.All.ToDictionary(
                name => name,
                name => name == EmailTrustedBlocks.ActionBlock
                    ? EmailComposition.ActionBlockStart + "<div>block</div>" + EmailComposition.ActionBlockEnd
                    : "<div>block</div>",
                StringComparer.Ordinal);

            var rendered = await renderer.RenderAsync(
                new EmailRenderRequest(template.TemplateCode, language, variables, trusted));

            Assert.DoesNotContain("{{", rendered.Subject);
            Assert.DoesNotContain("{{", rendered.Body);
            Assert.Equal(language, rendered.LanguageUsed);

            // A declared variable must reach the output — except the sender names, which every capable
            // template declares all six of while its shipped wording prints only some. Declaring the
            // full set is what lets an operator add {{senderPhone}} without a re-seed; requiring the
            // shipped body to print it would defeat that. The no-placeholder-left-behind assertion above
            // still covers them: if one were declared and NOT substituted, "{{" would survive.
            foreach (var v in template.DeclaredVariables)
            {
                if (PEMS.Application.Emails.Sender.EmailSenderVariableNames.IsSenderVariable(v)) continue;
                Assert.Contains($"[{v}]", rendered.Subject + rendered.Body);
            }
        }
    }

    // ── The rules that must not soften ──────────────────────────────────────

    [Fact]
    public async Task A_missing_or_unexpected_variable_stops_the_render()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var renderer = new EmailTemplateRenderer(db);
        var template = SystemEmailTemplates.Find(SystemEmailTemplates.ReportCampusOperation)!;

        var complete = template.DeclaredVariables.ToDictionary(v => v, v => "x", StringComparer.Ordinal);

        var missing = new Dictionary<string, string>(complete);
        missing.Remove("periodTo");
        var missingEx = await Assert.ThrowsAnyAsync<Exception>(() => renderer.RenderAsync(
            new EmailRenderRequest(template.TemplateCode, EmailLanguages.Vi, missing)));
        Assert.Contains(EmailErrorCodes.TemplateVariableMissing, ErrorCodeOf(missingEx));

        var extra = new Dictionary<string, string>(complete) { ["somethingElse"] = "x" };
        var extraEx = await Assert.ThrowsAnyAsync<Exception>(() => renderer.RenderAsync(
            new EmailRenderRequest(template.TemplateCode, EmailLanguages.Vi, extra)));
        Assert.Contains(EmailErrorCodes.TemplateVariableUnknown, ErrorCodeOf(extraEx));
    }

    private static string ErrorCodeOf(Exception ex) => ex switch
    {
        PEMS.Application.Common.Exceptions.BusinessRuleException b => b.ErrorCode ?? string.Empty,
        PEMS.Application.Common.Exceptions.ValidationException v => v.ErrorCode ?? string.Empty,
        PEMS.Application.Common.Exceptions.ConflictException c => c.ErrorCode ?? string.Empty,
        PEMS.Application.Common.Exceptions.NotFoundException n => n.ErrorCode ?? string.Empty,
        _ => ex.Message,
    };

    /// <summary>
    /// Recipient and retention policy come from the template's own classification. Asserting the shape
    /// rather than a hand-written list is what keeps a new template from silently defaulting to the
    /// permissive answer.
    /// </summary>
    [Fact]
    public void Policy_metadata_matches_what_each_group_requires()
    {
        foreach (var template in SystemEmailTemplates.All)
        {
            var code = template.TemplateCode;

            // Anything carrying a one-time link or a personal action goes to exactly one person.
            if (template.HasSensitiveAction)
            {
                Assert.Equal(EmailRecipientPolicy.SingleRecipientNoCopies, template.RecipientPolicy);
                Assert.False(template.AllowsCopies, $"{code}: sensitive mail must not allow copies.");
                Assert.NotEqual(HistoryBodyPolicy.Full, SensitiveEmailHistory.PolicyFor(code));
            }
            else
            {
                // Nothing in it grants access, so the history may keep the whole body.
                Assert.Equal(HistoryBodyPolicy.Full, SensitiveEmailHistory.PolicyFor(code));
            }

            // A credential interpolated into the text means the body must not be stored at all.
            var carriesCredential = SensitiveEmailVariables.DeclaredBy(template).Count > 0;
            Assert.Equal(
                carriesCredential ? HistoryBodyPolicy.None : SensitiveEmailHistory.PolicyFor(code),
                SensitiveEmailHistory.PolicyFor(code));
        }
    }

    [Fact]
    public void The_two_otp_templates_keep_no_body_and_the_reminders_keep_all_of_it()
    {
        Assert.Equal(HistoryBodyPolicy.None,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.AuthPasswordResetOtp));
        Assert.Equal(HistoryBodyPolicy.None,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitRequestOtp));

        Assert.Equal(HistoryBodyPolicy.Full,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitReminderHost));
        Assert.Equal(HistoryBodyPolicy.Full,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitReminderParticipants));
    }

    /// <summary>
    /// The two invoice senders share one template and the two performance senders share another. A sixth
    /// REPORT code would mean the two directions could drift apart again.
    ///
    /// <para>
    /// VISIT_SETUP_PROGRESS_UPDATE is the fifth, and it belongs here rather than with the invitations:
    /// it publishes the Schedule Report to a recipient list the Host owns, carries no one-time link, and
    /// is therefore caller-controlled like the other four. Listed explicitly so that a sixth still has to
    /// be a deliberate act.
    /// </para>
    /// </summary>
    [Fact]
    public void The_report_group_is_five_templates_and_no_more()
    {
        var report = SystemEmailTemplates.All
            .Where(t => t.Purpose == PEMS.Domain.Constants.EmailTemplatePurposes.Report)
            .Select(t => t.TemplateCode)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                SystemEmailTemplates.ReportCampusOperation,
                SystemEmailTemplates.ReportDepartmentCollaboration,
                SystemEmailTemplates.ReportDepartmentInvoice,
                SystemEmailTemplates.ReportPersonnelPerformance,
                SystemEmailTemplates.VisitSetupProgressUpdate,
            }.OrderBy(c => c, StringComparer.Ordinal),
            report);
    }

    [Fact]
    public async Task A_template_that_is_switched_off_stops_the_send_rather_than_falling_back()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var renderer = new EmailTemplateRenderer(db);
        var template = SystemEmailTemplates.Find(SystemEmailTemplates.AccountActivated)!;
        var variables = template.DeclaredVariables.ToDictionary(v => v, v => "x", StringComparer.Ordinal);

        await EmailEvidenceHarness.WithTemplateAsync(
            db, template.TemplateCode, t => t.Status = "INACTIVE",
            async () =>
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => renderer.RenderAsync(
                    new EmailRenderRequest(template.TemplateCode, EmailLanguages.Vi, variables)));
                Assert.Contains(EmailErrorCodes.TemplateInactive, ErrorCodeOf(ex));
            });
    }
}
