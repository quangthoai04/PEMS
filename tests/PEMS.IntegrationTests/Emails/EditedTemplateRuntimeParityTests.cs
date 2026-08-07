using System;
using System.Collections.Generic;
using System.Net;
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
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// What an operator saves in the template editor, rendered by the real renderer with real values.
///
/// <para>
/// <b>The gap this closes.</b> Everything about the editor was proved in the browser — the chip, the
/// table node, the preview pane — and everything about the renderer was proved against bodies written by
/// hand in a test. Nothing joined the two, so "the editor works" and "the renderer works" could both be
/// true while a template edited on screen produced a broken message: a system block written as a
/// position node the renderer never substitutes, a colour dropped by the sanitiser, a variable the
/// contract offers and no caller supplies. The body below is exactly what the editor emits — the same
/// string its own save-payload test asserts — so a change on either side that breaks the join fails here.
/// </para>
/// <para>
/// Runs against the disposable MySQL database, inside a transaction that is rolled back, replacing the
/// seeded row for one code so the content under test is controlled without leaving anything behind.
/// </para>
/// </summary>
public sealed class EditedTemplateRuntimeParityTests
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
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch (Exception ex) { _dbUp = false; _dbFailure = ex.ToString(); }
        }

        Assert.True(_dbUp, "Disposable MySQL database is not reachable. " + _dbFailure);
    }

    /// <summary>
    /// A template that exercises every join at once: it declares business variables, permits the six
    /// sender variables, and its registry entry carries an action spec — so a body edited on screen can
    /// legitimately contain all three kinds of placeholder.
    /// </summary>
    private const string Code = SystemEmailTemplates.VisitParticipantInvitation;

    /// <summary>The action block the caller builds at send time. Inert here — a test mints no token.</summary>
    private const string ActionBlockHtml =
        "<!-- PEMS_ACTION_BLOCK_START --><div><a href=\"https://pems.fpt.edu.vn/a/xyz\">Đồng ý</a>"
        + "<a href=\"https://pems.fpt.edu.vn/d/xyz\">Từ chối</a></div><!-- PEMS_ACTION_BLOCK_END -->";

    /// <summary>
    /// The body as the EDITOR emits it: every format the toolbar offers, a variable, a sender variable,
    /// a table with a variable in a cell, a divider, a list, and the system block as its placeholder.
    /// </summary>
    private static string EditedBody() =>
        "<p style=\"text-align: center;\"><span style=\"font-size: 18px; color: rgb(255, 0, 0);\">"
        + "Kính gửi {{recipientName}}</span></p>"
        + "<hr style=\"border:none;border-top:1px solid #e2e8f0;margin:20px 0\">"
        + "<ul><li>Đoàn: {{delegationName}}</li><li>Cơ sở: {{campusName}}</li></ul>"
        + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\""
        + " style=\"border-collapse:collapse;width:100%;margin:16px 0\"><tbody>"
        + "<tr><th style=\"border:1px solid #dbe4ee;padding:8px 10px;background:#f8fafc\">Thời gian</th>"
        + "<th style=\"border:1px solid #dbe4ee;padding:8px 10px;background:#f8fafc\">Vai trò</th></tr>"
        + "<tr><td style=\"border:1px solid #dbe4ee;padding:8px 10px\">{{plannedTime}}</td>"
        + "<td style=\"border:1px solid #dbe4ee;padding:8px 10px\">{{roleLabel}}</td></tr>"
        + "</tbody></table>"
        + "<p><em>{{hostMessage}}</em></p>"
        + "{{actionBlock}}"
        + "<p>Trân trọng,<br><strong>{{senderName}}</strong> — {{senderRole}}<br>"
        + "{{senderEmail}} · {{senderPhone}}<br>{{senderDepartment}}, {{senderCampus}}</p>";

    /// <summary>Every variable the registry declares, each with a value a person would recognise.</summary>
    private static Dictionary<string, string> Values()
    {
        var declared = SystemEmailTemplates.Find(Code)!.DeclaredVariables
            .Where(v => !EmailTrustedBlocks.All.Contains(v, StringComparer.Ordinal));

        var known = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["recipientName"] = "Nguyễn Văn An",
            ["delegationName"] = "Đoàn THPT Chu Văn An",
            ["campusName"] = "FPTU Hà Nội",
            ["plannedTime"] = "09:00 20/08/2026",
            ["hostName"] = "Trần Thị Bình",
            ["roleLabel"] = "Khách mời",
            ["hostMessage"] = "Rất mong anh/chị thu xếp tham dự.",
            ["senderName"] = "Trần Thị Bình",
            ["senderRole"] = "Trưởng phòng Hợp tác Quốc tế",
            ["senderEmail"] = "binh.tran@fpt.edu.vn",
            ["senderPhone"] = "0901234567",
            ["senderDepartment"] = "Phòng Hợp tác Quốc tế",
            ["senderCampus"] = "FPTU Hà Nội",
        };

        // Anything the registry declares that this test has no wording for still gets a value: the
        // renderer refuses a supplied set that does not match the declared set exactly, in both
        // directions, and a test that skipped one would be reporting that rule rather than this one.
        return declared.ToDictionary(name => name, name => known.TryGetValue(name, out var v) ? v : $"[{name}]",
            StringComparer.Ordinal);
    }

    private static EmailTemplate Row(string bodyVi) => new()
    {
        TemplateCode = Code,
        Name = "Test — participant invitation",
        Purpose = EmailTemplatePurposes.VisitParticipant,
        Status = "ACTIVE",
        SubjectVi = "Thư mời {{recipientName}} — {{delegationName}}",
        BodyVi = bodyVi,
        SubjectEn = "Invitation for {{recipientName}} — {{delegationName}}",
        BodyEn = bodyVi,
        BodyFormat = EmailBodyFormat.HTML,
        VariablesText = string.Join(", ", SystemEmailTemplates.Find(Code)!.DeclaredVariables),
        CreatedAt = DateTime.Now,
    };

    private static async Task ReplaceSeededAsync(ApplicationDbContext db, EmailTemplate row)
    {
        var seeded = await db.EmailTemplates.FirstOrDefaultAsync(t => t.TemplateCode == Code);
        if (seeded is not null) db.EmailTemplates.Remove(seeded);
        await db.SaveChangesAsync();

        db.EmailTemplates.Add(row);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The rendered body with entities decoded, for assertions about VALUES.
    ///
    /// <para>
    /// A variable value is HTML-encoded on its way into an HTML body — that is the escape hatch closing,
    /// and it is why a pasted <c>&lt;script&gt;</c> is inert text by the time it reaches a recipient.
    /// `WebUtility.HtmlEncode` writes anything in the Latin-1 range as a numeric reference, so "Đoàn"
    /// arrives as "Đo&amp;#224;n" — correct on screen in every mail client, and unrecognisable to a
    /// substring match. Assertions about MARKUP stay on the raw body, where the difference matters.
    /// </para>
    /// </summary>
    private static string Decoded(string html) => WebUtility.HtmlDecode(html);

    private static async Task<EmailRenderResult> RenderAsync(ApplicationDbContext db, string body)
    {
        await ReplaceSeededAsync(db, Row(body));

        return await new EmailTemplateRenderer(db).RenderAsync(new EmailRenderRequest(
            Code,
            EmailLanguages.Vi,
            Values(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EmailTrustedBlocks.ActionBlock] = ActionBlockHtml,
            }));
    }

    // ── §32 the whole chain, on one body ─────────────────────────────────────

    [Fact]
    public async Task An_edited_template_renders_with_every_value_resolved()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var result = await RenderAsync(db, EditedBody());

        // Business variables, in the body and in the subject.
        Assert.Contains("Nguyễn Văn An", Decoded(result.Body));
        Assert.Contains("Đoàn THPT Chu Văn An", Decoded(result.Body));
        Assert.Contains("Rất mong anh/chị thu xếp tham dự.", Decoded(result.Body));
        Assert.Contains("Nguyễn Văn An", result.Subject);

        // Sender variables — the actor who pressed send, not a sample.
        Assert.Contains("Trần Thị Bình", Decoded(result.Body));
        Assert.Contains("binh.tran@fpt.edu.vn", Decoded(result.Body));
        Assert.Contains("Phòng Hợp tác Quốc tế", Decoded(result.Body));

        // Nothing wearing braces reaches a recipient.
        Assert.DoesNotContain("{{", result.Body);
        Assert.DoesNotContain("{{", result.Subject);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task The_system_block_is_rendered_where_the_author_put_it()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var result = await RenderAsync(db, EditedBody());

        Assert.Contains("Đồng ý", result.Body);
        Assert.Contains("https://pems.fpt.edu.vn/a/xyz", result.Body);
        Assert.DoesNotContain("{{actionBlock}}", result.Body);

        // At the placeholder's position: after the host's message, before the signature. Appending it
        // instead — which is what happens when the placeholder is missing — would put the buttons under
        // the signature, and the sentence that introduces them would point at nothing.
        Assert.True(
            result.Body.IndexOf("Đồng ý", StringComparison.Ordinal)
            > result.Body.IndexOf("Rất mong", StringComparison.Ordinal),
            "The action block was rendered before the sentence that introduces it.");
        Assert.True(
            result.Body.IndexOf("Đồng ý", StringComparison.Ordinal)
            < result.Body.IndexOf("Trân trọng", StringComparison.Ordinal),
            "The action block was rendered after the signature rather than at its placeholder.");

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData("text-align: center")]
    [InlineData("font-size: 18px")]
    [InlineData("color: rgb(255, 0, 0)")]
    [InlineData("<hr")]
    [InlineData("border-top:1px solid #e2e8f0")]
    [InlineData("<ul>")]
    [InlineData("<li>")]
    [InlineData("<table")]
    [InlineData("role=\"presentation\"")]
    [InlineData("border:1px solid #dbe4ee")]
    [InlineData("padding:8px 10px")]
    [InlineData("<strong>")]
    [InlineData("<em>")]
    public async Task Formatting_survives_the_render(string fragment)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var result = await RenderAsync(db, EditedBody());

        Assert.Contains(fragment, result.Body);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_variable_in_a_table_cell_is_substituted_like_any_other()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var result = await RenderAsync(db, EditedBody());

        // Inside the cell it was written in, not appended somewhere else.
        Assert.Contains("<td style=\"border:1px solid #dbe4ee;padding:8px 10px\">09:00 20/08/2026</td>", result.Body);
        Assert.Contains("Khách mời", Decoded(result.Body));

        await tx.RollbackAsync();
    }

    // ── §25 / §26 a template that lost something fails CLOSED ────────────────

    /**
     * The editor cannot produce this, and that is the point: the check has to hold for a body that
     * reached the row some other way — a hand-edited database, a half-applied migration, a sync script.
     */
    [Fact]
    public async Task A_placeholder_no_caller_supplies_blocks_the_send()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Declared stays as the registry says, so the supplied set still matches; the BODY is what
        // acquired a name nobody knows.
        var body = EditedBody().Replace("{{hostMessage}}", "{{unknownVariable}}", StringComparison.Ordinal);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => RenderAsync(db, body));

        Assert.Equal(EmailErrorCodes.TemplateUnresolvedPlaceholder, error.ErrorCode);

        await tx.RollbackAsync();
    }

    /// <summary>
    /// The system block written as the COMPOSE position node instead of the placeholder — precisely what
    /// the template editor used to insert. The renderer substitutes `{{actionBlock}}`, so a body carrying
    /// the node has nowhere to put the buttons: the message would go out with an empty div where its
    /// action area belongs. Refused instead, by the block-presence check.
    /// </summary>
    [Fact]
    public async Task A_body_carrying_the_compose_node_instead_of_the_placeholder_is_refused()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var body = EditedBody().Replace(
            "{{actionBlock}}", EmailSystemBlockNodes.ActionNodeHtml, StringComparison.Ordinal);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => RenderAsync(db, body));

        // Named, so the operator is told what to repair rather than left with a button-less message.
        Assert.Equal(EmailErrorCodes.ActionBlockMalformed, error.ErrorCode);
        Assert.Contains("{{actionBlock}}", error.Message);

        await tx.RollbackAsync();
    }
}
