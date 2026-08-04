using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The contract between the code-side catalog (<see cref="SystemEmailTemplates"/>) and the
/// database-side catalog (the <c>email_templates</c> seed), checked in BOTH directions against a
/// database freshly imported from the canonical script.
///
/// <para>
/// One direction alone is not enough, and the history of this seed shows why. Checking only
/// "every caller has a template" leaves orphans: the previous seed shipped 16 ACTIVE templates of which
/// nine had no production caller at all — including one whose text invited a user to sign in before they
/// had confirmed their email. Checking only "every template has a caller" leaves the opposite hole: a
/// caller naming a code nobody seeded, which now fails at send time because the renderer has no
/// fallback. Both directions are asserted here so neither can happen again.
/// </para>
/// </summary>
public sealed class SystemEmailTemplateContractTests
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

    private static async Task<List<EmailTemplate>> LoadAllAsync()
    {
        using var db = NewContext();
        return await db.EmailTemplates.AsNoTracking().OrderBy(t => t.TemplateCode).ToListAsync();
    }

    /// <summary>Action/detail URLs are minted per send; a template must never declare one as a variable.</summary>
    private static readonly string[] ReservedActionUrlNames =
    {
        "acceptUrl", "declineUrl", "assignUrl", "detailUrl", "negotiateUrl",
        "approveProposalUrl", "rejectProposalUrl", "confirmBorrowUrl", "confirmReturnUrl",
    };

    // ── Both directions ──────────────────────────────────────────────────────

    [Fact]
    public async Task Every_registered_template_has_an_active_row_in_the_seed()
    {
        RequireDb();
        var active = (await LoadAllAsync())
            .Where(t => string.Equals(t.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.TemplateCode)
            .ToHashSet(StringComparer.Ordinal);

        var missing = SystemEmailTemplates.AllCodes.Where(c => !active.Contains(c)).OrderBy(c => c).ToList();

        Assert.True(missing.Count == 0,
            "A production caller would fail at send time — the renderer has no fallback. Missing from the seed: "
            + string.Join(", ", missing));
    }

    [Fact]
    public async Task Every_active_seeded_template_is_a_registered_system_template()
    {
        RequireDb();
        var orphans = (await LoadAllAsync())
            .Where(t => string.Equals(t.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.TemplateCode)
            .Where(c => !SystemEmailTemplates.IsSystemTemplate(c))
            .OrderBy(c => c)
            .ToList();

        Assert.True(orphans.Count == 0,
            "ACTIVE templates with no production caller (dead seed): " + string.Join(", ", orphans));
    }

    [Fact]
    public async Task The_seed_contains_exactly_the_agreed_catalog_and_nothing_inactive()
    {
        RequireDb();
        var all = await LoadAllAsync();

        // 30 + VISIT_SETUP_PROGRESS_UPDATE, the Host's manual preparation update to the guest.
        Assert.Equal(31, all.Count);
        Assert.All(all, t => Assert.Equal("ACTIVE", t.Status));
    }

    [Fact]
    public async Task No_template_code_is_seeded_twice()
    {
        RequireDb();
        var dupes = (await LoadAllAsync())
            .GroupBy(t => t.TemplateCode, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(dupes.Count == 0, "Duplicate template_code: " + string.Join(", ", dupes));
    }

    // ── Row-level contract ───────────────────────────────────────────────────

    [Fact]
    public async Task Every_template_uses_a_purpose_from_the_email_catalog()
    {
        RequireDb();
        var bad = (await LoadAllAsync())
            .Where(t => !EmailTemplatePurposes.IsValid(t.Purpose))
            .Select(t => $"{t.TemplateCode}={t.Purpose}")
            .ToList();

        // The template API used to validate this against the OTP purpose enum, which rejected every
        // value the seed actually stores.
        Assert.True(bad.Count == 0, "Purpose outside EmailTemplatePurposes: " + string.Join(", ", bad));
    }

    [Fact]
    public async Task Every_templates_purpose_matches_the_registry()
    {
        RequireDb();
        var mismatched = (await LoadAllAsync())
            .Select(t => (t.TemplateCode, Db: t.Purpose, Registry: SystemEmailTemplates.Find(t.TemplateCode)?.Purpose))
            .Where(x => x.Registry is not null && !string.Equals(x.Db, x.Registry, StringComparison.Ordinal))
            .Select(x => $"{x.TemplateCode}: db={x.Db} registry={x.Registry}")
            .ToList();

        Assert.True(mismatched.Count == 0, string.Join(" | ", mismatched));
    }

    [Fact]
    public async Task No_active_template_is_missing_vietnamese_or_english_content()
    {
        RequireDb();
        var incomplete = (await LoadAllAsync())
            .Where(t => string.IsNullOrWhiteSpace(t.SubjectVi) || string.IsNullOrWhiteSpace(t.BodyVi)
                     || string.IsNullOrWhiteSpace(t.SubjectEn) || string.IsNullOrWhiteSpace(t.BodyEn))
            .Select(t => t.TemplateCode)
            .ToList();

        // The renderer refuses to substitute one language for the other, so a half-translated row is a
        // send-time failure rather than a cosmetic gap.
        Assert.True(incomplete.Count == 0, "Missing VI or EN content: " + string.Join(", ", incomplete));
    }

    [Fact]
    public async Task No_template_is_bound_to_a_campus_without_evidence()
    {
        RequireDb();
        var scoped = (await LoadAllAsync()).Where(t => t.CampusId is not null).Select(t => t.TemplateCode).ToList();
        Assert.True(scoped.Count == 0, "Campus-scoped templates need a documented reason: " + string.Join(", ", scoped));
    }

    // ── Placeholder contract ─────────────────────────────────────────────────

    [Fact]
    public async Task Declared_variables_match_the_placeholders_actually_written_in_all_four_fields()
    {
        RequireDb();
        var problems = new List<string>();

        foreach (var t in await LoadAllAsync())
        {
            var used = EmailTemplateVariables
                .ExtractPlaceholders(t.SubjectVi, t.BodyVi, t.SubjectEn, t.BodyEn)
                .Where(n => !EmailTrustedBlocks.All.Contains(n, StringComparer.Ordinal))
                .ToHashSet(StringComparer.Ordinal);

            var declared = EmailTemplateVariables.ParseDeclared(t.VariablesText);

            var undeclared = used.Except(declared).OrderBy(x => x, StringComparer.Ordinal).ToList();

            // The sender variables are exempt from "declared but never used", and ONLY from that half.
            //
            // Every template whose capability permits them declares all six, because variables_text is
            // what the editor offers an operator as "names you may write here". A template that declares
            // only the three its shipped wording happens to print would silently refuse the other three
            // the moment somebody added one — and adding one is the point of the feature. So the shipped
            // bodies use senderName/senderRole/senderEmail, and senderPhone/senderDepartment/senderCampus
            // sit declared and unused until an operator wants them. That asymmetry is deliberate.
            //
            // The other direction stays enforced for them exactly as for any other variable: a body that
            // writes {{senderName}} without declaring it is still a fault, and still fails here.
            var unused = declared
                .Except(used)
                .Where(v => !PEMS.Application.Emails.Sender.EmailSenderVariableNames.IsSenderVariable(v))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (undeclared.Count > 0) problems.Add($"{t.TemplateCode}: used but not declared -> {string.Join(",", undeclared)}");
            if (unused.Count > 0) problems.Add($"{t.TemplateCode}: declared but never used -> {string.Join(",", unused)}");
        }

        Assert.True(problems.Count == 0, string.Join(" | ", problems));
    }

    [Fact]
    public async Task Declared_variables_match_the_registry()
    {
        RequireDb();
        var problems = new List<string>();

        foreach (var t in await LoadAllAsync())
        {
            var registered = SystemEmailTemplates.Find(t.TemplateCode);
            if (registered is null) continue;

            var declared = EmailTemplateVariables.ParseDeclared(t.VariablesText);
            var expected = registered.DeclaredVariables.ToHashSet(StringComparer.Ordinal);

            if (!declared.SetEquals(expected))
                problems.Add($"{t.TemplateCode}: db=[{string.Join(",", declared.OrderBy(x => x))}] " +
                             $"registry=[{string.Join(",", expected.OrderBy(x => x))}]");
        }

        Assert.True(problems.Count == 0, string.Join(" | ", problems));
    }

    [Fact]
    public async Task Every_placeholder_is_lower_camel_case()
    {
        RequireDb();
        var offenders = new List<string>();

        foreach (var t in await LoadAllAsync())
        {
            foreach (var name in EmailTemplateVariables.ExtractPlaceholders(t.SubjectVi, t.BodyVi, t.SubjectEn, t.BodyEn))
            {
                if (!EmailTemplateVariables.IsValidName(name))
                    offenders.Add($"{t.TemplateCode}.{{{{{name}}}}}");
            }
        }

        // The legacy seed mixed {{FullName}} and {{recipientName}} in one catalog; the renderer treats an
        // unknown spelling as unresolved, so this has to be uniform.
        Assert.True(offenders.Count == 0, "Non lower-camelCase placeholders: " + string.Join(", ", offenders));
    }

    [Fact]
    public async Task No_template_declares_an_action_url_as_an_editable_variable()
    {
        RequireDb();
        var offenders = new List<string>();

        foreach (var t in await LoadAllAsync())
        {
            foreach (var name in EmailTemplateVariables.ParseDeclared(t.VariablesText))
            {
                if (ReservedActionUrlNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    offenders.Add($"{t.TemplateCode}.{name}");
            }
        }

        // Declaring one would let a template author move, delete or fake the button that carries a
        // one-time token.
        Assert.True(offenders.Count == 0, "Action URLs declared as variables: " + string.Join(", ", offenders));
    }

    [Fact]
    public async Task No_template_body_contains_a_raw_action_or_token_url()
    {
        RequireDb();
        var offenders = new List<string>();

        foreach (var t in await LoadAllAsync())
        {
            var content = string.Concat(t.BodyVi, t.BodyEn);
            if (content.Contains("/public/email-actions/", StringComparison.OrdinalIgnoreCase)
                || content.Contains("visit-contact-claim/", StringComparison.OrdinalIgnoreCase)
                || content.Contains("visit-contact-transfer/", StringComparison.OrdinalIgnoreCase)
                || content.Contains("confirm-email?token=", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(t.TemplateCode);
            }
        }

        Assert.True(offenders.Count == 0, "Templates embedding a token URL: " + string.Join(", ", offenders));
    }

    // ── Render smoke test ────────────────────────────────────────────────────

    [Theory]
    [InlineData(EmailLanguages.Vi)]
    [InlineData(EmailLanguages.En)]
    public async Task Every_template_renders_in_both_languages_with_nothing_left_unresolved(string language)
    {
        RequireDb();
        using var db = NewContext();
        var renderer = new EmailTemplateRenderer(db);

        var failures = new List<string>();

        foreach (var template in await LoadAllAsync())
        {
            var registered = SystemEmailTemplates.Find(template.TemplateCode);
            if (registered is null) continue;

            var variables = registered.DeclaredVariables
                .ToDictionary(v => v, v => $"[{v}]", StringComparer.Ordinal);

            // Supply every trusted block the body references; a body that needs one and does not get it
            // is a caller bug, covered separately by the renderer tests.
            var trusted = EmailTrustedBlocks.All
                .ToDictionary(b => b, _ => "<div>action</div>", StringComparer.Ordinal);

            try
            {
                var result = await renderer.RenderAsync(
                    new EmailRenderRequest(template.TemplateCode, language, variables, trusted));

                if (string.IsNullOrWhiteSpace(result.Subject)) failures.Add($"{template.TemplateCode}: empty subject");
                if (string.IsNullOrWhiteSpace(result.Body)) failures.Add($"{template.TemplateCode}: empty body");
                if (result.Subject.Contains("{{")) failures.Add($"{template.TemplateCode}: subject still has a placeholder");
                if (result.Body.Contains("{{")) failures.Add($"{template.TemplateCode}: body still has a placeholder");
            }
            catch (Exception ex)
            {
                failures.Add($"{template.TemplateCode}: {ex.GetType().Name} {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(" | ", failures));
    }

    // ── History preservation ─────────────────────────────────────────────────

    [Fact]
    public async Task No_seeded_sent_email_points_at_a_template_that_no_longer_exists()
    {
        RequireDb();
        using var db = NewContext();

        var orphans = await db.SentEmails.AsNoTracking()
            .Where(e => e.EmailTemplateId != null
                        && !db.EmailTemplates.Any(t => t.EmailTemplateId == e.EmailTemplateId))
            .Select(e => e.SentEmailId)
            .ToListAsync();

        Assert.True(orphans.Count == 0, "Dangling email_template_id on: " + string.Join(", ", orphans));
    }

    /// <summary>
    /// Deleting a template must UNLINK its delivery history, never destroy it.
    ///
    /// <para>
    /// The stake is an audit one: <c>sent_emails</c> is the only record that a message went out, to
    /// whom, and what it said. A catalog change — retiring a template, a reseed, an operator deleting
    /// something they authored — must not be able to erase that. The FK is declared
    /// <c>ON DELETE SET NULL</c> for exactly this reason, and the risk is a future migration quietly
    /// re-declaring it as CASCADE.
    /// </para>
    /// <para>
    /// The test builds its own template, message and recipient and then performs the real deletion,
    /// rather than asserting the FK's metadata. Metadata says what the schema claims; this says what
    /// the database actually does to a row that exists. It also replaces an earlier version that read
    /// eight seeded ids (99101-99108) which no longer exist: the canonical script inserted them and
    /// then, thousands of lines later, ran an unconditional <c>DELETE FROM sent_emails</c> during the
    /// catalog rebuild, so that assertion could never pass. Those dead seed blocks are gone.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Deleting_a_template_unlinks_its_history_and_destroys_none_of_it()
    {
        RequireDb();

        var stamp = DateTime.UtcNow.Ticks;
        var code = $"IT_UNLINK_{stamp}";
        const string subject = "[IT] Bản ghi lịch sử phải sống sót";
        const string body = "<p>Nội dung đã gửi, giữ nguyên sau khi template bị xoá.</p>";
        const string recipientEmail = "it-unlink@fpt.edu.vn";

        ulong templateId;
        ulong sentEmailId;
        ulong recipientId;

        using (var db = NewContext())
        {
            var template = new EmailTemplate
            {
                TemplateCode = code,
                Name = "IT unlink fixture",
                Purpose = EmailTemplatePurposes.Account,
                Description = "Created by an integration test; deleted within it.",
                Status = "ACTIVE",
                SubjectVi = "Chủ đề", BodyVi = "<p>Thân</p>",
                SubjectEn = "Subject", BodyEn = "<p>Body</p>",
                CreatedAt = DateTime.Now,
            };
            db.EmailTemplates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.EmailTemplateId;

            var sent = new SentEmail
            {
                EmailTemplateId = templateId,
                RelatedType = "VISIT_PARTICIPANT",
                RelatedId = 4242,
                Subject = subject,
                BodySnapshot = body,
                Status = "SENT",
                SentAt = DateTime.Now,
                CreatedAt = DateTime.Now,
                Recipients = new List<SentEmailRecipient>
                {
                    new()
                    {
                        RecipientEmail = recipientEmail,
                        RecipientName = "Người nhận IT",
                        RecipientType = EmailRecipientTypes.To,
                        DeliveryStatus = "DELIVERED",
                        CreatedAt = DateTime.Now,
                    },
                },
            };
            db.SentEmails.Add(sent);
            await db.SaveChangesAsync();

            sentEmailId = sent.SentEmailId;
            recipientId = sent.Recipients.Single().SentEmailRecipientId;
        }

        try
        {
            using (var db = NewContext())
            {
                var template = await db.EmailTemplates.SingleAsync(t => t.EmailTemplateId == templateId);
                db.EmailTemplates.Remove(template);
                await db.SaveChangesAsync();
            }

            using (var verify = NewContext())
            {
                Assert.False(await verify.EmailTemplates.AsNoTracking()
                    .AnyAsync(t => t.EmailTemplateId == templateId), "the template was not actually deleted.");

                var stored = await verify.SentEmails.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.SentEmailId == sentEmailId);

                Assert.NotNull(stored);
                Assert.Null(stored!.EmailTemplateId);           // unlinked…
                Assert.Equal(subject, stored.Subject);          // …and everything else untouched
                Assert.Equal(body, stored.BodySnapshot);
                Assert.Equal("VISIT_PARTICIPANT", stored.RelatedType);
                Assert.Equal(4242ul, stored.RelatedId);
                Assert.Equal("SENT", stored.Status);
                Assert.NotNull(stored.SentAt);

                // The recipient hangs off the message, not the template, and must be equally untouched:
                // a history row with no addressee cannot answer "who received this".
                var recipient = await verify.SentEmailRecipients.AsNoTracking()
                    .SingleOrDefaultAsync(r => r.SentEmailRecipientId == recipientId);

                Assert.NotNull(recipient);
                Assert.Equal(sentEmailId, recipient!.SentEmailId);
                Assert.Equal(recipientEmail, recipient.RecipientEmail);
                Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);
                Assert.Equal("DELIVERED", recipient.DeliveryStatus);
            }
        }
        finally
        {
            using var cleanup = NewContext();
            await cleanup.Database.ExecuteSqlRawAsync(
                "DELETE FROM sent_email_recipients WHERE sent_email_id = {0}", sentEmailId);
            await cleanup.Database.ExecuteSqlRawAsync(
                "DELETE FROM sent_emails WHERE sent_email_id = {0}", sentEmailId);
            await cleanup.Database.ExecuteSqlRawAsync(
                "DELETE FROM email_templates WHERE template_code = {0}", code);
        }
    }
}
