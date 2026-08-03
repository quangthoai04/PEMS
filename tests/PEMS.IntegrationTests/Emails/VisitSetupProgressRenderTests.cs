using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// What the setup-progress mail does when its stored template and the code that fills it disagree.
///
/// <para>
/// The defect these pin down was invisible. <c>pems_db</c> carried a revision of
/// <c>VISIT_SETUP_PROGRESS_UPDATE</c> from before the setup tables moved into the body: byte-identical
/// to canonical except that <c>{{setupSummaryBlock}}</c> had been dropped. Nothing complained.
/// Substitution had no placeholder to replace, so the unresolved-placeholder guard saw no leftover
/// braces and the variable contract saw no missing variable — and the mail went out telling the guest
/// "here is the latest update on preparations" with no update in it.
/// </para>
/// <para>
/// The three states below are the whole story: the template as canonical ships it, the template with
/// the block deleted, and the template rendered by a caller that forgot to build the block. Each has to
/// fail — or succeed — in its own distinguishable way, because they need three different repairs.
/// </para>
/// </summary>
public sealed class VisitSetupProgressRenderTests
{
    private const string Code = SystemEmailTemplates.VisitSetupProgressUpdate;

    /// <summary>Stands in for the tables VisitSetupEmailHtml builds — markup a variable may never carry.</summary>
    private const string Tables =
        "<h3>1. Thông tin chung</h3><table><tr><td>Tên đoàn</td><td>Đoàn A</td></tr></table>";

    /// <summary>
    /// Exactly the variables the registry declares for this template — read from it, not restated.
    ///
    /// <para>
    /// This list used to be typed out here and included <c>hostEmail</c>. That variable was withdrawn
    /// when <c>{{contactInformationBlock}}</c> took over printing the Host's address, and a copy of a
    /// contract is a copy that goes stale: the fixture kept supplying a variable the template no longer
    /// declares, which the renderer refuses.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> BuildVariables()
    {
        var contract = EmailTemplateContracts.For(Code)!;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["delegationName"] = "Đoàn Đại học Kyoto",
            ["campusName"] = "FPT Hà Nội",
            ["plannedStart"] = "09:00 12/08/2026",
            ["plannedEnd"] = "11:30 12/08/2026",
            ["hostName"] = "Nguyễn Văn A",
        };

        // Anything the contract adds later still gets a value, so a new variable fails on its own
        // meaning rather than on this fixture being out of date.
        foreach (var name in contract.AllowedVariables)
            if (!EmailTrustedBlocks.All.Contains(name) && !values.ContainsKey(name))
                values[name] = "giá trị kiểm thử";

        return values;
    }

    private static readonly Dictionary<string, string> Variables = BuildVariables();

    private static EmailTemplateRenderer Renderer(ApplicationDbContext db) => new(db);

    /// <summary>
    /// Every trusted block this template requires — the summary tables AND the contact block.
    ///
    /// <para>
    /// Both, because the template requires both, and a test that supplies one of two proves nothing
    /// about the one it withheld: the refusal it asserts on would be about the other. The contact block
    /// is resolved by the real resolver rather than written out here.
    /// </para>
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> WithBlockAsync(ApplicationDbContext db)
        => await EmailContractFixture.TrustedBlocksAsync(
            db, Code, "vi",
            new Dictionary<string, string>(StringComparer.Ordinal) { [EmailTrustedBlocks.SetupSummaryBlock] = Tables });

    /// <summary>
    /// The summary block withheld, everything else supplied — so "the caller forgot the block" is about
    /// the summary block and nothing else.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> NoSummaryBlockAsync(ApplicationDbContext db)
        => await EmailContractFixture.TrustedBlocksAsync(db, Code, "vi");

    /// <summary>The canonical bodies, read from the shipped defaults rather than restated here.</summary>
    private static string CanonicalVi => EmailTemplateDefaults.For(Code)!.BodyVi!;
    private static string CanonicalEn => EmailTemplateDefaults.For(Code)!.BodyEn!;

    /// <summary>Exactly what pems_db held: canonical with the block segment removed, nothing else changed.</summary>
    private static string DriftedVi => CanonicalVi.Replace("{{setupSummaryBlock}}", "");
    private static string DriftedEn => CanonicalEn.Replace("{{setupSummaryBlock}}", "");

    /// <summary>
    /// Borrows the seeded template row, points it at the bodies this test is about, and always puts the
    /// original content back. The row belongs to the seeded catalog — inserting a rival one collides on
    /// the unique code, and deleting it would leave the suites that assert on the real seed with nothing.
    /// </summary>
    private static Task WithBodiesAsync(
        ApplicationDbContext db, string bodyVi, string bodyEn, Func<Task> body, string? subjectVi = null)
        => EmailEvidenceHarness.WithTemplateAsync(db, Code, row =>
        {
            row.BodyVi = bodyVi;
            row.BodyEn = bodyEn;
            if (subjectVi is not null) row.SubjectVi = subjectVi;
        }, body);

    // ── A. The template as canonical ships it ────────────────────────────────

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public async Task The_canonical_template_renders_the_tables_and_leaves_no_placeholder(string language)
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        await WithBodiesAsync(db, CanonicalVi, CanonicalEn, async () =>
        {
            var rendered = await Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, language, Variables, await WithBlockAsync(db)), CancellationToken.None);

            // The reported symptom, in both languages: nothing reaches a recipient still wearing braces.
            Assert.DoesNotContain("{{", rendered.Body);
            Assert.DoesNotContain("}}", rendered.Body);
            Assert.DoesNotContain("{{", rendered.Subject);

            // The tables are there AS MARKUP — the whole reason this is a trusted block, not a variable.
            Assert.Contains("<table>", rendered.Body);
            Assert.Contains("Thông tin chung", rendered.Body);

            // …and the ordinary variables really were substituted, not merely stripped.
            //
            // Compared in ENCODED form: an HTML body encodes every variable value, and WebUtility
            // encodes non-ASCII as numeric references, so "Đoàn" reaches the recipient's client as
            // "&#272;o&#224;n" and renders correctly there. Asserting the raw text would be asserting
            // that the encoding is absent, which is the opposite of what this template must do.
            Assert.Contains(WebUtility.HtmlEncode("Đoàn Đại học Kyoto"), rendered.Body);
            Assert.Contains(WebUtility.HtmlEncode("FPT Hà Nội"), rendered.Body);
            Assert.Contains(WebUtility.HtmlEncode("Nguyễn Văn A"), rendered.Body);
        });
    }

    [Fact]
    public async Task Preview_and_send_render_the_same_body_from_the_same_variables()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        await WithBodiesAsync(db, CanonicalVi, CanonicalEn, async () =>
        {
            // There is one renderer and one variable set; "preview" and "send" differ only in what the
            // caller does with the result. Rendering twice must therefore be indistinguishable — if a
            // second table of sample values ever creeps back in, this is what catches it.
            var first = await Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, "vi", Variables, await WithBlockAsync(db)), CancellationToken.None);
            var second = await Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, "vi", Variables, await WithBlockAsync(db)), CancellationToken.None);

            Assert.Equal(first.Subject, second.Subject);
            Assert.Equal(first.Body, second.Body);
        });
    }

    // ── B. The drift that was silent ─────────────────────────────────────────

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public async Task A_body_that_lost_the_block_is_refused_instead_of_quietly_dropping_the_tables(
        string language)
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        await WithBodiesAsync(db, DriftedVi, DriftedEn, async () =>
        {
            var blocks = await WithBlockAsync(db);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, language, Variables, blocks), CancellationToken.None));

            // Its own code: the repair is "re-sync this row", not "fix the caller" and not "retry".
            Assert.Equal(EmailErrorCodes.TemplateRequiredBlockNotInBody, error.ErrorCode);
            Assert.Contains(EmailTrustedBlocks.SetupSummaryBlock, error.Message);
        });
    }

    [Fact]
    public async Task The_refusal_names_the_repair_without_quoting_the_content()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        await WithBodiesAsync(db, DriftedVi, DriftedEn, async () =>
        {
            var blocks = await WithBlockAsync(db);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, "vi", Variables, blocks), CancellationToken.None));

            // An operator reads this on a template screen. It must not echo the guest's data back.
            Assert.DoesNotContain("Đoàn Đại học Kyoto", error.Message);
            Assert.DoesNotContain(Tables, error.Message);
            Assert.Contains("đồng bộ", error.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── C. The caller that forgets the block ─────────────────────────────────

    [Fact]
    public async Task A_caller_that_builds_no_block_still_hits_the_unresolved_placeholder_guard()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        await WithBodiesAsync(db, CanonicalVi, CanonicalEn, async () =>
        {
            // The reported error reproduced exactly: canonical body, no block supplied. It stays a
            // fail-closed refusal — the new guard must not have replaced or weakened it.
            var blocks = await NoSummaryBlockAsync(db);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, "vi", Variables, blocks), CancellationToken.None));

            Assert.Equal(EmailErrorCodes.TemplateUnresolvedPlaceholder, error.ErrorCode);
            Assert.Contains(EmailTrustedBlocks.SetupSummaryBlock, error.Message);
        });
    }

    // ── C2. The whole matrix, fail-closed in every cell ──────────────────────
    //
    // The tests above each pin one cell and assert what the operator reads. This pins the SHAPE: two
    // independent faults — a row that lost the placeholder, and a caller that built no block — in all
    // four combinations. The fourth cell is the one worth having: both faults at once. Nothing is left
    // to substitute AND nothing is left unresolved, so every downstream guard is satisfied and the send
    // would succeed with the tables missing. It is caught because the body check asks the contract, not
    // the caller — a template whose content IS a trusted block is unsendable without it either way.

    [Theory]
    //          stored body has {{setupSummaryBlock}} | caller supplies the block | expected refusal
    [InlineData(true, true, null)]
    [InlineData(true, false, EmailErrorCodes.TemplateUnresolvedPlaceholder)]
    [InlineData(false, true, EmailErrorCodes.TemplateRequiredBlockNotInBody)]
    [InlineData(false, false, EmailErrorCodes.TemplateRequiredBlockNotInBody)]
    public async Task Every_combination_of_stored_body_and_supplied_block_fails_closed(
        bool bodyCarriesPlaceholder, bool callerSuppliesBlock, string? expectedErrorCode)
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var vi = bodyCarriesPlaceholder ? CanonicalVi : DriftedVi;
        var en = bodyCarriesPlaceholder ? CanonicalEn : DriftedEn;
        var blocks = callerSuppliesBlock ? await WithBlockAsync(db) : await NoSummaryBlockAsync(db);

        await WithBodiesAsync(db, vi, en, async () =>
        {
            var render = () => Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, "vi", Variables, blocks), CancellationToken.None);

            if (expectedErrorCode is null)
            {
                var result = await render();
                Assert.Contains(Tables, result.Body);
                Assert.DoesNotContain("{{", result.Body);
                return;
            }

            var error = await Assert.ThrowsAsync<BusinessRuleException>(render);
            Assert.Equal(expectedErrorCode, error.ErrorCode);

            // Whichever fault fired, the message has to name the block, or the operator is left
            // reading "something is wrong with this template".
            Assert.Contains(EmailTrustedBlocks.SetupSummaryBlock, error.Message);
        });
    }

    [Fact]
    public async Task An_unrelated_stray_placeholder_is_still_refused()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        await WithBodiesAsync(db, CanonicalVi + "<p>{{khongKhaiBao}}</p>", CanonicalEn, async () =>
        {
            var blocks = await WithBlockAsync(db);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, "vi", Variables, blocks), CancellationToken.None));

            Assert.Equal(EmailErrorCodes.TemplateUnresolvedPlaceholder, error.ErrorCode);
            Assert.Contains("khongKhaiBao", error.Message);
        });
    }

    // ── D. What the block may and may not do ─────────────────────────────────

    [Fact]
    public async Task Guest_supplied_markup_is_encoded_while_the_trusted_block_stays_markup()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        await WithBodiesAsync(db, CanonicalVi, CanonicalEn, async () =>
        {
            var hostile = new Dictionary<string, string>(Variables, StringComparer.Ordinal)
            {
                ["delegationName"] = "<script>alert('x')</script>",
            };

            var rendered = await Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, "vi", hostile, await WithBlockAsync(db)), CancellationToken.None);

            // The variable is text and is encoded; the block is the ONLY route by which markup enters.
            Assert.DoesNotContain("<script>", rendered.Body);
            Assert.Contains("&lt;script&gt;", rendered.Body);
            Assert.Contains("<table>", rendered.Body);
        });
    }

    [Fact]
    public async Task The_block_may_not_be_rendered_into_the_subject()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        // A subject is stored and shown in history lists; a block there would put a table into both.
        // The refusal is total rather than a strip: the render fails and nothing is sent or recorded,
        // which is the stronger guarantee — a template edited into this shape is a configuration fault
        // to fix, not something to quietly clean up on the way out.
        await WithBodiesAsync(db, CanonicalVi, CanonicalEn, async () =>
        {
            var blocks = await WithBlockAsync(db);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Renderer(db).RenderAsync(
                new EmailRenderRequest(Code, "vi", Variables, blocks), CancellationToken.None));

            Assert.Equal(EmailErrorCodes.TemplateSensitiveInSubject, error.ErrorCode);
            Assert.Contains(EmailTrustedBlocks.SetupSummaryBlock, error.Message);
        },
        subjectVi: "[PEMS] {{setupSummaryBlock}} {{delegationName}}");
    }

    // ── E. Code, canonical source and the shipped default must agree ─────────

    [Fact]
    public void The_contract_the_renderer_enforces_is_the_one_the_canonical_sources_satisfy()
    {
        // The block is required by the contract…
        Assert.Equal(
            EmailTrustedBlocks.SetupSummaryBlock,
            EmailTemplateContracts.RequiredTrustedBlockFor(Code));

        // …it is NOT an operator-editable variable…
        Assert.DoesNotContain(
            EmailTrustedBlocks.SetupSummaryBlock,
            SystemEmailTemplates.Find(Code)!.DeclaredVariables);

        // …and the content this application ships carries it in both languages, so a database synced
        // from either canonical source renders. A drifted row is a data problem, never a code one.
        var shipped = EmailTemplateDefaults.For(Code);
        Assert.NotNull(shipped);
        Assert.Contains("{{setupSummaryBlock}}", shipped!.BodyVi);
        Assert.Contains("{{setupSummaryBlock}}", shipped.BodyEn);
    }

    [Fact]
    public void Only_this_template_carries_a_required_trusted_block()
    {
        // Guards the blast radius of the new render-time refusal: it fires for this template and no
        // other, so a template whose body legitimately never mentions a block cannot start failing.
        Assert.NotNull(EmailTemplateContracts.RequiredTrustedBlockFor(Code));

        foreach (var template in SystemEmailTemplates.All)
        {
            if (template.TemplateCode == Code) continue;
            Assert.Null(EmailTemplateContracts.RequiredTrustedBlockFor(template.TemplateCode));
        }
    }
}
