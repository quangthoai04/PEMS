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

        // The registry is pinned so a new block has to be added here deliberately. Trusted blocks are the
        // ONLY route by which un-encoded markup enters a rendered body, so the list of them is worth
        // being unable to grow by accident.
        Assert.Equal(
            new[]
            {
                EmailTrustedBlocks.ActionBlock,
                EmailTrustedBlocks.ContactInformationBlock,
                EmailTrustedBlocks.SetupSummaryBlock,
            }.OrderBy(b => b, StringComparer.Ordinal),
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
    /// The body says "phản hồi email này" / "reply to this email" so the Host can act on it, and that
    /// sentence only leads somewhere if the guest is given an address.
    ///
    /// <para>
    /// The address used to be a <c>{{hostEmail}}</c> variable printed mid-sentence. It now arrives in
    /// <c>{{contactInformationBlock}}</c>, which is a better answer to the same requirement: the block
    /// resolves the Host from the visit INSTANCE rather than from whatever the caller passed — so a
    /// multi-campus request cannot show a guest another campus's Host — and it carries the role and
    /// telephone number, which a bare address never did. The variable is gone rather than kept alongside
    /// it, because printing the same mailbox twice, once without its context, is not an improvement.
    /// </para>
    /// </summary>
    [Fact]
    public void The_setup_progress_body_names_the_host_and_prints_an_address_to_reach_them()
    {
        var shipped = EmailTemplateDefaults.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;

        foreach (var body in new[] { shipped.BodyVi, shipped.BodyEn })
        {
            Assert.Contains("{{hostName}}", body);
            Assert.Contains("{{contactInformationBlock}}", body);
            Assert.DoesNotContain("{{hostEmail}}", body);
        }

        var declared = SystemEmailTemplates.Find(SystemEmailTemplates.VisitSetupProgressUpdate)!.DeclaredVariables;
        Assert.DoesNotContain("hostEmail", declared);

        // REQUIRED, so a body edited to drop the block is refused rather than sending the instruction
        // with nothing behind it.
        Assert.True(PEMS.Application.Emails.Contact.EmailContactPolicyDefaults
            .RequiresContactBlock(SystemEmailTemplates.VisitSetupProgressUpdate));
    }

    /// <summary>
    /// An address is not a secret. Classifying it as one would strip the stored body from the history,
    /// which is the record the Host relies on to prove what the guest was told.
    ///
    /// <para>
    /// This used to assert the classification of a <c>hostEmail</c> VARIABLE. There is no such variable
    /// any more — the address travels inside <c>{{contactInformationBlock}}</c> — so the assertion moved
    /// to the property that actually protects the record: the template carries no secret, so its history
    /// policy is to keep the body in full.
    /// </para>
    /// </summary>
    [Fact]
    public void The_host_address_is_classified_as_ordinary_content()
    {
        var template = SystemEmailTemplates.Find(SystemEmailTemplates.VisitSetupProgressUpdate)!;

        Assert.False(SensitiveEmailVariables.CarriesSecret(template));
        Assert.Empty(SensitiveEmailVariables.DeclaredBy(template));
        Assert.Equal(
            HistoryBodyPolicy.Full,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitSetupProgressUpdate));

        // And the block itself may never be put in a subject, which IS stored and shown in list screens.
        Assert.True(SensitiveEmailVariables.ForbiddenInSubject(
            EmailTrustedBlocks.ContactInformationBlock));
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
