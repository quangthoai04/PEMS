using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PEMS.ArchitectureTests;

/// <summary>
/// The request-edit path does not write operational-contact data (repair v3 §2.2).
///
/// <para>
/// Editing a visit request and managing its operational contact are two workflows, with two screens and
/// two endpoints. The edit payload still CARRIES a contact snapshot so an unchanged one round-trips, and
/// <c>EnsureContactSnapshotUnchanged</c> refuses the call if any of the five fields differs — but a
/// guard in front of an assignment is only as good as the next person who moves it. Removing the
/// assignment entirely is what makes the guard the only route, and this test is what keeps it removed.
/// </para>
///
/// <para>
/// A source-text guard, like <see cref="VisitLeadTimeScopeTests"/> and for the same reason: "this
/// method does not assign those columns" is not expressible as a type-level assertion, because a method
/// that writes them has exactly the same dependencies as one that does not.
/// </para>
/// </summary>
public class RequestEditContactBoundaryTests
{
    /// <summary>
    /// Only <c>BuildFormDetail</c> may write them, and only for a campus being ADDED — which has no
    /// contact yet and no invitation bound to anything.
    /// </summary>
    private const string EditOpsFile = "VisitRequestV2EditOps.cs";

    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "backend");
    }

    private static string ReadEditOps()
    {
        var path = Directory.EnumerateFiles(BackendRoot(), EditOpsFile, SearchOption.AllDirectories)
            .FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        Assert.True(path is not null, $"{EditOpsFile} was not found under backend/.");
        return File.ReadAllText(path!);
    }

    [Fact]
    public void Applying_an_edit_to_an_existing_campus_never_writes_the_contact_columns()
    {
        var source = ReadEditOps();

        // The body of ApplyFormDetail: from its signature to the closing brace of the method, taken as
        // "up to the next method-level XML doc comment", which is how this file separates its members.
        var start = source.IndexOf("public static void ApplyFormDetail(", StringComparison.Ordinal);
        Assert.True(start >= 0, "ApplyFormDetail is no longer in " + EditOpsFile + ".");
        var end = source.IndexOf("public static VisitInstanceFormDetail BuildFormDetail(", start, StringComparison.Ordinal);
        Assert.True(end > start, "BuildFormDetail no longer follows ApplyFormDetail; the slice below is wrong.");

        var body = source[start..end];
        var writes = Regex.Matches(body, @"detail\.OperationalContact\w*\s*=")
            .Select(m => m.Value.Trim())
            .ToList();

        Assert.True(writes.Count == 0,
            "ApplyFormDetail runs on a campus that ALREADY EXISTS, whose operational contact belongs to "
            + "the contact-management workflow — its own endpoint, its own concurrency check, its own "
            + "audit entry, and the only path that can ask a new address to accept. A request edit that "
            + "wrote these columns would be a second, silent writer of contact data. Found: "
            + string.Join(", ", writes));
    }
}
