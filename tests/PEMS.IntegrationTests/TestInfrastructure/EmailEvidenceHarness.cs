using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.IntegrationTests.Api;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using Xunit;
using Xunit.Sdk;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// The rig every email-evidence suite needs: a disposable database, the REAL renderer and dispatcher, a
/// real <see cref="EmailService"/> writing <c>.eml</c> files to a pickup directory, and a cleanup that
/// removes only the rows the suite created.
///
/// <para>
/// Nothing here is a mock. The point of these suites is to answer "what would actually be sent, and what
/// would actually be stored" — questions a fake cannot answer, because the interesting failures live in
/// the renderer, in MIME, and in what reaches <c>sent_emails</c>.
/// </para>
/// </summary>
public sealed class EmailEvidenceHarness : IDisposable
{
    private static bool? _dbUp;
    private static string? _dbFailure;

    /// <summary>Marker address: every row this suite writes carries it, and cleanup keys on it.</summary>
    public string Marker { get; }

    public string PickupDirectory { get; }

    public EmailEvidenceHarness(string marker)
    {
        Marker = marker;
        PickupDirectory = Path.Combine(Path.GetTempPath(), "pems-evidence-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(PickupDirectory)) Directory.Delete(PickupDirectory, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    private static string ConnString =>
        DisposableDatabaseManager.GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    public static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    public static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch (Exception ex) { _dbUp = false; _dbFailure = ex.ToString(); }
        }

        Assert.True(_dbUp, "Disposable MySQL database is not reachable. " + _dbFailure);
    }

    /// <summary>
    /// The real sender. With no <paramref name="brokenHost"/> it serialises to the pickup directory;
    /// with one, it is pointed at an unreachable server in a Production environment so the outcome is a
    /// genuine provider failure rather than a simulated one.
    /// </summary>
    public EmailService Sender(string? brokenHost = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Smtp:Enabled"] = "true",
            ["Smtp:FromEmail"] = "no-reply@pems.test",
            ["Smtp:FromName"] = "PEMS",
        };

        if (brokenHost is null) values["Smtp:PickupDirectory"] = PickupDirectory;
        else { values["Smtp:Host"] = brokenHost; values["Smtp:Port"] = "2525"; }

        return new EmailService(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            NullLogger<EmailService>.Instance,
            new FakeHostEnvironment(brokenHost is null ? "Development" : "Production"),
            Options.Create(new EmailRecipientOptions()));
    }

    /// <summary>
    /// A dispatcher built the way the container builds one — including the reply-contact resolver.
    ///
    /// <para>
    /// The resolver is an OPTIONAL constructor argument, and leaving it out is silent: the dispatcher
    /// simply contributes no <c>{{contactInformationBlock}}</c>, and the renderer then refuses the
    /// message with "còn placeholder chưa thay thế". Every end-to-end test of the fourteen templates
    /// whose policy is REQUIRED failed that way — sixty-five of them — describing a defect that exists
    /// only in the harness, because <c>DependencyInjection</c> registers the resolver and production
    /// therefore always has one.
    /// </para>
    ///
    /// <para>
    /// It is the REAL resolver and the REAL policy store over the test database, not a stub. A stub
    /// would make these tests agree with themselves rather than with the cascade an operator configures,
    /// and the fail-closed behaviour for a REQUIRED template with no reachable contact is exactly the
    /// thing worth proving end to end.
    /// </para>
    /// </summary>
    public SystemEmailDispatcher Dispatcher(ApplicationDbContext db, string? brokenHost = null)
        => new(db, new EmailTemplateRenderer(db), Sender(brokenHost),
               recipientOptions: null, contacts: Contacts(db));

    /// <summary>The contact resolver, over the same context, with the support contact tests rely on.</summary>
    public static PEMS.Application.Emails.Contact.IEmailContactResolver Contacts(ApplicationDbContext db)
        => new PEMS.Application.Emails.Contact.EmailContactResolver(
            db,
            new PEMS.Application.Emails.Contact.EmailContactPolicyStore(db),
            Options.Create(new PEMS.Application.Emails.Contact.EmailSupportContactOptions
            {
                // A last-resort address for the templates whose policy is SUPPORT_CONTACT. Present so a
                // REQUIRED template can resolve at all; the tests that care about the fail-closed path
                // set their own options rather than relying on this one being absent.
                Name = "PEMS Support",
                Email = "support@pems.test",
                Phone = "1900 0000",
            }));

    public string[] Messages()
        => Directory.Exists(PickupDirectory) ? Directory.GetFiles(PickupDirectory, "*.eml") : Array.Empty<string>();

    /// <summary>How long to keep watching for messages that have not appeared yet.</summary>
    private static readonly TimeSpan MessageTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long to keep watching AFTER the expected count is reached, so a duplicate that lands a moment
    /// late is still caught.
    /// </summary>
    private static readonly TimeSpan MessageSettleWindow = TimeSpan.FromMilliseconds(400);

    private static readonly TimeSpan MessagePollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Waits until exactly <paramref name="expected"/> delivered messages match
    /// <paramref name="matches"/>, then keeps watching briefly to be sure no more arrive.
    ///
    /// <para>
    /// Reading the pickup directory once is a race the test loses silently. Writing an <c>.eml</c> is not
    /// instantaneous, so a dispatch that has already committed its database rows can still have nothing on
    /// disk when the assertion runs — which is exactly how the twenty-attempt idempotency test failed:
    /// <c>reminder.Status == SENT</c> and one <c>sent_email_recipients</c> row both held, and the mailbox
    /// reported <b>0</b> files. A broken exactly-once guard produces two messages, never zero, so that
    /// failure was never about the product.
    /// </para>
    /// <para>
    /// The settle window matters as much as the timeout, and is why this cannot be replaced by simply
    /// waiting longer: returning the instant the count is right would make "exactly one" unfalsifiable,
    /// since the duplicate a duplicate-suppression test is looking for is precisely the message that
    /// arrives second. Too many messages fails at once — more will not turn into fewer.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<EmlMessage>> AwaitMessagesAsync(
        Func<EmlMessage, bool> matches,
        int expected,
        string recipient,
        CancellationToken ct = default)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        string? lastReadFailure = null;

        List<(string Path, EmlMessage Message)> Read()
        {
            var found = new List<(string, EmlMessage)>();
            foreach (var path in Messages())
            {
                try
                {
                    var message = new EmlMessage(File.ReadAllText(path));
                    if (matches(message))
                        found.Add((path, message));
                }
                catch (IOException ex)
                {
                    // Half-written file: not an answer yet, and not something to report unless we run out
                    // of time still seeing it.
                    lastReadFailure = $"{Path.GetFileName(path)}: {ex.Message}";
                }
            }

            return found;
        }

        while (true)
        {
            var observed = Read();

            if (observed.Count > expected)
                throw new XunitException(Explain("more messages than expected", observed, expected, recipient, started, lastReadFailure));

            if (observed.Count == expected)
            {
                var settleUntil = started.Elapsed + MessageSettleWindow;
                while (started.Elapsed < settleUntil)
                {
                    await Task.Delay(MessagePollInterval, ct);
                    var recheck = Read();
                    if (recheck.Count > expected)
                        throw new XunitException(Explain("a further message arrived after the expected ones", recheck, expected, recipient, started, lastReadFailure));
                    observed = recheck;
                }

                if (observed.Count != expected)
                    throw new XunitException(Explain("the count changed while settling", observed, expected, recipient, started, lastReadFailure));

                return observed.Select(o => o.Message).ToList();
            }

            if (started.Elapsed >= MessageTimeout)
                throw new XunitException(Explain("timed out waiting for messages", observed, expected, recipient, started, lastReadFailure));

            await Task.Delay(MessagePollInterval, ct);
        }
    }

    private string Explain(
        string what,
        IReadOnlyList<(string Path, EmlMessage Message)> observed,
        int expected,
        string recipient,
        System.Diagnostics.Stopwatch started,
        string? lastReadFailure)
    {
        var all = Messages();
        var lines = new List<string>
        {
            $"File-sink evidence: {what}.",
            $"  recipient : {recipient}",
            $"  expected  : {expected}",
            $"  actual    : {observed.Count} matching",
            $"  elapsed   : {started.ElapsedMilliseconds} ms (timeout {MessageTimeout.TotalMilliseconds:0} ms, "
                + $"settle {MessageSettleWindow.TotalMilliseconds:0} ms)",
            $"  pickup    : {PickupDirectory}",
            $"  files     : {(all.Length == 0 ? "(none)" : "")}",
        };

        foreach (var path in all)
        {
            var name = Path.GetFileName(path);
            var isMatch = observed.Any(o => string.Equals(o.Path, path, StringComparison.Ordinal));
            string to;
            try { to = new EmlMessage(File.ReadAllText(path)).Header("To"); }
            catch (IOException ex) { to = $"<unreadable: {ex.Message}>"; }
            lines.Add($"    {(isMatch ? "*" : " ")} {name}  To: {to}");
        }

        if (lastReadFailure is not null)
            lines.Add($"  last read error: {lastReadFailure}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>The single message produced, parsed. Fails when there is not exactly one.</summary>
    public EmlMessage OnlyMessage()
    {
        var files = Messages();
        Assert.Single(files);
        return new EmlMessage(File.ReadAllText(files[0]));
    }

    public void ClearMessages()
    {
        foreach (var file in Messages()) File.Delete(file);
    }

    /// <summary>Removes only the history rows addressed to <see cref="Marker"/>.</summary>
    public async Task CleanupAsync()
    {
        using var db = NewContext();
        var ids = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.RecipientEmail == Marker)
            .Select(r => r.SentEmailId).Distinct().ToListAsync();

        if (ids.Count == 0) return;

        await db.SentEmailRecipients.Where(r => ids.Contains(r.SentEmailId)).ExecuteDeleteAsync();
        await db.SentEmails.Where(e => ids.Contains(e.SentEmailId)).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Runs <paramref name="body"/> with a template column temporarily changed — the same UPDATE the
    /// template screen performs — and always restores the seeded value, so suites that assert on the
    /// real seed content are unaffected by a hot-edit test running beside them.
    /// </summary>
    public static async Task WithTemplateAsync(
        ApplicationDbContext db,
        string templateCode,
        Action<PEMS.Domain.Entities.Emails.EmailTemplate> edit,
        Func<Task> body)
    {
        var row = await db.EmailTemplates.SingleAsync(t => t.TemplateCode == templateCode);
        var subjectVi = row.SubjectVi;
        var subjectEn = row.SubjectEn;
        var bodyVi = row.BodyVi;
        var bodyEn = row.BodyEn;
        var status = row.Status;

        try
        {
            edit(row);
            await db.SaveChangesAsync();
            await body();
        }
        finally
        {
            row.SubjectVi = subjectVi;
            row.SubjectEn = subjectEn;
            row.BodyVi = bodyVi;
            row.BodyEn = bodyEn;
            row.Status = status;
            await db.SaveChangesAsync();
        }
    }
}
