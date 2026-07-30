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

    public SystemEmailDispatcher Dispatcher(ApplicationDbContext db, string? brokenHost = null)
        => new(db, new EmailTemplateRenderer(db), Sender(brokenHost));

    public string[] Messages()
        => Directory.Exists(PickupDirectory) ? Directory.GetFiles(PickupDirectory, "*.eml") : Array.Empty<string>();

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
