using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// What the dispatcher does to the caller's unit of work, proven against a real MySQL provider rather
/// than a mocked <c>SaveChangesAsync</c>.
///
/// <para>
/// The dispatcher shares the caller's DbContext and calls SaveChanges to record the message, so it sits
/// one mistake away from committing a business change early as a side effect of sending mail. Every
/// assertion about "did not reach the database" is made through a SEPARATE context, because an in-memory
/// <c>EntityState</c> check alone cannot tell a pending write from a completed one.
/// </para>
/// </summary>
public sealed class SystemEmailDispatcherBoundaryTests
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

    /// <summary>A seeded template with a real row, so the renderer under test is the real one.</summary>
    private const string Code = SystemEmailTemplates.AccountActivated;

    private const string Marker = "dispatcher-boundary@fpt.edu.vn";

    /// <summary>Returns a fixed provider outcome and remembers what it was handed.</summary>
    private sealed class FakeSender : IEmailService
    {
        private readonly EmailDeliveryResult _result;
        private readonly Action? _onSend;

        public OutboundEmail? Last { get; private set; }
        public int SendCount { get; private set; }

        public FakeSender(EmailDeliveryResult result, Action? onSend = null)
        {
            _result = result;
            _onSend = onSend;
        }

        public Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken ct = default)
        {
            Last = message;
            SendCount++;
            _onSend?.Invoke();
            return Task.FromResult(_result);
        }

        public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
        { Last = message; SendCount++; _onSend?.Invoke(); return Task.CompletedTask; }

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.FromResult(_result);
    }

    private static SystemEmailRequest Request() => new(
        Code,
        new EmailRecipient(Marker, "Nguyễn Văn A"),
        new Dictionary<string, string>
        {
            ["fullName"] = "Nguyễn Văn A",
            ["roleName"] = "Staff",
            ["campusName"] = "HCM",
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] = "<div>login</div>",
        },
        RelatedType: "User",
        RelatedId: 3);

    private static SystemEmailDispatcher Dispatcher(ApplicationDbContext db, IEmailService sender)
        => new(db, new EmailTemplateRenderer(db), sender);

    /// <summary>Removes only the rows this class creates, identified by its marker address.</summary>
    private static async Task CleanupAsync()
    {
        using var db = NewContext();
        var ids = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.RecipientEmail == Marker)
            .Select(r => r.SentEmailId)
            .Distinct()
            .ToListAsync();

        if (ids.Count == 0) return;

        await db.SentEmailRecipients.Where(r => ids.Contains(r.SentEmailId)).ExecuteDeleteAsync();
        await db.SentEmails.Where(e => ids.Contains(e.SentEmailId)).ExecuteDeleteAsync();
    }

    /// <summary>Reads back the message this class just wrote, through a context that never saw it written.</summary>
    private static async Task<SentEmail?> ReadBackAsync(ulong sentEmailId)
    {
        using var verify = NewContext();
        return await verify.SentEmails.AsNoTracking()
            .Include(e => e.Recipients)
            .FirstOrDefaultAsync(e => e.SentEmailId == sentEmailId);
    }

    // ── 1) Render + send succeeds ────────────────────────────────────────────

    [Fact]
    public async Task A_successful_send_records_one_message_one_TO_and_stops_at_SENT()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var sender = new FakeSender(EmailDeliveryResult.Sent());

            var result = await Dispatcher(db, sender).SendAsync(Request());

            // Read through a SEPARATE context: this is what actually reached the database.
            var row = await ReadBackAsync(result.SentEmailId);
            Assert.NotNull(row);

            Assert.Equal(result.EmailTemplateId, row!.EmailTemplateId);
            Assert.False(string.IsNullOrWhiteSpace(row.Subject));
            Assert.False(string.IsNullOrWhiteSpace(row.BodySnapshot));
            // The snapshot is the rendered content, not the template source.
            Assert.DoesNotContain("{{", row.BodySnapshot!);
            Assert.Contains("Nguyễn Văn A", row.BodySnapshot!);

            var recipient = Assert.Single(row.Recipients);
            Assert.Equal(Marker, recipient.RecipientEmail);
            Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);
            Assert.DoesNotContain(row.Recipients, r => r.RecipientType != EmailRecipientTypes.To);

            // Provider acceptance is as far as PEMS can honestly go — there is no delivery webhook.
            Assert.Equal("SENT", row.Status);
            Assert.NotNull(row.SentAt);
            Assert.Null(row.DeliveredAt);
            Assert.Equal("SENT", recipient.DeliveryStatus);
            Assert.Null(recipient.DeliveredAt);

            // One message, one addressee, no copies.
            Assert.Equal(1, sender.SendCount);
            Assert.Single(sender.Last!.To);
            Assert.Empty(sender.Last!.Cc);
            Assert.Empty(sender.Last!.Bcc);
        }
        finally { await CleanupAsync(); }
    }

    // ── 2) Sender skipped ────────────────────────────────────────────────────

    [Fact]
    public async Task A_skipped_send_leaves_the_row_QUEUED_with_exactly_one_recipient()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var skipped = EmailDeliveryResult.Skipped("SMTP_DISABLED", "SMTP is not enabled in this environment.");

            var result = await Dispatcher(db, new FakeSender(skipped)).SendAsync(Request());

            var row = await ReadBackAsync(result.SentEmailId);
            Assert.NotNull(row);

            // Nothing reached a provider, so QUEUED is the truthful state — sent_emails.status has no
            // SKIPPED member and inventing one would need a schema change.
            Assert.Equal("QUEUED", row!.Status);
            Assert.Null(row.SentAt);
            Assert.Equal("QUEUED", Assert.Single(row.Recipients).DeliveryStatus);
            Assert.Equal("SKIPPED", result.NotificationStatus);

            // No duplicate history was produced by the non-send.
            using var verify = NewContext();
            Assert.Equal(1, await verify.SentEmailRecipients.CountAsync(r => r.RecipientEmail == Marker));
            Assert.Equal(1, await verify.SentEmails.CountAsync(e => e.SentEmailId == result.SentEmailId));
        }
        finally { await CleanupAsync(); }
    }

    // ── 3) Sender fails ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_failed_send_records_FAILED_and_leaves_committed_business_data_untouched()
    {
        RequireDb();
        try
        {
            using var db = NewContext();

            // A business change the caller committed BEFORE sending — the order every account handler uses.
            var template = await db.EmailTemplates.FirstAsync(t => t.TemplateCode == Code);
            var originalName = template.Name;
            template.Name = originalName + " (committed before send)";
            await db.SaveChangesAsync();

            var failed = EmailDeliveryResult.Failed("SMTP_SEND_FAILED", "Email delivery failed.");
            var result = await Dispatcher(db, new FakeSender(failed)).SendAsync(Request());

            var row = await ReadBackAsync(result.SentEmailId);
            Assert.Equal("FAILED", row!.Status);
            Assert.Null(row.SentAt);
            Assert.Equal("Email delivery failed.", row.ErrorMessage);
            Assert.Equal("FAILED", Assert.Single(row.Recipients).DeliveryStatus);

            // The committed business change survives the email failure — confirmed from another context.
            using (var verify = NewContext())
            {
                var seen = await verify.EmailTemplates.AsNoTracking().FirstAsync(t => t.TemplateCode == Code);
                Assert.Equal(originalName + " (committed before send)", seen.Name);
            }

            // Restore the seeded value so the shared disposable database is left as found.
            template.Name = originalName;
            await db.SaveChangesAsync();
        }
        finally { await CleanupAsync(); }
    }

    // ── 4) Broken template ───────────────────────────────────────────────────

    [Fact]
    public async Task An_inactive_template_fails_with_a_stable_code_and_writes_no_history()
    {
        RequireDb();
        try
        {
            using var db = NewContext();

            var template = await db.EmailTemplates.FirstAsync(t => t.TemplateCode == Code);
            template.Status = "INACTIVE";
            await db.SaveChangesAsync();

            try
            {
                var sender = new FakeSender(EmailDeliveryResult.Sent());

                var ex = await Assert.ThrowsAsync<ConflictException>(
                    () => Dispatcher(db, sender).SendAsync(Request()));
                Assert.Equal(EmailErrorCodes.TemplateInactive, ex.ErrorCode);

                Assert.Equal(0, sender.SendCount);

                using var verify = NewContext();
                Assert.Equal(0, await verify.SentEmailRecipients.CountAsync(r => r.RecipientEmail == Marker));
            }
            finally
            {
                template.Status = "ACTIVE";
                await db.SaveChangesAsync();
            }
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_render_failure_leaves_a_pending_business_insert_pending_and_out_of_the_database()
    {
        RequireDb();
        try
        {
            using var db = NewContext();

            // A caller that has NOT yet saved. This is the precondition violation the dispatcher must not
            // convert into a silent early write.
            var pending = new EmailTemplate
            {
                TemplateCode = "DISPATCHER_BOUNDARY_PROBE",
                Name = "probe",
                Purpose = PEMS.Domain.Constants.EmailTemplatePurposes.Account,
                Status = "ACTIVE",
                SubjectVi = "x", BodyVi = "x", SubjectEn = "x", BodyEn = "x",
                CreatedAt = DateTime.Now,
            };
            db.EmailTemplates.Add(pending);
            Assert.Equal(EntityState.Added, db.Entry(pending).State);

            // A variable the template does not declare — the renderer refuses before anything is written.
            var badRequest = Request() with
            {
                Variables = new Dictionary<string, string> { ["fullName"] = "A" },
            };

            var sender = new FakeSender(EmailDeliveryResult.Sent());
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => Dispatcher(db, sender).SendAsync(badRequest));
            Assert.Equal(EmailErrorCodes.TemplateVariableMissing, ex.ErrorCode);

            // Still only tracked…
            Assert.Equal(EntityState.Added, db.Entry(pending).State);

            // …and, checked from a context that shares no change tracker, never written.
            using var verify = NewContext();
            Assert.False(
                await verify.EmailTemplates.AsNoTracking().AnyAsync(t => t.TemplateCode == "DISPATCHER_BOUNDARY_PROBE"),
                "The dispatcher flushed a business entity the caller had not saved.");
            Assert.Equal(0, await verify.SentEmailRecipients.CountAsync(r => r.RecipientEmail == Marker));
            Assert.Equal(0, sender.SendCount);
        }
        finally { await CleanupAsync(); }
    }

    // ── 5) Transactions ──────────────────────────────────────────────────────

    [Fact]
    public async Task The_dispatcher_opens_no_transaction_of_its_own()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            Assert.Null(db.Database.CurrentTransaction);

            await Dispatcher(db, new FakeSender(EmailDeliveryResult.Sent())).SendAsync(Request());

            Assert.Null(db.Database.CurrentTransaction);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task An_outer_transaction_still_belongs_to_the_caller_and_can_be_rolled_back()
    {
        RequireDb();

        ulong sentEmailId;
        using (var db = NewContext())
        {
            // The dispatcher is never called inside a transaction by the account handlers, but if one is
            // open it must remain the caller's to commit or abandon.
            using var tx = await db.Database.BeginTransactionAsync();

            var result = await Dispatcher(db, new FakeSender(EmailDeliveryResult.Sent())).SendAsync(Request());
            sentEmailId = result.SentEmailId;

            Assert.NotNull(db.Database.CurrentTransaction);
            await tx.RollbackAsync();
        }

        // The caller rolled back, so the message it recorded went with it — the dispatcher neither
        // committed early nor kept a transaction of its own alive.
        Assert.Null(await ReadBackAsync(sentEmailId));
    }

    // ── 6) Business ordering, both handler shapes ────────────────────────────

    [Fact]
    public async Task Business_data_saved_with_SaveChanges_is_readable_elsewhere_when_the_sender_runs()
    {
        RequireDb();
        try
        {
            using var db = NewContext();

            var template = await db.EmailTemplates.FirstAsync(t => t.TemplateCode == Code);
            var originalDescription = template.Description;
            template.Description = "saved before send";
            await db.SaveChangesAsync();          // the C-02 / C-06 / C-07 shape

            var visibleDuringSend = false;
            var sender = new FakeSender(EmailDeliveryResult.Sent(), onSend: () =>
            {
                using var verify = NewContext();
                visibleDuringSend = verify.EmailTemplates.AsNoTracking()
                    .Any(t => t.TemplateCode == Code && t.Description == "saved before send");
            });

            await Dispatcher(db, sender).SendAsync(Request());

            Assert.True(visibleDuringSend,
                "The business change was not durable at the moment the email was sent.");

            template.Description = originalDescription;
            await db.SaveChangesAsync();
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task Business_data_committed_with_a_transaction_is_readable_elsewhere_when_the_sender_runs()
    {
        RequireDb();
        try
        {
            using var db = NewContext();

            var template = await db.EmailTemplates.FirstAsync(t => t.TemplateCode == Code);
            var originalDescription = template.Description;

            // the C-01 / C-05 / C-08 / C-10 shape: commit, dispose the transaction, then send
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                template.Description = "committed before send";
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }

            var visibleDuringSend = false;
            var sender = new FakeSender(EmailDeliveryResult.Sent(), onSend: () =>
            {
                using var verify = NewContext();
                visibleDuringSend = verify.EmailTemplates.AsNoTracking()
                    .Any(t => t.TemplateCode == Code && t.Description == "committed before send");
            });

            await Dispatcher(db, sender).SendAsync(Request());

            Assert.True(visibleDuringSend,
                "The committed business change was not visible to another connection during the send.");

            template.Description = originalDescription;
            await db.SaveChangesAsync();
        }
        finally { await CleanupAsync(); }
    }
}
