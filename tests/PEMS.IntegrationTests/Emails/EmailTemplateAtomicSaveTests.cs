using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Commands.RestoreEmailTemplate;
using PEMS.Application.Emails.Commands.UpdateEmailTemplate;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The atomic template save, and the rule that spans its two halves.
///
/// <para>
/// <b>Two defects are pinned here.</b>
/// </para>
/// <para>
/// The first is partial saves. Content and contact settings were written by two endpoints with two
/// <c>SaveChangesAsync</c> calls, so a screen that changed both had to make two requests that could not be
/// made atomic from a browser: the second failing left the first written, and the pair could be left
/// contradicting each other. Worse, neither half could accept a change to BOTH at once, because each
/// judged the incoming half against the other half as STORED — removing the block and switching to NONE
/// was refused by the settings endpoint (the stored body still had the block), and adding the block while
/// switching to REQUIRED was refused by the content endpoint (the stored policy still said OPTIONAL).
/// </para>
/// <para>
/// The second is the visibility matrix. <c>AllowsSystemBlock</c> answered from CAPABILITY alone, so a
/// template whose administrator had switched the block off still accepted a body carrying it. The save
/// succeeded at both layers, and the send then substituted an empty string — so the mail went out looking
/// correct and the contradiction produced no signal anywhere. That last part is covered by
/// <see cref="EmailContactBlockRuntimeGuardTests"/>; the save-time half is here.
/// </para>
/// </summary>
public sealed class EmailTemplateAtomicSaveTests : IDisposable
{
    /// <summary>
    /// A template that MAY carry the block and is not obliged to — the only capability on which all three
    /// levels are legal, and therefore the only one on which the whole matrix can be exercised.
    /// </summary>
    private const string OptionalTemplate = SystemEmailTemplates.AccountRoleChanged;

    /// <summary>A template that can NEVER carry the block: its whole message is a one-time link.</summary>
    private const string UnsupportedTemplate = SystemEmailTemplates.AccountEmailConfirmation;

    private readonly List<EmailContactPolicy> _policies = new();
    private readonly Dictionary<string, (string Name, string? Description, string? SubjectVi, string? BodyVi,
        string? SubjectEn, string? BodyEn, string? VariablesText)> _templates = new();

    public EmailTemplateAtomicSaveTests()
    {
        if (!CanUseDatabase()) return;

        // Integration tests share one database and xUnit runs classes in parallel, so anything this suite
        // edits it must be able to put back exactly.
        using var db = EmailEvidenceHarness.NewContext();

        foreach (var row in db.EmailContactPolicies.AsNoTracking().ToList())
            _policies.Add(row);

        foreach (var t in db.EmailTemplates.AsNoTracking().ToList())
            _templates[t.TemplateCode] = (t.Name, t.Description, t.SubjectVi, t.BodyVi,
                                          t.SubjectEn, t.BodyEn, t.VariablesText);
    }

    private static bool CanUseDatabase()
    {
        try { EmailEvidenceHarness.RequireDb(); return true; }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_templates.Count == 0) return;

        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            foreach (var t in db.EmailTemplates.ToList())
            {
                if (!_templates.TryGetValue(t.TemplateCode, out var o)) continue;
                t.Name = o.Name; t.Description = o.Description;
                t.SubjectVi = o.SubjectVi; t.BodyVi = o.BodyVi;
                t.SubjectEn = o.SubjectEn; t.BodyEn = o.BodyEn;
                t.VariablesText = o.VariablesText;
            }

            var byKey = _policies.ToDictionary(r => (r.ScopeType, r.ScopeKey));

            foreach (var row in db.EmailContactPolicies.ToList())
            {
                if (!byKey.TryGetValue((row.ScopeType, row.ScopeKey), out var o))
                {
                    db.EmailContactPolicies.Remove(row);   // something this suite added
                    continue;
                }

                row.Requirement = o.Requirement;
                row.ContactSource = o.ContactSource;
                row.ShowEmail = o.ShowEmail;
                row.ShowPhone = o.ShowPhone;
                row.ShowDepartment = o.ShowDepartment;
                row.ShowCampus = o.ShowCampus;
                row.ShowSender = o.ShowSender;
                row.HeadingVi = o.HeadingVi;
                row.HeadingEn = o.HeadingEn;
                row.ReplyToSource = o.ReplyToSource;
            }

            db.SaveChanges();
        }
        catch
        {
            // Never let cleanup mask the assertion that actually failed.
        }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private static readonly string Marker = EmailContactBlockText.Marker;

    private static async Task<EmailTemplate> LoadAsync(ApplicationDbContext db, string code)
        => await db.EmailTemplates.AsNoTracking().FirstAsync(t => t.TemplateCode == code);

    /// <summary>The contact settings as they are stored right now, in the shape a save sends back.</summary>
    private static async Task<EmailContactSettingsDto> SettingsAsync(ApplicationDbContext db, string code)
        => await EmailTemplateHandlers.ContactSettings(db)
            .Handle(new GetEmailContactSettingsQuery { TemplateCode = code }, CancellationToken.None);

    private static UpdateEmailTemplateContactSettings Contact(
        string requirement,
        string source = nameof(EmailContactSource.CAMPUS_DEFAULT),
        bool showEmail = true,
        bool showPhone = true,
        string replyTo = nameof(EmailReplyToSource.NONE)) => new()
    {
        Requirement = requirement,
        ContactSource = source,
        ShowEmail = showEmail,
        ShowPhone = showPhone,
        ShowCampus = true,
        ReplyToSource = replyTo,
    };

    /// <summary>
    /// A save that carries everything, so each test changes only the one thing it is about.
    /// </summary>
    private static UpdateEmailTemplateCommand Save(
        EmailTemplate t,
        uint expectedRevision,
        string? bodyVi = null,
        string? bodyEn = null,
        UpdateEmailTemplateContactSettings? contact = null,
        string? name = null) => new()
    {
        EmailTemplateId = t.EmailTemplateId,
        Name = name ?? t.Name,
        Description = t.Description,
        SubjectVi = t.SubjectVi ?? "Tiêu đề",
        BodyVi = bodyVi ?? t.BodyVi,
        SubjectEn = t.SubjectEn,
        BodyEn = bodyEn ?? t.BodyEn,
        ExpectedRevision = expectedRevision,
        ContactSettings = contact,
    };

    /// <summary>A body for the optional template, with or without the contact block.</summary>
    private static string Body(bool withBlock, string text = "Xin chào bạn.")
        => $"<p>{text}</p>" + (withBlock ? Marker : string.Empty);

    /// <summary>
    /// Puts the optional template into a known state — both bodies carrying the block, level OPTIONAL —
    /// and returns the revision that state was written at.
    /// </summary>
    private static async Task<uint> ArrangeOptionalAsync(ApplicationDbContext db, bool withBlock = true)
    {
        var t = await LoadAsync(db, OptionalTemplate);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, t.Revision,
                bodyVi: Body(withBlock),
                bodyEn: Body(withBlock, "Hello."),
                contact: Contact(nameof(EmailContactRequirement.OPTIONAL))),
            CancellationToken.None);

        return result.Revision;
    }

    // ── The matrix: level against body (visibility prompt §2, §7, §8) ───────

    [Fact]
    public async Task Hidden_with_no_block_anywhere_saves()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db, withBlock: false);
        var t = await LoadAsync(db, OptionalTemplate);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, revision, contact: Contact(nameof(EmailContactRequirement.NONE))),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(nameof(EmailContactRequirement.NONE), result.ContactSettings!.Requirement);
    }

    /// <summary>
    /// The row that did not exist before this change.
    ///
    /// <para>
    /// It comes back as a CONTENT exception rather than a business-rule one, and that is the better of the
    /// two: the content validator reaches it first and reports it per FIELD, so the screen can anchor the
    /// message under the body it is about and offer the removal there. The settings validator holds the
    /// same rule as a backstop for the callers that change only the policy.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(true, false, new[] { EmailTemplateFields.BodyVi }, "tiếng Việt")]
    [InlineData(false, true, new[] { EmailTemplateFields.BodyEn }, "tiếng Anh")]
    [InlineData(true, true, new[] { EmailTemplateFields.BodyVi, EmailTemplateFields.BodyEn }, "tiếng")]
    public async Task Hidden_with_the_block_still_in_a_body_is_refused(
        bool inVi, bool inEn, string[] expectedFields, string named)
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db, withBlock: false);
        var t = await LoadAsync(db, OptionalTemplate);

        var ex = await Assert.ThrowsAsync<EmailTemplateContentException>(() =>
            EmailTemplateHandlers.Update(db).Handle(
                Save(t, revision,
                    bodyVi: Body(inVi),
                    bodyEn: Body(inEn, "Hello."),
                    contact: Contact(nameof(EmailContactRequirement.NONE))),
                CancellationToken.None));

        var refusals = ex.Issues
            .Where(i => i.Code == EmailErrorCodes.ContactBlockNotAllowedWhenHidden)
            .ToList();

        Assert.Equal(expectedFields.Length, refusals.Count);
        Assert.Equal(expectedFields.OrderBy(f => f, StringComparer.Ordinal),
                     refusals.Select(i => i.Field).OrderBy(f => f, StringComparer.Ordinal));

        // The message names the language, so an operator with a clean Vietnamese tab is not left guessing.
        Assert.All(refusals, i => Assert.Contains(named, i.MessageVi, StringComparison.OrdinalIgnoreCase));

        // NOT the "this template cannot carry it" code: this template can, and the repair is a choice.
        Assert.DoesNotContain(ex.Issues, i => i.Code == EmailErrorCodes.TemplateSystemBlockNotAllowed);
    }

    /// <summary>
    /// The settings validator holds the same rule, for the caller that changes only the policy.
    ///
    /// <para>
    /// Exercised directly rather than through the combined save, because the combined save's content
    /// validator answers first — which is right for the editor and would leave this backstop untested.
    /// </para>
    /// </summary>
    [Fact]
    public void The_settings_validator_refuses_a_hidden_level_over_a_body_that_carries_the_block()
    {
        var input = new EmailContactSettingsInput(
            EmailContactRequirement.NONE, EmailContactSource.CAMPUS_DEFAULT,
            ShowEmail: true, ShowPhone: true, ShowDepartment: false, ShowCampus: true, ShowSender: false,
            HeadingVi: null, HeadingEn: null, EmailReplyToSource.NONE);

        var ex = Assert.Throws<BusinessRuleException>(() =>
            EmailContactSettingsValidator.Validate(
                OptionalTemplate, input, bodyVi: Body(true), bodyEn: Body(false, "Hello.")));

        Assert.Equal(EmailErrorCodes.ContactBlockNotAllowedWhenHidden, ex.ErrorCode);
        Assert.Contains("tiếng Việt", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Optional_accepts_a_body_with_the_block_and_one_without(bool withBlock)
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var t = await LoadAsync(db, OptionalTemplate);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, revision,
                bodyVi: Body(withBlock),
                bodyEn: Body(withBlock, "Hello."),
                contact: Contact(nameof(EmailContactRequirement.OPTIONAL))),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Theory]
    [InlineData(false, true, EmailTemplateFields.BodyVi, "tiếng Việt")]
    [InlineData(true, false, EmailTemplateFields.BodyEn, "tiếng Anh")]
    public async Task Required_with_a_body_missing_the_block_is_refused_and_names_the_language(
        bool inVi, bool inEn, string expectedField, string named)
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var t = await LoadAsync(db, OptionalTemplate);

        var ex = await Assert.ThrowsAsync<EmailTemplateContentException>(() =>
            EmailTemplateHandlers.Update(db).Handle(
                Save(t, revision,
                    bodyVi: Body(inVi),
                    bodyEn: Body(inEn, "Hello."),
                    contact: Contact(nameof(EmailContactRequirement.REQUIRED))),
                CancellationToken.None));

        var missing = Assert.Single(ex.Issues,
            i => i.Code == EmailErrorCodes.TemplateRequiredContactBlockNotInBody);

        // Addressed to the language that is short, and saying which one — an operator whose Vietnamese
        // body is fine needs to be sent to the English tab, not told "this template needs the block".
        Assert.Equal(expectedField, missing.Field);
        Assert.Contains(named, missing.MessageVi, StringComparison.OrdinalIgnoreCase);

        // And never as the opposite refusal, which would send them to delete the block they must add.
        Assert.DoesNotContain(ex.Issues, i => i.Code == EmailErrorCodes.ContactBlockNotAllowedWhenHidden);
    }

    [Fact]
    public async Task Required_with_the_block_in_both_languages_saves()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var t = await LoadAsync(db, OptionalTemplate);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, revision,
                bodyVi: Body(true),
                bodyEn: Body(true, "Hello."),
                contact: Contact(nameof(EmailContactRequirement.REQUIRED))),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(nameof(EmailContactRequirement.REQUIRED), result.ContactSettings!.Requirement);
    }

    /// <summary>
    /// §9: UNSUPPORTED is not NONE. A template that can never carry the block is refused under the code
    /// that says so, whatever level the request happens to carry.
    /// </summary>
    [Fact]
    public async Task An_unsupported_template_refuses_contact_settings_outright()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, UnsupportedTemplate);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            EmailTemplateHandlers.Update(db).Handle(
                Save(t, t.Revision, contact: Contact(nameof(EmailContactRequirement.OPTIONAL))),
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.ContactNotSupportedForTemplate, ex.ErrorCode);
    }

    [Fact]
    public async Task An_unsupported_template_refuses_a_body_carrying_the_block()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, UnsupportedTemplate);

        var ex = await Assert.ThrowsAsync<EmailTemplateContentException>(() =>
            EmailTemplateHandlers.Update(db).Handle(
                Save(t, t.Revision, bodyVi: (t.BodyVi ?? "") + Marker),
                CancellationToken.None));

        // Under the capability code, NOT the hidden-level one: no setting would make this legal, so
        // offering the operator a choice between two repairs would be offering one that does not exist.
        Assert.Contains(ex.Issues, i => i.Code == EmailErrorCodes.TemplateSystemBlockNotAllowed);
        Assert.DoesNotContain(ex.Issues, i => i.Code == EmailErrorCodes.ContactBlockNotAllowedWhenHidden);
    }

    /// <summary>§6.1: content still saves on an unsupported template when no settings are sent.</summary>
    [Fact]
    public async Task An_unsupported_template_saves_content_when_the_settings_are_omitted()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, UnsupportedTemplate);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, t.Revision, name: "Tên mới cho mẫu xác nhận"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Tên mới cho mẫu xác nhận", result.Name);
        // No configuration to describe, so none is invented.
        Assert.Null(result.ContactSettings);
    }

    // ── Atomicity (atomic-save prompt §5, §7, §15) ──────────────────────────

    /// <summary>
    /// The edit that neither endpoint could accept before: remove the block AND hide the block, together.
    ///
    /// <para>
    /// It was refused whichever way round it was attempted, because each half judged the incoming value
    /// against the other half as STORED. That is the case for merging them, stated as a test.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Removing_the_block_and_hiding_it_succeeds_in_one_request()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var t = await LoadAsync(db, OptionalTemplate);
        Assert.Contains(Marker, t.BodyVi!, StringComparison.Ordinal);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, revision,
                bodyVi: Body(false),
                bodyEn: Body(false, "Hello."),
                contact: Contact(nameof(EmailContactRequirement.NONE))),
            CancellationToken.None);

        Assert.True(result.Success);

        var after = await LoadAsync(db, OptionalTemplate);
        Assert.DoesNotContain(Marker, after.BodyVi ?? "", StringComparison.Ordinal);
        Assert.Equal(nameof(EmailContactRequirement.NONE),
            (await SettingsAsync(db, OptionalTemplate)).Requirement);
    }

    /// <summary>And the mirror image: add the block AND require it, together.</summary>
    [Fact]
    public async Task Adding_the_block_and_requiring_it_succeeds_in_one_request()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db, withBlock: false);
        var t = await LoadAsync(db, OptionalTemplate);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, revision,
                bodyVi: Body(true),
                bodyEn: Body(true, "Hello."),
                contact: Contact(nameof(EmailContactRequirement.REQUIRED))),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(nameof(EmailContactRequirement.REQUIRED),
            (await SettingsAsync(db, OptionalTemplate)).Requirement);
    }

    /// <summary>
    /// Valid content, invalid settings: NOTHING is written. Not the content, not the revision.
    ///
    /// <para>
    /// This is the partial save the merge exists to remove. Under two endpoints the content landed and
    /// the settings did not, and the operator was told the settings had failed while the wording had
    /// silently changed underneath them.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Valid_content_with_invalid_settings_writes_neither()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var before = await LoadAsync(db, OptionalTemplate);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            EmailTemplateHandlers.Update(db).Handle(
                Save(before, revision,
                    name: "Tên đáng lẽ không được lưu",
                    // Both channels off under a visible level: refused by the settings validator.
                    contact: Contact(nameof(EmailContactRequirement.OPTIONAL),
                        showEmail: false, showPhone: false)),
                CancellationToken.None));

        await using var fresh = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(fresh, OptionalTemplate);

        Assert.Equal(before.Name, after.Name);
        Assert.Equal(revision, after.Revision);   // not bumped
    }

    /// <summary>The other direction: invalid content leaves the stored SETTINGS untouched.</summary>
    [Fact]
    public async Task Invalid_content_with_valid_settings_writes_neither()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var settingsBefore = await SettingsAsync(db, OptionalTemplate);
        var t = await LoadAsync(db, OptionalTemplate);

        await Assert.ThrowsAsync<EmailTemplateContentException>(() =>
            EmailTemplateHandlers.Update(db).Handle(
                Save(t, revision,
                    // A variable this template does not declare — refused by the content validator.
                    bodyVi: "<p>{{khongTonTaiBaoGio}}</p>" + Marker,
                    contact: Contact(nameof(EmailContactRequirement.REQUIRED))),
                CancellationToken.None));

        await using var fresh = EmailEvidenceHarness.NewContext();
        var settingsAfter = await SettingsAsync(fresh, OptionalTemplate);
        var after = await LoadAsync(fresh, OptionalTemplate);

        Assert.Equal(settingsBefore.Requirement, settingsAfter.Requirement);
        Assert.Equal(revision, after.Revision);
    }

    /// <summary>§7: one save bumps the revision once, however many groups of fields it touched.</summary>
    [Fact]
    public async Task One_save_bumps_the_revision_exactly_once()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var t = await LoadAsync(db, OptionalTemplate);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, revision,
                name: "Tên đã sửa cùng lúc với cấu hình",
                contact: Contact(nameof(EmailContactRequirement.OPTIONAL), showPhone: false)),
            CancellationToken.None);

        Assert.Equal(revision + 1, result.Revision);

        await using var fresh = EmailEvidenceHarness.NewContext();
        Assert.Equal(revision + 1, (await LoadAsync(fresh, OptionalTemplate)).Revision);
    }

    /// <summary>§7: a stale token writes nothing, in either half.</summary>
    [Fact]
    public async Task A_stale_revision_writes_neither_half()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var settingsBefore = await SettingsAsync(db, OptionalTemplate);
        var t = await LoadAsync(db, OptionalTemplate);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            EmailTemplateHandlers.Update(db).Handle(
                Save(t, revision - 1,
                    name: "Tên từ một tab đã cũ",
                    contact: Contact(nameof(EmailContactRequirement.REQUIRED))),
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateConcurrencyConflict, ex.ErrorCode);

        await using var fresh = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(fresh, OptionalTemplate);
        var settingsAfter = await SettingsAsync(fresh, OptionalTemplate);

        Assert.Equal(t.Name, after.Name);
        Assert.Equal(revision, after.Revision);
        // The policy write happens AFTER the conditional content write, inside the same transaction, so a
        // refused revision must leave it untouched too.
        Assert.Equal(settingsBefore.Requirement, settingsAfter.Requirement);
    }

    /// <summary>Omitting the settings leaves the stored policy exactly as it was.</summary>
    [Fact]
    public async Task Omitting_the_settings_leaves_the_policy_alone()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var before = await SettingsAsync(db, OptionalTemplate);
        var t = await LoadAsync(db, OptionalTemplate);

        var result = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, revision, name: "Chỉ sửa tên"),
            CancellationToken.None);

        Assert.True(result.Success);

        var after = await SettingsAsync(db, OptionalTemplate);
        Assert.Equal(before.Requirement, after.Requirement);
        Assert.Equal(before.ContactSource, after.ContactSource);
        Assert.Equal(before.ShowPhone, after.ShowPhone);
        // And the response still describes the policy, so the editor re-baselines from one shape.
        Assert.NotNull(result.ContactSettings);
    }

    /// <summary>
    /// The response is the STORED snapshot, not an echo of the request.
    ///
    /// <para>
    /// The editor re-baselines its dirty check from it, and the two are not always the same: headings are
    /// trimmed and stripped of markup on the way in, and an empty description is stored as NULL. A
    /// baseline built from the request would report those normalisations as unsaved changes the instant
    /// the save succeeded.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_response_reports_what_was_stored_rather_than_what_was_sent()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var t = await LoadAsync(db, OptionalTemplate);

        var contact = Contact(nameof(EmailContactRequirement.OPTIONAL));
        contact.HeadingVi = "  <b>Liên hệ</b>  ";

        var result = await EmailTemplateHandlers.Update(db).Handle(
            new UpdateEmailTemplateCommand
            {
                EmailTemplateId = t.EmailTemplateId,
                Name = t.Name,
                Description = "",                       // stored as NULL
                SubjectVi = t.SubjectVi ?? "Tiêu đề",
                BodyVi = Body(true),
                SubjectEn = t.SubjectEn,
                BodyEn = Body(true, "Hello."),
                ExpectedRevision = revision,
                ContactSettings = contact,
            },
            CancellationToken.None);

        Assert.Null(result.Description);
        Assert.Equal("Liên hệ", result.ContactSettings!.HeadingVi);
    }

    // ── Restore (atomic-save prompt §8) ─────────────────────────────────────

    /// <summary>
    /// Restore puts BOTH halves back, in one transaction.
    ///
    /// <para>
    /// It used to restore content only, leaving a policy the operator had changed — so a template could
    /// land in exactly the contradiction both halves refuse: a shipped body carrying the block under a
    /// stored policy of NONE. The shipped content and the shipped policy are consistent with each other
    /// by construction, so restoring them together is the only version that always produces a valid
    /// template.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Restore_puts_back_the_content_and_the_contact_settings_together()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        // Move both halves away from the shipped values.
        var revision = await ArrangeOptionalAsync(db);
        var t = await LoadAsync(db, OptionalTemplate);
        var damaged = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, revision,
                name: "Tên đã bị sửa",
                bodyVi: Body(true, "Nội dung đã bị sửa."),
                bodyEn: Body(true, "Edited."),
                contact: Contact(nameof(EmailContactRequirement.REQUIRED), showPhone: false)),
            CancellationToken.None);

        var result = await EmailTemplateHandlers.Restore(db).Handle(
            new RestoreEmailTemplateCommand
            {
                EmailTemplateId = t.EmailTemplateId,
                ExpectedRevision = damaged.Revision,
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.ContactSettingsRestored);

        var shipped = EmailContactPolicyDefaults.For(OptionalTemplate);
        var after = await SettingsAsync(db, OptionalTemplate);

        Assert.Equal(shipped.Requirement.ToString(), after.Requirement);
        Assert.Equal(shipped.ContactSource.ToString(), after.ContactSource);
        Assert.Equal(shipped.ShowPhone, after.ShowPhone);

        // And the content, from the same transaction.
        var row = await LoadAsync(db, OptionalTemplate);
        Assert.Equal(EmailTemplateDefaults.For(OptionalTemplate)!.Name, row.Name);
    }

    /// <summary>
    /// §8: an unsupported template restores its CONTENT and is not refused for having no policy.
    ///
    /// <para>
    /// The standalone contact-restore endpoint answers <c>CONTACT_NOT_SUPPORTED</c> here, correctly,
    /// because restoring a policy is all it does. Applying that refusal to the combined restore would
    /// block the content restore on those four templates for a reason that has nothing to do with content.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Restore_on_an_unsupported_template_restores_content_and_creates_no_policy()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, UnsupportedTemplate);
        var damaged = await EmailTemplateHandlers.Update(db).Handle(
            Save(t, t.Revision, name: "Tên đã bị sửa"),
            CancellationToken.None);

        var result = await EmailTemplateHandlers.Restore(db).Handle(
            new RestoreEmailTemplateCommand
            {
                EmailTemplateId = t.EmailTemplateId,
                ExpectedRevision = damaged.Revision,
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.ContactSettingsRestored);
        Assert.Null(result.ContactSettings);
        Assert.Equal(EmailTemplateDefaults.For(UnsupportedTemplate)!.Name, result.Name);
    }

    /// <summary>A restore bumps the revision once, like a save.</summary>
    [Fact]
    public async Task Restore_bumps_the_revision_exactly_once()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var t = await LoadAsync(db, OptionalTemplate);

        var result = await EmailTemplateHandlers.Restore(db).Handle(
            new RestoreEmailTemplateCommand
            {
                EmailTemplateId = t.EmailTemplateId,
                ExpectedRevision = revision,
            },
            CancellationToken.None);

        Assert.Equal(revision + 1, result.Revision);
    }

    /// <summary>A stale token on a restore writes nothing, in either half.</summary>
    [Fact]
    public async Task A_stale_revision_on_restore_writes_neither_half()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var revision = await ArrangeOptionalAsync(db);
        var before = await SettingsAsync(db, OptionalTemplate);
        var t = await LoadAsync(db, OptionalTemplate);

        await Assert.ThrowsAsync<ConflictException>(() =>
            EmailTemplateHandlers.Restore(db).Handle(
                new RestoreEmailTemplateCommand
                {
                    EmailTemplateId = t.EmailTemplateId,
                    ExpectedRevision = revision - 1,
                },
                CancellationToken.None));

        await using var fresh = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(fresh, OptionalTemplate);
        var settingsAfter = await SettingsAsync(fresh, OptionalTemplate);

        Assert.Equal(t.Name, after.Name);
        Assert.Equal(revision, after.Revision);
        Assert.Equal(before.Requirement, settingsAfter.Requirement);
    }
}
