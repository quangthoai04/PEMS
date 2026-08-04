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
using PEMS.Domain.Entities.Emails;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The template save and restore, against a real database.
///
/// <para>
/// This replaces <c>EmailTemplateAtomicSaveTests</c>, which pinned the save's two-halves behaviour:
/// content and contact settings written in one transaction, and the visibility matrix (NONE / OPTIONAL /
/// REQUIRED) judged against both language bodies at once. Neither exists any more — there is no contact
/// configuration, so a save has one half and a body cannot contradict a setting.
/// </para>
/// <para>
/// What survives is everything that was never about the contact block: the concurrency token, the refusal
/// of an unregistered code, the response reporting what was STORED rather than what was sent, and the
/// content contract — including the one rule the sender variables added, that a credential-bearing
/// template refuses <c>{{sender*}}</c> at save time rather than at send time.
/// </para>
/// </summary>
public sealed class EmailTemplateSaveTests : IDisposable
{
    /// <summary>An ordinary template: no credential, no required content block.</summary>
    private const string OrdinaryTemplate = SystemEmailTemplates.AccountRoleChanged;

    /// <summary>A template whose whole message is a one-time link, so it may not name a sender.</summary>
    private const string CredentialTemplate = SystemEmailTemplates.AccountEmailConfirmation;

    private readonly Dictionary<string, (string Name, string? Description, string? SubjectVi, string? BodyVi,
        string? SubjectEn, string? BodyEn, string? VariablesText)> _templates = new();

    public EmailTemplateSaveTests()
    {
        if (!CanUseDatabase()) return;

        // Integration tests share one database and xUnit runs classes in parallel, so anything this suite
        // edits it must be able to put back exactly.
        using var db = EmailEvidenceHarness.NewContext();

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

            db.SaveChanges();
        }
        catch
        {
            // Never let cleanup mask the assertion that actually failed.
        }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private static async Task<EmailTemplate> LoadAsync(ApplicationDbContext db, string code)
        => await db.EmailTemplates.AsNoTracking().FirstAsync(t => t.TemplateCode == code);

    private static UpdateEmailTemplateCommand Save(EmailTemplate t, string? bodyVi = null, string? bodyEn = null)
        => new()
        {
            EmailTemplateId = t.EmailTemplateId,
            Name = t.Name,
            Description = t.Description,
            SubjectVi = t.SubjectVi,
            BodyVi = bodyVi ?? t.BodyVi,
            SubjectEn = t.SubjectEn,
            BodyEn = bodyEn ?? t.BodyEn,
            ExpectedRevision = t.Revision,
        };

    // ── Concurrency ─────────────────────────────────────────────────────────

    [Fact]
    public async Task One_save_bumps_the_revision_exactly_once()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var before = await LoadAsync(db, OrdinaryTemplate);

        var response = await EmailTemplateHandlers.Update(db)
            .Handle(Save(before), CancellationToken.None);

        Assert.Equal(before.Revision + 1, response.Revision);
        Assert.Equal(response.Revision, (await LoadAsync(db, OrdinaryTemplate)).Revision);
    }

    /// <summary>
    /// The token is compared inside the write statement, so a save that lost the race writes nothing.
    ///
    /// <para>
    /// This used to be titled "writes neither half" — the interesting part being that a stale revision
    /// could not leave the content saved and the policy not, or the reverse. There is one half now, and
    /// the property that matters is unchanged: nothing at all is written.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_stale_revision_writes_nothing()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var before = await LoadAsync(db, OrdinaryTemplate);

        var command = Save(before, bodyVi: before.BodyVi + "<p>Bản sửa sẽ không được ghi.</p>");
        command.ExpectedRevision = before.Revision + 99;   // a token nobody was ever given

        await Assert.ThrowsAsync<ConflictException>(
            () => EmailTemplateHandlers.Update(db).Handle(command, CancellationToken.None));

        var after = await LoadAsync(db, OrdinaryTemplate);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.BodyVi, after.BodyVi);
    }

    [Fact]
    public async Task Restore_bumps_the_revision_exactly_once()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var before = await LoadAsync(db, OrdinaryTemplate);

        var response = await EmailTemplateHandlers.Restore(db).Handle(
            new RestoreEmailTemplateCommand
            {
                EmailTemplateId = before.EmailTemplateId,
                ExpectedRevision = before.Revision,
            },
            CancellationToken.None);

        Assert.Equal(before.Revision + 1, response.Revision);
    }

    // ── What the response says ──────────────────────────────────────────────

    /// <summary>
    /// The editor re-baselines its dirty check from the response, so it has to describe the STORED row.
    /// An empty description is written as NULL, and echoing '' back would leave the screen reporting an
    /// unsaved change the instant a save succeeded.
    /// </summary>
    [Fact]
    public async Task The_response_reports_what_was_stored_rather_than_what_was_sent()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var before = await LoadAsync(db, OrdinaryTemplate);

        var command = Save(before);
        command.Description = "";

        var response = await EmailTemplateHandlers.Update(db).Handle(command, CancellationToken.None);

        Assert.Null(response.Description);
        Assert.Null((await LoadAsync(db, OrdinaryTemplate)).Description);
    }

    // ── The content contract, including the sender rule ─────────────────────

    /// <summary>
    /// A <c>{{sender*}}</c> placeholder on a credential-bearing template is refused where the operator
    /// made the change, not at send time in front of a recipient.
    /// </summary>
    [Fact]
    public async Task A_sender_variable_on_a_credential_template_is_refused_at_save()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var before = await LoadAsync(db, CredentialTemplate);

        var ex = await Assert.ThrowsAsync<EmailTemplateContentException>(
            () => EmailTemplateHandlers.Update(db).Handle(
                Save(before, bodyVi: before.BodyVi + "<p>{{senderName}}</p>"),
                CancellationToken.None));

        Assert.Contains(ex.Issues, i => i.Code == EmailErrorCodes.TemplateSenderVariableNotAllowed);
        // Its own code, not "biến không tồn tại": the variable exists and resolves on 28 other templates.
        Assert.DoesNotContain(ex.Issues, i => i.Code == EmailErrorCodes.TemplateVariableUnknown);

        var after = await LoadAsync(db, CredentialTemplate);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.BodyVi, after.BodyVi);
    }

    /// <summary>…and the same placeholder saves cleanly on a template whose capability permits it.</summary>
    [Fact]
    public async Task A_sender_variable_saves_on_a_capable_template()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var before = await LoadAsync(db, OrdinaryTemplate);

        var response = await EmailTemplateHandlers.Update(db).Handle(
            Save(before,
                bodyVi: before.BodyVi + "<p>{{senderName}} — {{senderEmail}}</p>",
                bodyEn: before.BodyEn + "<p>{{senderName}} — {{senderEmail}}</p>"),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains("{{senderName}}", (await LoadAsync(db, OrdinaryTemplate)).BodyVi);
    }

    /// <summary>
    /// <c>variables_text</c> is rewritten from the contract on every save, so the column the renderer
    /// reads cannot drift from the registry — and that includes the six sender names.
    /// </summary>
    [Fact]
    public async Task A_save_rewrites_variables_text_from_the_contract()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var before = await LoadAsync(db, OrdinaryTemplate);

        await EmailTemplateHandlers.Update(db).Handle(Save(before), CancellationToken.None);

        var declared = (await LoadAsync(db, OrdinaryTemplate)).VariablesText ?? "";
        foreach (var name in PEMS.Application.Emails.Sender.EmailSenderVariableNames.All)
            Assert.Contains(name, declared);

        // A trusted block is the backend's, never an operator-supplied value, so it may not be advertised
        // in the column an operator reads as "what I may write".
        Assert.DoesNotContain("actionBlock", declared);
    }
}
