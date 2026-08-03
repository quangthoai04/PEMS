using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Emails.Common;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The shipped defaults, the canonical SQL seed and the active catalog say the same thing (G11-I).
///
/// <para>
/// <b>Why this suite is the load-bearing part of restore.</b> The default content is authored in ONE
/// place — the canonical SQL seed — and shipped to the backend as a generated embedded resource, because
/// hand-copying thirty templates' worth of wording into C# would create a second author for the same
/// text and the two would drift the first time somebody fixed a typo in one of them. A generated
/// artifact is only safe while something checks it is still a faithful copy. That is this file.
/// </para>
/// <para>
/// The database these tests read is imported fresh from the canonical script by
/// <see cref="DisposableDatabaseManager"/>, and its hash is pinned — so "matches the database" here means
/// "matches the verified canonical seed", not "matches whatever a developer's machine happens to hold".
/// </para>
/// </summary>
public sealed class EmailTemplateDefaultsParityTests
{
    /// <summary>
    /// Every default matches the freshly imported catalog, field for field.
    ///
    /// <para>
    /// This is the assertion that catches a seed edit that was not regenerated into the resource. It is
    /// deliberately compared per field per template rather than as one digest: a digest failure says only
    /// "something changed", and the person who has to fix it then has thirty templates to search.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Every_shipped_default_matches_the_freshly_imported_canonical_catalog()
    {
        EmailEvidenceHarness.RequireDb();

        // A database of this test's own, imported from the canonical script and dropped on the way out.
        //
        // The shared disposable database is NOT a pristine seed: other suites edit template content and
        // put it back, and one that fails part way leaves the edit behind. Reading it made this test
        // report "ACCOUNT_ACTIVATED.Name differs between the shipped default and the canonical seed" in a
        // full run while passing on its own — a drift in the seed that did not exist, pointing the reader
        // at the wrong file. "Freshly imported" is what the name promises, so it is what it now reads.
        using var pristine = DisposableDatabaseManager.CreatePristineDatabase(EmailEvidenceHarness.BaseConnectionString);
        await using var db = EmailEvidenceHarness.ContextFor(pristine.ConnectionString);

        // Read straight from the table, but ONLY the codes the registry owns: a historical row left by
        // another suite is not part of the shipped catalog and must not fail this.
        var rows = await db.EmailTemplates.AsNoTracking()
            .Select(t => new { t.TemplateCode, t.Name, t.Description, t.SubjectVi, t.BodyVi, t.SubjectEn, t.BodyEn })
            .ToListAsync();

        var byCode = rows
            .Where(r => EmailTemplateDefaults.For(r.TemplateCode) is not null)
            .ToDictionary(r => r.TemplateCode, r => r, StringComparer.Ordinal);

        var differences = new List<string>();

        foreach (var code in EmailTemplateDefaults.AllCodes)
        {
            if (!byCode.TryGetValue(code, out var row))
            {
                differences.Add($"{code}: shipped default has no row in the canonical catalog");
                continue;
            }

            var shipped = EmailTemplateDefaults.For(code)!;

            void Compare(string field, string? expected, string? actual)
            {
                if (!string.Equals(expected ?? "", actual ?? "", StringComparison.Ordinal))
                    differences.Add($"{code}.{field} differs between the shipped default and the canonical seed");
            }

            Compare(nameof(shipped.Name), shipped.Name, row.Name);
            Compare(nameof(shipped.Description), shipped.Description, row.Description);
            Compare(nameof(shipped.SubjectVi), shipped.SubjectVi, row.SubjectVi);
            Compare(nameof(shipped.BodyVi), shipped.BodyVi, row.BodyVi);
            Compare(nameof(shipped.SubjectEn), shipped.SubjectEn, row.SubjectEn);
            Compare(nameof(shipped.BodyEn), shipped.BodyEn, row.BodyEn);
        }

        Assert.Empty(differences);
    }

    /// <summary>
    /// The three code sets agree: registry, shipped defaults, and the ACTIVE catalog. Checked in both
    /// directions, so neither an orphan default nor an unbacked template can survive.
    /// </summary>
    [Fact]
    public async Task The_registry_the_defaults_and_the_active_catalog_hold_the_same_codes()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var registry = SystemEmailTemplates.All.Select(t => t.TemplateCode)
            .OrderBy(c => c, StringComparer.Ordinal).ToList();

        var defaults = EmailTemplateDefaults.AllCodes.ToList();

        var active = (await db.EmailTemplates.AsNoTracking()
                .Where(t => t.Status == "ACTIVE")
                .Select(t => t.TemplateCode)
                .ToListAsync())
            .Where(c => registry.Contains(c, StringComparer.Ordinal))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(registry, defaults);
        Assert.Equal(registry, active);
    }

    /// <summary>
    /// The shipped defaults carry no unresolved placeholder beyond the variables their own contract
    /// allows — so a restored template renders rather than shipping a literal <c>{{...}}</c> to a reader.
    /// </summary>
    [Fact]
    public void No_shipped_default_carries_a_variable_its_contract_does_not_allow()
    {
        var offenders = new List<string>();

        foreach (var code in EmailTemplateDefaults.AllCodes)
        {
            var contract = EmailTemplateContracts.For(code);
            var shipped = EmailTemplateDefaults.For(code)!;
            if (contract is null) { offenders.Add(code + ": no contract"); continue; }

            foreach (var (field, text) in new (string, string?)[]
                     {
                         ("subjectVi", shipped.SubjectVi), ("bodyVi", shipped.BodyVi),
                         ("subjectEn", shipped.SubjectEn), ("bodyEn", shipped.BodyEn),
                     })
            {
                foreach (var name in Placeholders(text))
                    if (!contract.AllowedVariables.Contains(name, StringComparer.Ordinal))
                        offenders.Add($"{code}.{field} uses {{{{{name}}}}}, which its contract does not allow");
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// No shipped default carries a real token, link or one-time code. Defaults are wording; anything
    /// that looks like a secret in them would be a secret committed to the repository.
    /// </summary>
    [Fact]
    public void No_shipped_default_contains_a_baked_in_token_or_action_url()
    {
        var offenders = new List<string>();

        foreach (var code in EmailTemplateDefaults.AllCodes)
        {
            var shipped = EmailTemplateDefaults.For(code)!;
            var all = string.Join("\n", shipped.SubjectVi, shipped.BodyVi, shipped.SubjectEn, shipped.BodyEn);

            // The action block is a placeholder, minted per send. A concrete public-action URL in the
            // stored content would mean every recipient of that template got one person's link.
            if (all.Contains("/public/email-actions/", StringComparison.OrdinalIgnoreCase))
                offenders.Add(code + ": contains a concrete public action URL");

            if (System.Text.RegularExpressions.Regex.IsMatch(all, @"\btoken=[A-Za-z0-9_\-]{8,}"))
                offenders.Add(code + ": contains something shaped like a real token");
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The embedded resource is well-formed, complete, and free of duplicates — asserted directly rather
    /// than only through the loader, so a corrupt file fails here with a clear message instead of
    /// surfacing as a puzzling restore failure.
    /// </summary>
    [Fact]
    public void The_embedded_resource_parses_and_holds_one_complete_entry_per_template()
    {
        var raw = EmailTemplateDefaults.ReadRawResource();
        Assert.False(string.IsNullOrWhiteSpace(raw));

        using var parsed = JsonDocument.Parse(raw);
        Assert.Equal(JsonValueKind.Array, parsed.RootElement.ValueKind);
        Assert.Equal(SystemEmailTemplates.All.Count, parsed.RootElement.GetArrayLength());

        var codes = parsed.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("templateCode").GetString()!)
            .ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());

        foreach (var entry in parsed.RootElement.EnumerateArray())
            foreach (var field in new[] { "name", "subjectVi", "bodyVi", "subjectEn", "bodyEn" })
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty(field).GetString()),
                    $"{entry.GetProperty("templateCode").GetString()}.{field} is empty in the embedded defaults.");
    }

    /// <summary>
    /// Vietnamese survived the generation round trip. The defaults were extracted from MySQL and written
    /// as UTF-8; a mojibake bug there would restore templates full of "Xác nháº­n" and every one of the
    /// content comparisons above would still pass, because both sides would be equally wrong.
    /// </summary>
    [Fact]
    public void The_vietnamese_defaults_are_not_mojibake()
    {
        var confirmation = EmailTemplateDefaults.For(SystemEmailTemplates.AccountEmailConfirmation)!;

        Assert.Contains("Xác nhận", confirmation.SubjectVi, StringComparison.Ordinal);
        Assert.DoesNotContain("Ã", confirmation.SubjectVi, StringComparison.Ordinal);
        Assert.DoesNotContain("â€", confirmation.BodyVi, StringComparison.Ordinal);

        foreach (var code in EmailTemplateDefaults.AllCodes)
        {
            var shipped = EmailTemplateDefaults.For(code)!;
            Assert.DoesNotContain("Ã¡", shipped.BodyVi, StringComparison.Ordinal);
            Assert.DoesNotContain("áº", shipped.BodyVi, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> Placeholders(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(text, @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}"))
            yield return m.Groups[1].Value;
    }
}
