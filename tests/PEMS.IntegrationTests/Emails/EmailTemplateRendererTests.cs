using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The renderer contract, exercised against a real MySQL disposable database so the EF query, the
/// <c>ACTIVE</c> check and the "no cache" promise are proven rather than asserted about a fake.
///
/// <para>
/// Every negative case here is a way the old code silently produced wrong mail: a missing variable became
/// the literal text "Chưa có thông tin", an untranslated template quietly fell back to Vietnamese, and a
/// value containing markup was written into the HTML body unescaped.
/// </para>
/// <para>Each test runs inside a transaction that is rolled back, so no template row survives it.</para>
/// </summary>
public sealed class EmailTemplateRendererTests
{
    private static string ConnString =>
        PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private static bool? _dbUp;
    private static string? _dbFailure;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try
            {
                // Probe through EF/Pomelo, not a raw MySql.Data connection: the canonical connection
                // string carries Pomelo-only options (GuidFormat) that MySql.Data rejects outright.
                using var db = NewContext();
                _dbUp = db.Database.CanConnect();
            }
            catch (Exception ex)
            {
                // Keep the reason: "not reachable" alone hides a hash mismatch or a failed import, which
                // are the failures worth acting on.
                _dbUp = false;
                _dbFailure = ex.ToString();
            }
        }

        Assert.True(_dbUp, "Disposable MySQL database is not reachable. " + _dbFailure);
    }

    /// <summary>
    /// The code these tests take over. Since the canonical seed now contains all 26 registered codes,
    /// a test cannot simply insert one — it replaces the seeded row inside a transaction it rolls back,
    /// so it controls the exact content under test without leaving anything behind.
    /// </summary>
    private const string Code = SystemEmailTemplates.AccountActivated;

    /// <summary>
    /// Removes the seeded row for <see cref="Code"/> and installs <paramref name="row"/> in its place.
    /// Always called inside a transaction that is rolled back.
    /// </summary>
    private static async Task ReplaceSeededAsync(ApplicationDbContext db, EmailTemplate? row)
    {
        var seeded = await db.EmailTemplates.FirstOrDefaultAsync(t => t.TemplateCode == Code);
        if (seeded is not null) db.EmailTemplates.Remove(seeded);
        await db.SaveChangesAsync();

        if (row is not null)
        {
            db.EmailTemplates.Add(row);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// A row under this class's control.
    ///
    /// <para>
    /// Bodies keep whatever trusted block <see cref="Code"/>'s contract requires — the renderer refuses a
    /// body that has lost one, and these tests are about encoding, language selection and placeholder
    /// reporting, not about that rule. Tests that mean to remove the block pass
    /// <paramref name="keepRequiredBlocks"/> false and say so.
    /// </para>
    /// </summary>
    private static EmailTemplate Row(
        string? subjectVi = "Xin chào {{fullName}}",
        string? bodyVi = "<p>Xin chào {{fullName}}, vai trò {{roleName}} tại {{campusName}}.</p>",
        string? subjectEn = "Hello {{fullName}}",
        string? bodyEn = "<p>Hello {{fullName}}, role {{roleName}} at {{campusName}}.</p>",
        string? variables = "fullName, roleName, campusName",
        string status = "ACTIVE",
        EmailBodyFormat format = EmailBodyFormat.HTML,
        bool keepRequiredBlocks = true)
        => new()
        {
            TemplateCode = Code,
            Name = "Test — account activated",
            Purpose = EmailTemplatePurposes.Account,
            Status = status,
            SubjectVi = subjectVi,
            BodyVi = keepRequiredBlocks && bodyVi is not null
                ? EmailContractFixture.BodyWithRequiredBlocks(Code, bodyVi) : bodyVi,
            SubjectEn = subjectEn,
            BodyEn = keepRequiredBlocks && bodyEn is not null
                ? EmailContractFixture.BodyWithRequiredBlocks(Code, bodyEn) : bodyEn,
            BodyFormat = format,
            VariablesText = variables,
            CreatedAt = DateTime.Now,
        };

    private static Dictionary<string, string> Vars(
        string fullName = "Nguyễn Văn A", string roleName = "Staff", string campusName = "HCM")
        => new() { ["fullName"] = fullName, ["roleName"] = roleName, ["campusName"] = campusName };

    private static EmailRenderRequest Request(
        IReadOnlyDictionary<string, string>? variables = null,
        string language = EmailLanguages.Vi,
        IReadOnlyDictionary<string, string>? trusted = null)
        => new(Code, language, variables ?? Vars(), trusted);

    /// <summary>
    /// The same request with a value for every trusted block the contract requires, resolved by the real
    /// resolver. Used by the tests whose subject is something else; the ones that deliberately withhold a
    /// block keep calling <see cref="Request"/>.
    /// </summary>
    private static async Task<EmailRenderRequest> RequestWithBlocksAsync(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, string>? variables = null,
        string language = EmailLanguages.Vi,
        IReadOnlyDictionary<string, string>? trusted = null)
        => new(Code, language, variables ?? Vars(),
               await EmailContractFixture.TrustedBlocksAsync(db, Code, language, trusted));

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Renders_subject_and_body_from_the_database_row()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var result = await new EmailTemplateRenderer(db).RenderAsync(await RequestWithBlocksAsync(db));

        Assert.Equal(Code, result.TemplateCode);
        Assert.Equal(EmailLanguages.Vi, result.LanguageUsed);
        Assert.Equal("Xin chào Nguyễn Văn A", result.Subject);
        Assert.Contains("vai trò Staff tại HCM", result.Body);
        Assert.True(result.EmailTemplateId > 0);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Renders_the_english_content_when_english_is_requested()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var result = await new EmailTemplateRenderer(db).RenderAsync(await RequestWithBlocksAsync(db, language: EmailLanguages.En));

        Assert.Equal(EmailLanguages.En, result.LanguageUsed);
        Assert.Equal("Hello Nguyễn Văn A", result.Subject);
        Assert.Contains("role Staff at HCM", result.Body);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_edit_to_the_row_is_visible_on_the_very_next_render()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var row = Row();
        await ReplaceSeededAsync(db, row);

        var renderer = new EmailTemplateRenderer(db);
        var before = await renderer.RenderAsync(await RequestWithBlocksAsync(db));
        Assert.Equal("Xin chào Nguyễn Văn A", before.Subject);

        // An operator edits the template. No deploy, no restart, no cache to invalidate.
        row.SubjectVi = "Kính gửi {{fullName}}";
        await db.SaveChangesAsync();

        var after = await renderer.RenderAsync(await RequestWithBlocksAsync(db));
        Assert.Equal("Kính gửi Nguyễn Văn A", after.Subject);

        await tx.RollbackAsync();
    }

    // ── Resolution failures ──────────────────────────────────────────────────

    [Fact]
    public async Task An_unregistered_code_is_rejected_before_the_database_is_consulted()
    {
        RequireDb();
        using var db = NewContext();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(
                new EmailRenderRequest("NOT_A_REGISTERED_CODE", EmailLanguages.Vi, Vars())));

        Assert.Equal(EmailErrorCodes.TemplateNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task A_registered_code_with_no_row_fails_instead_of_falling_back_to_hard_coded_content()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Remove the seeded row and put nothing back: a registered caller whose template is missing
        // must fail loudly rather than reach for hard-coded content.
        await ReplaceSeededAsync(db, null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(Request()));

        Assert.Equal(EmailErrorCodes.TemplateNotFound, ex.ErrorCode);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_inactive_template_is_refused()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row(status: "INACTIVE"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(Request()));

        Assert.Equal(EmailErrorCodes.TemplateInactive, ex.ErrorCode);

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData(true, false)]  // missing EN subject
    [InlineData(false, true)]  // missing EN body
    public async Task A_half_translated_template_fails_rather_than_silently_serving_vietnamese(
        bool clearSubject, bool clearBody)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row(
            subjectEn: clearSubject ? null : "Hello {{fullName}}",
            bodyEn: clearBody ? null : "<p>Hello {{fullName}}, role {{roleName}} at {{campusName}}.</p>"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(Request(language: EmailLanguages.En)));

        Assert.Equal(EmailErrorCodes.TemplateLanguageContentMissing, ex.ErrorCode);

        await tx.RollbackAsync();
    }

    // ── Variable contract ────────────────────────────────────────────────────

    [Fact]
    public async Task A_missing_variable_is_an_error_not_a_placeholder_message()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var incomplete = new Dictionary<string, string> { ["fullName"] = "A", ["roleName"] = "Staff" };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(Request(incomplete)));

        Assert.Equal(EmailErrorCodes.TemplateVariableMissing, ex.ErrorCode);
        Assert.Contains("campusName", ex.Message);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_undeclared_variable_is_an_error()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var extra = Vars();
        extra["somethingElse"] = "x";

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(Request(extra)));

        Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, ex.ErrorCode);
        Assert.Contains("somethingElse", ex.Message);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_pascal_case_key_does_not_satisfy_a_camel_case_declaration()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var wrongCase = new Dictionary<string, string>
        {
            ["FullName"] = "A", ["roleName"] = "Staff", ["campusName"] = "HCM",
        };

        // Accepting this would let the legacy PascalCase spellings survive indefinitely.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(Request(wrongCase)));

        Assert.Equal(EmailErrorCodes.TemplateVariableMissing, ex.ErrorCode);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_placeholder_the_template_never_declared_is_reported_not_shipped()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // The body references {{RequestCode}} but variables_text does not declare it — the exact drift
        // the legacy seed contained.
        await ReplaceSeededAsync(db, Row(
            bodyVi: "<p>Xin chào {{fullName}} — đơn {{RequestCode}} ({{roleName}}, {{campusName}}).</p>"));

        var request = await RequestWithBlocksAsync(db);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(request));

        Assert.Equal(EmailErrorCodes.TemplateUnresolvedPlaceholder, ex.ErrorCode);
        Assert.Contains("RequestCode", ex.Message);

        await tx.RollbackAsync();
    }

    // ── Encoding and injection ───────────────────────────────────────────────

    [Fact]
    public async Task A_variable_containing_markup_is_encoded_in_an_html_body()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var result = await new EmailTemplateRenderer(db)
            .RenderAsync(await RequestWithBlocksAsync(db, Vars(fullName: "<script>alert('x')</script>")));

        Assert.DoesNotContain("<script>", result.Body);
        Assert.Contains("&lt;script&gt;", result.Body);

        await tx.RollbackAsync();
    }

    /// <summary>
    /// A multi-line value keeps its lines. HTML treats a newline as whitespace, so before this a
    /// logistics description written as three instructions arrived as one run-on sentence.
    ///
    /// <para>
    /// These three tests deliberately drive <c>roleName</c>, which <see cref="Row"/> writes into the BODY
    /// only. A newline in <c>fullName</c> would never reach the break logic at all: that variable is in
    /// the subject too, and a subject carrying a line break is refused outright as a header injection —
    /// see <c>A_variable_carrying_a_newline_cannot_break_the_subject_header</c>. Using it here would have
    /// tested that older rule a fourth time instead of this one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_multi_line_variable_keeps_its_line_breaks_in_an_html_body()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var result = await new EmailTemplateRenderer(db).RenderAsync(
            await RequestWithBlocksAsync(db, Vars(roleName: "Dòng một\nDòng hai\n\nDòng bốn")));

        // Encode the expected text: a VALUE goes through HtmlEncode, which renders every non-ASCII
        // character as a numeric entity ("Dòng" → "D&#242;ng"). Only the breaks are literal markup.
        var e = (string s) => System.Net.WebUtility.HtmlEncode(s);
        Assert.Contains($"{e("Dòng một")}<br />{e("Dòng hai")}<br /><br />{e("Dòng bốn")}", result.Body);
        // The raw newline is gone: had it survived, the mail would render as a single line.
        Assert.DoesNotContain("\n" + e("Dòng hai"), result.Body);

        await tx.RollbackAsync();
    }

    /// <summary>
    /// CRLF and CR produce ONE break each, not two and not none — a description typed on Windows or
    /// pasted from a document must not double-space itself.
    /// </summary>
    [Theory]
    [InlineData("Một\r\nHai")]
    [InlineData("Một\rHai")]
    [InlineData("Một\nHai")]
    public async Task Every_newline_convention_becomes_exactly_one_break(string value)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var result = await new EmailTemplateRenderer(db)
            .RenderAsync(await RequestWithBlocksAsync(db, Vars(roleName: value)));

        var e = (string s) => System.Net.WebUtility.HtmlEncode(s);
        Assert.Contains($"{e("Một")}<br />Hai", result.Body);
        Assert.DoesNotContain($"{e("Một")}<br /><br />Hai", result.Body);

        await tx.RollbackAsync();
    }

    /// <summary>
    /// The order of encode-then-break is the security property, not a detail. A value carrying BOTH
    /// markup and newlines must come out with its script inert and its lines intact — the only tag in
    /// the result is the one the renderer wrote.
    /// </summary>
    [Fact]
    public async Task A_multi_line_variable_is_still_encoded_before_its_breaks_are_added()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        var result = await new EmailTemplateRenderer(db).RenderAsync(await RequestWithBlocksAsync(
            db, Vars(roleName: "<script>alert('x')</script>\n<img src=x onerror=alert(1)>")));

        Assert.DoesNotContain("<script>", result.Body);
        Assert.DoesNotContain("onerror=alert(1)>", result.Body);
        Assert.Contains("&lt;script&gt;", result.Body);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", result.Body);
        // Encoded first, so the break is a real tag rather than the text "&lt;br /&gt;".
        Assert.Contains("&lt;/script&gt;<br />&lt;img", result.Body);
        Assert.DoesNotContain("&lt;br /&gt;", result.Body);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_subject_variable_is_not_html_encoded()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        // A subject is a header, not markup: encoding here would show the recipient "A &amp; B".
        var result = await new EmailTemplateRenderer(db).RenderAsync(await RequestWithBlocksAsync(db, Vars(fullName: "A & B")));

        Assert.Equal("Xin chào A & B", result.Subject);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_variable_carrying_a_newline_cannot_break_the_subject_header()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row());

        // The blocks are supplied so the ONLY thing wrong with this render is the header injection —
        // otherwise the refusal comes from the missing contact block and the test proves nothing.
        var request = await RequestWithBlocksAsync(db, Vars(fullName: "A\r\nBcc: attacker@evil.test"));
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(request));

        Assert.Equal(EmailErrorCodes.HeaderInvalid, ex.ErrorCode);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_plain_text_body_is_not_html_encoded()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row(
            bodyVi: "Xin chào {{fullName}} — {{roleName}} tại {{campusName}}",
            format: EmailBodyFormat.PLAIN_TEXT));

        var result = await new EmailTemplateRenderer(db).RenderAsync(await RequestWithBlocksAsync(db, Vars(fullName: "A & B")));

        Assert.Equal(EmailBodyFormat.PLAIN_TEXT, result.BodyFormat);
        Assert.Contains("A & B", result.Body);
        Assert.DoesNotContain("&amp;", result.Body);

        await tx.RollbackAsync();
    }

    // ── Trusted blocks ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_trusted_block_is_injected_as_markup_without_being_declared_as_a_variable()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row(
            bodyVi: "<p>Xin chào {{fullName}} ({{roleName}}, {{campusName}})</p>{{actionBlock}}"));

        var trusted = new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] = "<a href=\"https://pems.test/accept/abc\">Chấp nhận</a>",
        };

        var result = await new EmailTemplateRenderer(db).RenderAsync(await RequestWithBlocksAsync(db, trusted: trusted));

        // The backend's own markup survives intact…
        Assert.Contains("<a href=\"https://pems.test/accept/abc\">", result.Body);
        // …and it did not have to be declared in variables_text to do so.
        Assert.DoesNotContain("{{actionBlock}}", result.Body);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_body_referencing_a_trusted_block_that_the_caller_did_not_supply_is_reported()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        await ReplaceSeededAsync(db, Row(
            bodyVi: "<p>Xin chào {{fullName}} ({{roleName}}, {{campusName}})</p>{{actionBlock}}"));

        // Every OTHER required block is supplied, so the one withheld block is the only thing left to
        // report. Passing nothing at all would leave two unresolved and prove only that one of them was.
        var request = await RequestWithBlocksAsync(db);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new EmailTemplateRenderer(db).RenderAsync(request));

        Assert.Equal(EmailErrorCodes.TemplateUnresolvedPlaceholder, ex.ErrorCode);
        Assert.Contains(EmailTrustedBlocks.ActionBlock, ex.Message);

        await tx.RollbackAsync();
    }

    // ── variables_text parsing ───────────────────────────────────────────────

    [Theory]
    [InlineData("fullName, roleName, campusName")]
    [InlineData("fullName,roleName,campusName")]
    [InlineData("{{fullName}}, {{roleName}}, {{campusName}}")]
    [InlineData("fullName;roleName;campusName")]
    public void Declared_variables_parse_from_every_shape_the_seed_uses(string variablesText)
    {
        var parsed = EmailTemplateVariables.ParseDeclared(variablesText);

        Assert.Equal(3, parsed.Count);
        Assert.Contains("fullName", parsed);
        Assert.Contains("roleName", parsed);
        Assert.Contains("campusName", parsed);
    }

    [Fact]
    public void A_template_declaring_no_variables_parses_to_an_empty_set()
    {
        Assert.Empty(EmailTemplateVariables.ParseDeclared(null));
        Assert.Empty(EmailTemplateVariables.ParseDeclared("   "));
    }
}
