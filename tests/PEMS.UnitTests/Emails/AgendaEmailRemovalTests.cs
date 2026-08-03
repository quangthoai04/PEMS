using System;
using System.IO;
using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The merge decision that the setup-progress update is the ONLY way this product mails a schedule out,
/// asserted where somebody would otherwise re-add the other one by accident.
///
/// <para>
/// Two flows were built independently for the same screen: <c>VISIT_AGENDA_PROPOSAL</c> ("Gửi lịch
/// trình" — fire-and-forget to the campus's operational contact, no preview, no PDF) and
/// <c>VISIT_SETUP_PROGRESS_UPDATE</c> (the Host composes, previews and sends a draft with the Schedule
/// Report attached). Keeping both would give the guest side two different "here is your schedule"
/// mails from two different pipelines with two different recipient policies, and the Host no way to
/// tell which button did what. The proposal flow was dropped whole.
/// </para>
/// <para>
/// Deleting code proves nothing on its own — the check that matters is that the catalog cannot quietly
/// grow the template back, because a template is what every one of those pieces hangs off.
/// </para>
/// </summary>
public sealed class AgendaEmailRemovalTests
{
    private const string RemovedCode = "VISIT_AGENDA_PROPOSAL";
    private const string RemovedBlock = "agendaBlock";

    [Fact]
    public void The_agenda_proposal_template_is_not_registered()
    {
        Assert.Null(SystemEmailTemplates.Find(RemovedCode));
        Assert.DoesNotContain(RemovedCode, SystemEmailTemplates.AllCodes);
    }

    [Fact]
    public void No_shipped_default_content_carries_the_agenda_proposal()
    {
        Assert.Null(EmailTemplateDefaults.For(RemovedCode));
    }

    /// <summary>
    /// A trusted block is the only route by which raw markup enters a rendered body. Leaving a retired
    /// one in <c>All</c> would keep it excluded from the variable checks — so a template that used
    /// <c>{{agendaBlock}}</c> would validate cleanly and then render the placeholder to a recipient,
    /// because nothing would be supplying it any more.
    /// </summary>
    [Fact]
    public void The_agenda_trusted_block_is_gone_and_the_setup_summary_block_is_the_remaining_one()
    {
        Assert.DoesNotContain(RemovedBlock, EmailTrustedBlocks.All);
        Assert.Equal(
            new[] { EmailTrustedBlocks.ActionBlock, EmailTrustedBlocks.SetupSummaryBlock }.OrderBy(b => b, StringComparer.Ordinal),
            EmailTrustedBlocks.All.OrderBy(b => b, StringComparer.Ordinal));
    }

    /// <summary>
    /// The one surviving schedule-bearing mail. Named here so that deleting it — or moving it back under
    /// the invitation family — fails next to the removal it replaced.
    /// </summary>
    [Fact]
    public void The_setup_progress_update_is_the_only_remaining_schedule_email()
    {
        Assert.NotNull(SystemEmailTemplates.Find(SystemEmailTemplates.VisitSetupProgressUpdate));
        Assert.Equal("VISIT_SETUP_PROGRESS_UPDATE", SystemEmailTemplates.VisitSetupProgressUpdate);
    }

    // ── The Host has to be reachable from the mail that tells the guest to reply ──

    /// <summary>
    /// The body says "phản hồi email này" / "reply to this email" so the Host can act on it. The
    /// draft/manual send path carries the SYSTEM's configured Reply-To and takes no per-message
    /// override, so that sentence only leads somewhere if the Host's address is printed in the text.
    /// </summary>
    [Fact]
    public void The_setup_progress_body_names_the_host_and_prints_an_address_to_reach_them()
    {
        var shipped = EmailTemplateDefaults.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;

        foreach (var body in new[] { shipped.BodyVi, shipped.BodyEn })
        {
            Assert.Contains("{{hostName}}", body);
            Assert.Contains("{{hostEmail}}", body);
        }

        // Declared, so the renderer refuses to send with it unresolved rather than shipping the braces.
        Assert.Contains("hostEmail",
            SystemEmailTemplates.Find(SystemEmailTemplates.VisitSetupProgressUpdate)!.DeclaredVariables);
    }

    /// <summary>
    /// An address is not a secret. Classifying it as one would strip the stored body from the history,
    /// which is the record the Host relies on to prove what the guest was told.
    /// </summary>
    [Fact]
    public void The_host_address_is_classified_as_ordinary_content()
    {
        Assert.Contains("hostEmail", SensitiveEmailVariables.KnownNonSensitive);
        Assert.DoesNotContain("hostEmail", SensitiveEmailVariables.Names);
        Assert.False(SensitiveEmailVariables.ForbiddenInSubject("hostEmail"));
    }

    // ── Nothing in the shipped source still reaches for the removed flow ──────

    /// <summary>
    /// The registry check above cannot see a stale controller route, API client or button — those refer
    /// to the flow by URL and by label, not by template code. This walks the actual source of the four
    /// projects that made up the removed flow and fails on any surviving mention.
    /// </summary>
    [Fact]
    public void No_source_file_still_references_the_removed_agenda_email_flow()
    {
        var root = RepositoryRoot();
        var needles = new[]
        {
            RemovedCode, "VisitAgendaProposal", "AgendaBlock", "agendaBlock",
            "SendVisitAgendaEmail", "sendAgendaEmail", "AgendaListBlock", "agenda/send-email",
        };

        var searched = new[]
        {
            Path.Combine(root, "backend"),
            Path.Combine(root, "frontend", "pems-react", "src"),
        };

        var hits = searched
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            .Where(IsShippedSource)
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(f => needles.Any(n => f.text.Contains(n, StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(root, f.path))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(hits.Count == 0,
            "The agenda-proposal email flow was removed, but these files still mention it: "
            + string.Join(", ", hits));
    }

    private static bool IsShippedSource(string path)
    {
        // bin/obj hold copies of the very assets being checked, and node_modules is not ours.
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => p is "bin" or "obj" or "node_modules" or "dist" or ".vs"))
            return false;

        // Code only. The one JSON that could carry the template — email-template-defaults.json — is
        // already asserted through EmailTemplateDefaults above, and walking the i18n locale bundles as
        // well turned this single test into the slowest in the suite for no extra coverage.
        return Path.GetExtension(path) is ".cs" or ".ts" or ".tsx";
    }

    /// <summary>Walks up to the directory holding the solution, so the test does not depend on cwd.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx")))
            dir = dir.Parent;

        Assert.True(dir is not null, "Could not locate the repository root (PEMS.slnx) from the test output directory.");
        return dir!.FullName;
    }
}
