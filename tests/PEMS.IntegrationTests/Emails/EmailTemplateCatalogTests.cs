using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Commands.CreateEmailTemplate;
using PEMS.Application.Emails.Commands.ToggleEmailTemplateStatus;
using PEMS.Application.Emails.Commands.UpdateEmailTemplate;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The system template catalog is fixed, and only content is editable (G11-I), proved against a real
/// database rather than against the screen that used to be the only thing enforcing it.
///
/// <para>
/// Every mutation here runs through the handler, not the controller, for the same reason the guard
/// tests drive SQL directly: the requirement is that NO path creates, deletes or deactivates a system
/// template. A check that lives only in a controller attribute or a hidden button is not that, and both
/// of those were exactly where the old protection lived — which is to say, nowhere.
/// </para>
/// </summary>
public sealed class EmailTemplateCatalogTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g11i-catalog@partner.example.com");

    /// <summary>
    /// Content as it stood before this class ran, restored on the way out.
    ///
    /// <para>
    /// These tests edit real canonical templates, and the suite shares one database. Without this,
    /// <c>ACCOUNT_ACTIVATED</c> was left holding a test marker, and three unrelated classes then failed
    /// — the renderer coverage, the G4 closure check and the R-106 preview matrix — each reporting a
    /// defect in the product rather than the mess this class had left behind. A test that damages the
    /// fixture other tests rely on does not merely fail loudly; it makes healthy code look broken.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, (string? SubjectVi, string? BodyVi, string? SubjectEn, string? BodyEn,
        string Name, string? Description, string? VariablesText, DateTime? UpdatedAt)> _original = new(StringComparer.Ordinal);

    public EmailTemplateCatalogTests()
    {
        if (!CanUseDatabase()) return;

        using var db = EmailEvidenceHarness.NewContext();
        foreach (var t in db.EmailTemplates.ToList())
            _original[t.TemplateCode] = (t.SubjectVi, t.BodyVi, t.SubjectEn, t.BodyEn,
                                         t.Name, t.Description, t.VariablesText, t.UpdatedAt);
    }

    private static bool CanUseDatabase()
    {
        try { EmailEvidenceHarness.RequireDb(); return true; }
        catch { return false; }
    }

    public void Dispose()
    {
        try
        {
            if (_original.Count > 0)
            {
                using var db = EmailEvidenceHarness.NewContext();
                foreach (var t in db.EmailTemplates.ToList())
                {
                    if (!_original.TryGetValue(t.TemplateCode, out var o)) continue;
                    t.SubjectVi = o.SubjectVi; t.BodyVi = o.BodyVi;
                    t.SubjectEn = o.SubjectEn; t.BodyEn = o.BodyEn;
                    t.Name = o.Name; t.Description = o.Description;
                    t.VariablesText = o.VariablesText; t.UpdatedAt = o.UpdatedAt;
                }
                db.SaveChanges();
            }
        }
        catch { /* a failed restore must not mask the test result; the next fresh import repairs it */ }

        _h.Dispose();
    }

    /// <summary>The HO operator every write in this suite is attributed to.</summary>
    private sealed class HoOperator : PEMS.Application.Common.Interfaces.ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => 1;
        public string? Email => "ho-operator@pems.test";
        public ulong? RoleId => null;
        public string? RoleCode => "HO";
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static UpdateEmailTemplateCommandHandler Update(ApplicationDbContext db)
        => new(db, new HoOperator());

    private static async Task<EmailTemplate> LoadAsync(ApplicationDbContext db, string code)
        => await db.EmailTemplates.FirstAsync(t => t.TemplateCode == code);

    /// <summary>A snapshot of everything an update must not be able to move.</summary>
    private static object Fingerprint(EmailTemplate t)
        => new { t.TemplateCode, t.Purpose, t.CampusId, t.Status, t.BodyFormat };

    // ── Create / delete / toggle ─────────────────────────────────────────────

    [Fact]
    public async Task Create_is_refused_with_a_stable_code()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var before = await db.EmailTemplates.CountAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateEmailTemplateCommandHandler().Handle(
                new CreateEmailTemplateCommand
                {
                    TemplateCode = "OPERATOR_INVENTED_CODE",
                    Name = "Mẫu tự tạo",
                    Purpose = "VISIT_REQUEST_VERIFY",
                },
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateCatalogFixed, ex.ErrorCode);

        // The row count is the assertion that matters: a refusal that still wrote would be worse than
        // no refusal, because nothing would look wrong.
        Assert.Equal(before, await db.EmailTemplates.CountAsync());
        Assert.False(await db.EmailTemplates.AnyAsync(t => t.TemplateCode == "OPERATOR_INVENTED_CODE"));
    }

    [Fact]
    public async Task Toggle_status_is_refused_with_a_stable_code()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var template = await LoadAsync(db, SystemEmailTemplates.AccountEmailConfirmation);
        var statusBefore = template.Status;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new ToggleEmailTemplateStatusCommandHandler().Handle(
                new ToggleEmailTemplateStatusCommand
                {
                    EmailTemplateId = template.EmailTemplateId,
                    Status = "INACTIVE",
                },
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateCatalogFixed, ex.ErrorCode);

        await using var fresh = EmailEvidenceHarness.NewContext();
        Assert.Equal(statusBefore, (await LoadAsync(fresh, SystemEmailTemplates.AccountEmailConfirmation)).Status);
    }

    /// <summary>
    /// There is no delete route at all, and there must not be one: history and drafts hold foreign keys
    /// into <c>email_templates</c>, so a deleted template takes a sent message's provenance with it.
    /// </summary>
    [Fact]
    public void No_delete_command_exists_for_email_templates()
    {
        var deleteCommands = typeof(CreateEmailTemplateCommand).Assembly
            .GetTypes()
            .Where(t => t.Name.Contains("EmailTemplate", StringComparison.Ordinal)
                     && (t.Name.StartsWith("Delete", StringComparison.Ordinal)
                      || t.Name.StartsWith("Clone", StringComparison.Ordinal)
                      || t.Name.StartsWith("Remove", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(deleteCommands.Count == 0,
            "These delete/clone commands exist for email templates: " + string.Join(", ", deleteCommands));
    }

    // ── The code set never moves ─────────────────────────────────────────────

    [Fact]
    public async Task The_database_code_set_matches_the_registry_exactly()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var inDatabase = await db.EmailTemplates.Select(t => t.TemplateCode).ToListAsync();
        var inRegistry = SystemEmailTemplates.AllCodes.ToHashSet(StringComparer.Ordinal);

        var unknown = inDatabase.Where(c => !inRegistry.Contains(c)).OrderBy(c => c).ToList();
        var missing = inRegistry.Where(c => !inDatabase.Contains(c)).OrderBy(c => c).ToList();

        Assert.True(unknown.Count == 0, "In the database but not the registry: " + string.Join(", ", unknown));
        Assert.True(missing.Count == 0, "In the registry but not the database: " + string.Join(", ", missing));
        Assert.Equal(inDatabase.Count, inDatabase.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task A_content_update_leaves_the_count_and_code_set_untouched()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var codesBefore = (await db.EmailTemplates.Select(t => t.TemplateCode).ToListAsync())
            .OrderBy(c => c, StringComparer.Ordinal).ToList();

        var template = await LoadAsync(db, SystemEmailTemplates.AccountActivated);
        await Update(db).Handle(ContentEdit(template, "<p>Chào {{fullName}}.</p>"), CancellationToken.None);

        await using var fresh = EmailEvidenceHarness.NewContext();
        var codesAfter = (await fresh.EmailTemplates.Select(t => t.TemplateCode).ToListAsync())
            .OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.Equal(codesBefore, codesAfter);
    }

    // ── The update whitelist ─────────────────────────────────────────────────

    /// <summary>The concurrency token an editor would have loaded: the row's current revision.</summary>
    private static uint TokenOf(EmailTemplate t) => t.Revision;

    private static UpdateEmailTemplateCommand ContentEdit(EmailTemplate t, string bodyVi) => new()
    {
        EmailTemplateId = t.EmailTemplateId,
        Name = t.Name,
        Description = t.Description,
        SubjectVi = t.SubjectVi ?? "Tiêu đề",
        BodyVi = bodyVi,
        SubjectEn = t.SubjectEn,
        BodyEn = t.BodyEn,
        ExpectedRevision = TokenOf(t),
    };

    [Fact]
    public async Task An_update_cannot_move_the_registry_owned_fields()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var template = await LoadAsync(db, SystemEmailTemplates.AccountRoleChanged);
        var before = Fingerprint(template);

        await Update(db).Handle(
            ContentEdit(template, "<p>Chào {{fullName}}, vai trò mới {{newRoleName}}.</p>"),
            CancellationToken.None);

        await using var fresh = EmailEvidenceHarness.NewContext();
        var after = Fingerprint(await LoadAsync(fresh, SystemEmailTemplates.AccountRoleChanged));

        Assert.Equal(before.ToString(), after.ToString());
    }

    /// <summary>
    /// <c>variables_text</c> is the column the renderer validates real sends against. An operator who
    /// could widen it could write placeholders no caller supplies; the save would look clean and every
    /// send afterwards would fail. It is now rewritten from the registry on each save.
    /// </summary>
    [Fact]
    public async Task An_update_rewrites_variables_text_from_the_registry()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var template = await LoadAsync(db, SystemEmailTemplates.AccountActivated);
        template.VariablesText = "somethingAnOperatorTyped";
        await db.SaveChangesAsync();

        await using var editDb = EmailEvidenceHarness.NewContext();
        var reloaded = await LoadAsync(editDb, SystemEmailTemplates.AccountActivated);
        await Update(editDb).Handle(
            ContentEdit(reloaded, "<p>Chào {{fullName}} tại {{campusName}}.</p>"), CancellationToken.None);

        await using var fresh = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(fresh, SystemEmailTemplates.AccountActivated);

        Assert.DoesNotContain("somethingAnOperatorTyped", after.VariablesText ?? "");

        var contract = EmailTemplateContracts.For(SystemEmailTemplates.AccountActivated)!;
        foreach (var declared in contract.AllowedVariables)
        {
            if (declared == PEMS.Application.Common.Interfaces.EmailTrustedBlocks.ActionBlock) continue;
            Assert.Contains(declared, after.VariablesText ?? "");
        }

        // The trusted block is deliberately NOT listed: variables_text describes what a CALLER supplies,
        // and the action block is minted by the backend. Listing it would invite a caller to pass one.
        Assert.DoesNotContain(
            PEMS.Application.Common.Interfaces.EmailTrustedBlocks.ActionBlock, after.VariablesText ?? "");
    }

    [Fact]
    public async Task A_content_update_is_persisted()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var marker = $"<p>Chào {{{{fullName}}}} — {Guid.NewGuid():N}</p>";
        var template = await LoadAsync(db, SystemEmailTemplates.AccountActivated);

        var response = await Update(db).Handle(ContentEdit(template, marker), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.UpdatedAt);

        await using var fresh = EmailEvidenceHarness.NewContext();
        Assert.Equal(marker, (await LoadAsync(fresh, SystemEmailTemplates.AccountActivated)).BodyVi);
    }

    // ── Content validation reaches the handler ───────────────────────────────

    [Fact]
    public async Task An_update_that_uses_another_modules_variable_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var template = await LoadAsync(db, SystemEmailTemplates.AccountEmailConfirmation);
        var bodyBefore = template.BodyVi;

        var ex = await Assert.ThrowsAsync<EmailTemplateContentException>(() =>
            Update(db).Handle(
                ContentEdit(template, "<p>Chào {{fullName}} — {{logisticsTitle}}</p>"),
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, ex.ErrorCode);
        Assert.Contains(ex.Issues, i => i.VariableName == "logisticsTitle");

        await using var fresh = EmailEvidenceHarness.NewContext();
        Assert.Equal(bodyBefore, (await LoadAsync(fresh, SystemEmailTemplates.AccountEmailConfirmation)).BodyVi);
    }

    [Fact]
    public async Task An_update_that_removes_the_action_block_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var template = await LoadAsync(db, SystemEmailTemplates.VisitParticipantInvitation);

        var ex = await Assert.ThrowsAsync<EmailTemplateContentException>(() =>
            Update(db).Handle(
                ContentEdit(template, "<p>Chào {{recipientName}}, mời bạn tham dự.</p>"),
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateActionBlockRequired, ex.ErrorCode);
    }

    // ── Optimistic concurrency ───────────────────────────────────────────────

    /// <summary>
    /// Two HO users open the same template; the second save must be refused rather than silently
    /// discarding the first person's wording.
    /// </summary>
    [Fact]
    public async Task A_second_editor_saving_over_a_newer_version_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var template = await LoadAsync(db, SystemEmailTemplates.AccountActivated);

        // The token is now the monotonic `revision`, so nothing has to be pinned to a fixed instant:
        // whatever revision the row is on, the value both editors loaded is unambiguous. The version
        // this test used to pin (a second-precision DATETIME) is exactly what the revision column
        // replaced.
        var staleToken = template.Revision;

        // First editor saves.
        var firstBody = $"<p>Bản của người thứ nhất {Guid.NewGuid():N} — {{{{fullName}}}}</p>";
        await Update(db).Handle(ContentEdit(template, firstBody), CancellationToken.None);

        // Second editor saves with the token they loaded BEFORE the first save.
        await using var secondDb = EmailEvidenceHarness.NewContext();
        var reloaded = await LoadAsync(secondDb, SystemEmailTemplates.AccountActivated);
        var stale = ContentEdit(reloaded, "<p>Bản của người thứ hai — {{fullName}}</p>");
        stale.ExpectedRevision = staleToken;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update(secondDb).Handle(stale, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateConcurrencyConflict, ex.ErrorCode);

        await using var fresh = EmailEvidenceHarness.NewContext();
        Assert.Equal(firstBody, (await LoadAsync(fresh, SystemEmailTemplates.AccountActivated)).BodyVi);
    }

    [Fact]
    public async Task An_update_without_a_concurrency_token_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var template = await LoadAsync(db, SystemEmailTemplates.AccountActivated);
        var command = ContentEdit(template, "<p>{{fullName}}</p>");
        command.ExpectedRevision = null;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update(db).Handle(command, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateConcurrencyConflict, ex.ErrorCode);
    }

    /// <summary>The editor's own next save must not be refused as a conflict with itself.</summary>
    [Fact]
    public async Task The_token_returned_by_a_save_is_accepted_by_the_next_one()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var template = await LoadAsync(db, SystemEmailTemplates.AccountActivated);
        var first = await Update(db).Handle(
            ContentEdit(template, "<p>Lần một — {{fullName}}</p>"), CancellationToken.None);

        await using var secondDb = EmailEvidenceHarness.NewContext();
        var reloaded = await LoadAsync(secondDb, SystemEmailTemplates.AccountActivated);
        var next = ContentEdit(reloaded, "<p>Lần hai — {{fullName}}</p>");
        next.ExpectedRevision = first.Revision;

        var second = await Update(secondDb).Handle(next, CancellationToken.None);

        Assert.True(second.Success);
    }

    // ── Historical rows ──────────────────────────────────────────────────────

    /// <summary>
    /// A row whose code is not in the registry is kept — history and drafts reference it — but it is not
    /// editable: nothing in any release sends it, so a change would alter a message that can never go
    /// out again.
    /// </summary>
    [Fact]
    public async Task A_historical_template_is_kept_but_not_editable()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var code = $"HISTORICAL_{Guid.NewGuid():N}"[..40];
        var historical = new EmailTemplate
        {
            TemplateCode = code,
            Name = "Mẫu lịch sử",
            Purpose = "VISIT_REQUEST_VERIFY",
            Status = "ACTIVE",
            SubjectVi = "Tiêu đề cũ",
            BodyVi = "<p>Nội dung cũ</p>",
            BodyFormat = PEMS.Domain.Enums.EmailBodyFormat.HTML,
            CreatedAt = DateTime.Now,
        };
        db.EmailTemplates.Add(historical);
        await db.SaveChangesAsync();

        try
        {
            await using var editDb = EmailEvidenceHarness.NewContext();
            var loaded = await LoadAsync(editDb, code);

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                Update(editDb).Handle(ContentEdit(loaded, "<p>Sửa</p>"), CancellationToken.None));

            Assert.Equal(EmailErrorCodes.TemplateCatalogFixed, ex.ErrorCode);

            await using var fresh = EmailEvidenceHarness.NewContext();
            var after = await LoadAsync(fresh, code);
            Assert.Equal("<p>Nội dung cũ</p>", after.BodyVi);
        }
        finally
        {
            await using var cleanup = EmailEvidenceHarness.NewContext();
            var row = await cleanup.EmailTemplates.FirstOrDefaultAsync(t => t.TemplateCode == code);
            if (row is not null) { cleanup.EmailTemplates.Remove(row); await cleanup.SaveChangesAsync(); }
        }
    }
}
