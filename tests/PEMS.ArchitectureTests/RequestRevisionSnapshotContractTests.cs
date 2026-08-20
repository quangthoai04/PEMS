using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PEMS.Domain.Entities.Delegations;
using Xunit;

namespace PEMS.ArchitectureTests;

/// <summary>
/// Patch 7 (P7.4) — architecture guard for the request-level revision snapshot. Patch 2 (HR-3) fixed
/// exactly the drift <see cref="VisitFormRevisionSnapshotBuilder"/>'s own doc comment describes: each
/// service used to hand-roll its own anonymous object for <c>SnapshotJson</c>, and the objects had
/// drifted apart — the create path once omitted a field entirely, so the very next edit's diff
/// reported a fabricated change for a value that had been filled in from the start. "A history that
/// invents a change is worse than one that misses it, because people act on it."
///
/// <para>
/// Two guards, deliberately different in kind:
/// </para>
/// <list type="bullet">
/// <item><see cref="Request_snapshot_carries_every_canonical_registrant_field_with_the_right_value"/>
/// and its sibling are BEHAVIOR tests — they reflectively INVOKE the real
/// <c>VisitFormRevisionSnapshotBuilder.Request</c> against a real <c>VisitRequest</c> carrying
/// distinct sentinel values and inspect the actual JSON produced. This catches a field silently
/// dropped, renamed, or left unassigned inside the one true builder — the class of bug source-text
/// scanning cannot distinguish from "present but wrong."</item>
/// <item><see cref="Every_SnapshotJson_assignment_in_the_backend_goes_through_the_canonical_builder"/>
/// is a source-text scan, the same technique <see cref="RequestEditContactBoundaryTests"/> already
/// uses and defends ("this method does not assign those columns is not expressible as a type-level
/// assertion") — because "no OTHER writer exists" is a structural, whole-codebase claim a behavior
/// test against the one known-good writer cannot make. Scoped to the whole backend, not one file, so
/// a brand new hand-rolled writer added anywhere is caught, not just a regression in a file this
/// suite already knew to watch.</item>
/// </list>
/// </summary>
public class RequestRevisionSnapshotContractTests
{
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("PEMS.Infrastructure");

    private static readonly string[] CanonicalKeys =
    {
        "registrantFullName", "registrantOrganization", "registrantJobTitle",
        "registrantNationality", "registrantPhone", "registrantEmail",
    };

    private static MethodInfo RequestBuilderMethod()
    {
        var type = InfrastructureAssembly.GetType("PEMS.Infrastructure.Services.VisitFormRevisionSnapshotBuilder");
        Assert.True(type is not null, "VisitFormRevisionSnapshotBuilder no longer exists in PEMS.Infrastructure.Services.");
        var method = type!.GetMethod("Request", BindingFlags.Public | BindingFlags.Static);
        Assert.True(method is not null, "VisitFormRevisionSnapshotBuilder.Request(VisitRequest) no longer exists — signature changed?");
        return method!;
    }

    private static JsonDocument InvokeRequestBuilder(VisitRequest request)
    {
        var json = (string)RequestBuilderMethod().Invoke(null, new object?[] { request })!;
        return JsonDocument.Parse(json);
    }

    [Fact]
    public void Request_snapshot_carries_every_canonical_registrant_field_with_the_right_value()
    {
        var request = new VisitRequest
        {
            RegistrantFullName = "SENTINEL_FULLNAME",
            RegistrantOrganization = "SENTINEL_ORGANIZATION",
            RegistrantJobTitle = "SENTINEL_JOBTITLE",
            RegistrantNationality = "SENTINEL_NATIONALITY",
            RegistrantPhone = "SENTINEL_PHONE",
            RegistrantEmail = "sentinel@example.com",
        };

        using var doc = InvokeRequestBuilder(request);
        var root = doc.RootElement;

        foreach (var key in CanonicalKeys)
            Assert.True(root.TryGetProperty(key, out _), $"Snapshot is missing canonical key '{key}'.");

        Assert.Equal("SENTINEL_FULLNAME", root.GetProperty("registrantFullName").GetString());
        Assert.Equal("SENTINEL_ORGANIZATION", root.GetProperty("registrantOrganization").GetString());
        Assert.Equal("SENTINEL_JOBTITLE", root.GetProperty("registrantJobTitle").GetString());
        Assert.Equal("SENTINEL_NATIONALITY", root.GetProperty("registrantNationality").GetString());
        Assert.Equal("SENTINEL_PHONE", root.GetProperty("registrantPhone").GetString());
        Assert.Equal("sentinel@example.com", root.GetProperty("registrantEmail").GetString());
    }

    /// <summary>
    /// A field silently dropped changes the key COUNT without necessarily changing which of the six
    /// names is missing in a way the presence-check above happens to catch — pin the exact set too, so
    /// an accidental rename ("registrantJobTitle" → "registrantTitle") fails here even if it still
    /// looks plausible field-by-field.
    /// </summary>
    [Fact]
    public void Request_snapshot_has_exactly_the_six_canonical_keys_no_more_no_less()
    {
        var request = new VisitRequest
        {
            RegistrantFullName = "A", RegistrantOrganization = "B", RegistrantJobTitle = "C",
            RegistrantNationality = "D", RegistrantPhone = "E", RegistrantEmail = "f@g.com",
        };

        using var doc = InvokeRequestBuilder(request);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.Equal(CanonicalKeys.OrderBy(k => k, StringComparer.Ordinal).ToList(), keys);
    }

    // ── Structural backstop: no OTHER writer exists ─────────────────────────────────────────────────

    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "backend");
    }

    /// <summary>
    /// Every construction of a <c>VisitInstanceFormRevisionHistory</c> or
    /// <c>VisitRequestRevisionHistory</c> row anywhere in the backend must set its
    /// <c>SnapshotJson</c> via <c>VisitFormRevisionSnapshotBuilder.Instance(...)</c> /
    /// <c>.Request(...)</c> — never a hand-rolled object or a raw string.
    ///
    /// <para>
    /// Deliberately positive-match on the TWO revision-history entity constructors, not a blanket scan
    /// for the property name <c>SnapshotJson</c> — that property name is not unique to this feature
    /// (<c>VisitRequestIdentityChange</c>, the operational-contact transfer/replace proposal record,
    /// carries its own unrelated <c>SnapshotJson</c> column that has nothing to do with revision
    /// history and is correctly never built by this class).
    /// </para>
    /// </summary>
    [Fact]
    public void Every_revision_history_row_sets_SnapshotJson_through_the_canonical_builder()
    {
        var csFiles = Directory.EnumerateFiles(BackendRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.True(csFiles.Count > 0, "No .cs files found under backend/ — path resolution is broken.");

        var constructorPattern = new Regex(
            @"new\s+(VisitInstanceFormRevisionHistory|VisitRequestRevisionHistory)\b", RegexOptions.Singleline);
        var offenders = new System.Collections.Generic.List<string>();
        var sitesFound = 0;

        foreach (var file in csFiles)
        {
            var text = File.ReadAllText(file);
            foreach (Match m in constructorPattern.Matches(text))
            {
                sitesFound++;
                // The object initializer for these two entities runs well under 900 chars in every
                // existing call site (~10-15 fields) — a bounded window rather than real brace-matching,
                // same trade-off RequestEditContactBoundaryTests already accepts for this style of check.
                var windowEnd = System.Math.Min(text.Length, m.Index + 900);
                var window = text[m.Index..windowEnd];
                var direct = window.Contains("SnapshotJson = VisitFormRevisionSnapshotBuilder.", StringComparison.Ordinal);
                // The ONE verified indirect exception: VisitRevisionBaselineGuard.EnsureRequestBaselineAsync
                // takes its snapshot as a pre-captured STRING parameter (`preEditSnapshotJson`), by its own
                // doc comment's design — the caller can only know a baseline is wanted AFTER applying the
                // edit, by which point the request row no longer holds the "before" values, so the string
                // must be captured earlier and handed in. Every one of its 3 call sites is verified (by this
                // very check, at the `requestBaselineJson =` declaration) to source that string from
                // VisitRevisionBaselineGuard.CaptureRequestSnapshot, which is a 1-line passthrough to
                // VisitFormRevisionSnapshotBuilder.Request — see the assertion just below this loop.
                var isVerifiedIndirectPassthrough =
                    Path.GetFileName(file) == "VisitRevisionBaselineGuard.cs"
                    && window.Contains("SnapshotJson = preEditSnapshotJson,", StringComparison.Ordinal);
                if (!direct && !isVerifiedIndirectPassthrough)
                    offenders.Add($"{Path.GetFileName(file)}:{LineOf(text, m.Index)} — {m.Groups[1].Value}");
            }
        }

        // The exception above is only sound as long as EVERY caller of EnsureRequestBaselineAsync
        // actually builds its string argument via CaptureRequestSnapshot — check that directly too,
        // rather than trusting the one-time review that justified the exception to stay true forever.
        foreach (var file in csFiles)
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"requestBaselineJson\s*=\s*([^;]+);"))
            {
                var rhs = m.Groups[1].Value.Trim();
                if (!rhs.StartsWith("VisitRevisionBaselineGuard.CaptureRequestSnapshot(", StringComparison.Ordinal))
                    offenders.Add($"{Path.GetFileName(file)}:{LineOf(text, m.Index)} — requestBaselineJson = {rhs}");
            }
        }

        Assert.True(sitesFound > 0, "No VisitInstanceFormRevisionHistory/VisitRequestRevisionHistory construction "
            + "sites found at all — the search pattern or path resolution is broken, not that the codebase stopped writing history.");
        Assert.True(offenders.Count == 0,
            "Found a revision-history row constructed WITHOUT SnapshotJson going through "
            + "VisitFormRevisionSnapshotBuilder — this is exactly the hand-rolled-object drift that "
            + "fabricated history entries before Patch 2. Offenders: " + string.Join(" | ", offenders));
    }

    private static int LineOf(string text, int index)
        => text[..index].Count(c => c == '\n') + 1;
}
