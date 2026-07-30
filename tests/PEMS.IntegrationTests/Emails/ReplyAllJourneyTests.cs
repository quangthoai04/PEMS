using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Commands.ReplytoEmail;
using PEMS.Application.Emails.Commands.SendEmail;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.FileStorage;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Reply and Reply All for real: real handlers, a real database, real MIME on disk (G11-H §7.2, §7.5).
///
/// <para>
/// Reply All did not exist before this change, and it is the single most dangerous thing to add to a mail
/// feature: the obvious implementation reads the original's recipient rows and copies them into the new
/// message, and the original's recipient rows include its blind copies. Doing that would announce to
/// every visible recipient exactly who had been quietly included — the one thing BCC promises will not
/// happen, and irreversible once sent.
/// </para>
/// <para>
/// So the assertions below are mostly negative, and they are made against the bytes actually written to
/// the pickup directory rather than against the command that produced them. A handler that intended to
/// exclude a blind copy and a message that does not contain one are different claims, and only the second
/// is worth anything.
/// </para>
/// </summary>
public sealed class ReplyAllJourneyTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g11h-replyall@partner.example.com");
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-g11h-files-" + Guid.NewGuid().ToString("N"));

    /// <summary>Suite-private id range. Cleanup keys on these exact ids, never on a range scan.</summary>
    private const ulong Base = 991_400;
    private const ulong CampusId = Base + 1;
    private const ulong AuthorId = Base + 2;
    private const ulong PartnerId = Base + 3;

    /// <summary>
    /// The person who was on BCC. A real account rather than a bare address, because replying requires
    /// one: <c>sent_emails.sent_by</c> is a foreign key, so a "user" who exists only as a string cannot
    /// send anything and the test would be exercising a shape production cannot produce.
    /// </summary>
    private const ulong HiddenReaderId = Base + 4;

    private static string Mail(ulong userId) => $"g11h-{userId}@partner.example.com";

    private const string VisibleB = "g11h-visible-b@partner.example.com";
    private const string CopiedC = "g11h-copied-c@partner.example.com";
    private static string Hidden => Mail(HiddenReaderId);

    public void Dispose()
    {
        _h.Dispose();
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email { get; init; }
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole => null;
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class NoHttpClients : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static readonly HtmlSanitizerService Sanitizer = new();
    private static readonly IOptions<EmailRecipientOptions> Recipients =
        Options.Create(new EmailRecipientOptions());

    private static ICurrentUserService Author => new FakeCurrentUser
    {
        UserId = AuthorId, Email = Mail(AuthorId), RoleCode = "STAFF", PrimaryCampusId = CampusId,
    };

    private static ICurrentUserService Partner => new FakeCurrentUser
    {
        UserId = PartnerId, Email = Mail(PartnerId), RoleCode = "STAFF", PrimaryCampusId = CampusId,
    };

    private LocalFileStorageService Storage() => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })
            .Build(),
        new NoHttpClients(),
        new NoServices(),
        NullLogger<LocalFileStorageService>.Instance);

    private PEMS.Application.Emails.Utils.EmailImageLayoutNormalizer Normalizer(ApplicationDbContext db)
        => new(db, Storage());

    private ManualEmailSender Sender(ApplicationDbContext db) => new(db, _h.Sender());

    private SendEmailCommandHandler Compose(ApplicationDbContext db, ICurrentUserService user)
        => new(user, Sanitizer, Sender(db), Normalizer(db), Recipients);

    private ReplytoEmailCommandHandler Reply(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, Sanitizer, Sender(db), Recipients);

    // ── Seed ────────────────────────────────────────────────────────────────

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        await CleanupRowsAsync(db);

        var roles = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        var staffRole = roles.First(r => r.RoleCode == "STAFF").RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, {1}, {2}, 'ACTIVE')",
            CampusId, "G11H", "PEMS G11H Campus");

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            CampusId, CampusId, "PEMS G11H Văn phòng IC");

        async Task User(ulong id, string name)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
                + $"department_id, status) VALUES ({id}, {{0}}, {{1}}, {staffRole}, 'STAFF', {CampusId}, "
                + $"{CampusId}, 'ACTIVE')",
                name, Mail(id));

        await User(AuthorId, "PEMS G11H Người soạn");
        await User(PartnerId, "PEMS G11H Đối tác");
        await User(HiddenReaderId, "PEMS G11H Người nhận ẩn");
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    private static async Task CleanupRowsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM sent_emails WHERE sent_by IN ({AuthorId}, {PartnerId}, {HiddenReaderId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM users WHERE user_id IN ({AuthorId}, {PartnerId}, {HiddenReaderId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM departments WHERE department_id = {CampusId}");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM campuses WHERE campus_id = {CampusId}");
    }

    /// <summary>
    /// The author sends one message: TO partner + a second visible person, CC a third, BCC a fourth.
    /// Returns the id of the stored message.
    /// </summary>
    private async Task<ulong> SendOriginalAsync(ApplicationDbContext db)
    {
        var sent = await Compose(db, Author).Handle(new SendEmailCommand
        {
            Subject = "Phối hợp đón đoàn tháng 8",
            Body = "<p>Kính gửi anh chị,</p><p>Nhờ hỗ trợ đón đoàn ngày 12/08.</p>",
            To = new List<EmailRecipientDto>
            {
                new() { Email = Mail(PartnerId), Name = "Đối tác" },
                new() { Email = VisibleB, Name = "Người nhận B" },
            },
            Cc = new List<EmailRecipientDto> { new() { Email = CopiedC, Name = "Người được sao chép" } },
            Bcc = new List<EmailRecipientDto> { new() { Email = Hidden, Name = "Người nhận ẩn" } },
        }, CancellationToken.None);

        Assert.True(sent.SentEmailId.HasValue);
        _h.ClearMessages();     // the original's own .eml must not be counted as the reply's
        return sent.SentEmailId!.Value;
    }

    private EmlMessage OnlyReply() => _h.OnlyMessage();

    // ── Reply ───────────────────────────────────────────────────────────────

    /// <summary>Plain Reply goes to the original author, and to nobody else at all.</summary>
    [Fact]
    public async Task A_real_reply_addresses_only_the_original_sender()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        try
        {
            var originalId = await SendOriginalAsync(db);

            var result = await Reply(db, Partner).Handle(new ReplytoEmailCommand
            {
                OriginalEmailId = originalId,
                Body = "<p>Chúng tôi xác nhận tham dự.</p>",
                ReplyAll = false,
            }, CancellationToken.None);

            Assert.True(result.Success);

            var eml = OnlyReply();
            Assert.Contains(Mail(AuthorId), eml.Header("To"));
            Assert.Equal(1, eml.AddressCount("To"));
            Assert.Equal(string.Empty, eml.Header("Cc"));

            // Nobody from the original message comes along.
            Assert.DoesNotContain(VisibleB, eml.Raw);
            Assert.DoesNotContain(CopiedC, eml.Raw);
            Assert.DoesNotContain(Hidden, eml.Raw);
        }
        finally { await CleanupRowsAsync(db); }
    }

    // ── Reply All ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reply All reaches the author and the other VISIBLE recipient, copies the CC, excludes the replier,
    /// and — the point of the whole exercise — never touches the blind copy.
    /// </summary>
    [Fact]
    public async Task A_real_reply_all_reaches_the_visible_recipients_and_never_the_blind_one()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        try
        {
            var originalId = await SendOriginalAsync(db);

            var result = await Reply(db, Partner).Handle(new ReplytoEmailCommand
            {
                OriginalEmailId = originalId,
                Body = "<p>Chúng tôi xác nhận tham dự và đã phân công đầu mối.</p>",
                ReplyAll = true,
            }, CancellationToken.None);

            Assert.True(result.Success);

            var eml = OnlyReply();

            // The author, plus the other person who was visibly on the TO line.
            Assert.Contains(Mail(AuthorId), eml.Header("To"));
            Assert.Contains(VisibleB, eml.Header("To"));

            // The CC stays a CC.
            Assert.Contains(CopiedC, eml.Header("Cc"));

            // The replier does not copy themselves.
            Assert.DoesNotContain(Mail(PartnerId), eml.Header("To"));
            Assert.DoesNotContain(Mail(PartnerId), eml.Header("Cc"));

            // And the blind copy appears NOWHERE in the message — not in a header, not in the body.
            Assert.DoesNotContain(Hidden, eml.Raw);
        }
        finally { await CleanupRowsAsync(db); }
    }

    /// <summary>
    /// BCC is an envelope fact, never a header — asserted on the reply as well as on an original send,
    /// because Reply All is the path most likely to reintroduce it.
    /// </summary>
    [Fact]
    public async Task A_reply_all_writes_no_bcc_header_and_leaks_nothing_into_the_body()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        try
        {
            var originalId = await SendOriginalAsync(db);

            await Reply(db, Partner).Handle(new ReplytoEmailCommand
            {
                OriginalEmailId = originalId,
                Body = "<p>Xác nhận.</p>",
                ReplyAll = true,
                Bcc = new List<EmailRecipientInput> { new() { Email = "g11h-my-own-bcc@partner.example.com" } },
            }, CancellationToken.None);

            var eml = OnlyReply();

            // The author's OWN blind copy is delivered, but is not announced in the MIME headers.
            Assert.Equal(string.Empty, eml.Header("Bcc"));
            Assert.DoesNotContain("g11h-my-own-bcc@partner.example.com", eml.Header("To"));
            Assert.DoesNotContain("g11h-my-own-bcc@partner.example.com", eml.Header("Cc"));

            // …and the original's blind copy is still nowhere at all.
            Assert.DoesNotContain(Hidden, eml.Raw);
        }
        finally { await CleanupRowsAsync(db); }
    }

    /// <summary>
    /// The history of a Reply All records the recipients it actually addressed, so an auditor sees the
    /// same set the message carried — and the original's blind copy is not among them.
    /// </summary>
    [Fact]
    public async Task The_stored_history_of_a_reply_all_matches_what_was_sent()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        try
        {
            var originalId = await SendOriginalAsync(db);

            await Reply(db, Partner).Handle(new ReplytoEmailCommand
            {
                OriginalEmailId = originalId,
                Body = "<p>Xác nhận.</p>",
                ReplyAll = true,
            }, CancellationToken.None);

            using var verify = EmailEvidenceHarness.NewContext();
            var reply = await verify.SentEmails.AsNoTracking()
                .Where(e => e.SentBy == PartnerId && e.RelatedType == "REPLY" && e.RelatedId == originalId)
                .OrderByDescending(e => e.SentEmailId)
                .FirstAsync();

            var recipients = await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == reply.SentEmailId)
                .Select(r => new { r.RecipientEmail, r.RecipientType })
                .ToListAsync();

            Assert.Contains(recipients, r => r.RecipientEmail == Mail(AuthorId) && r.RecipientType == "TO");
            Assert.Contains(recipients, r => r.RecipientEmail == VisibleB && r.RecipientType == "TO");
            Assert.Contains(recipients, r => r.RecipientEmail == CopiedC && r.RecipientType == "CC");

            Assert.DoesNotContain(recipients, r => r.RecipientEmail == Hidden);
            Assert.DoesNotContain(recipients, r => r.RecipientEmail == Mail(PartnerId));
        }
        finally { await CleanupRowsAsync(db); }
    }

    /// <summary>
    /// Being party to the message is what grants the right to reply, in either mode. A role does not:
    /// somebody who was never on the message has nothing to reply to, and Reply All would hand them the
    /// whole recipient list.
    /// </summary>
    [Fact]
    public async Task A_stranger_cannot_reply_all_to_a_message_they_were_never_on()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        try
        {
            var originalId = await SendOriginalAsync(db);

            var stranger = new FakeCurrentUser
            {
                UserId = Base + 99,
                Email = "g11h-stranger@partner.example.com",
                RoleCode = "HO",
                PrimaryCampusId = CampusId,
            };

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Reply(db, stranger).Handle(new ReplytoEmailCommand
                {
                    OriginalEmailId = originalId,
                    Body = "<p>Tôi cũng muốn trả lời.</p>",
                    ReplyAll = true,
                }, CancellationToken.None));

            Assert.Empty(_h.Messages());
        }
        finally { await CleanupRowsAsync(db); }
    }

    /// <summary>
    /// A person who was on BCC may reply — their copy showed them the TO and CC lines — but Reply All from
    /// them still carries no blind copy, including their own entry. Otherwise replying would out them.
    /// </summary>
    [Fact]
    public async Task A_blind_copy_recipient_replying_all_does_not_reveal_themselves()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        try
        {
            var originalId = await SendOriginalAsync(db);

            var hiddenReader = new FakeCurrentUser
            {
                UserId = HiddenReaderId, Email = Hidden, RoleCode = "STAFF", PrimaryCampusId = CampusId,
            };

            await Reply(db, hiddenReader).Handle(new ReplytoEmailCommand
            {
                OriginalEmailId = originalId,
                Body = "<p>Tôi đã nhận được thông tin.</p>",
                ReplyAll = true,
            }, CancellationToken.None);

            var eml = OnlyReply();

            Assert.Contains(Mail(AuthorId), eml.Header("To"));
            Assert.Contains(VisibleB, eml.Header("To"));
            Assert.Contains(CopiedC, eml.Header("Cc"));

            // Their own blind address is not added to the reply's visible headers by the planner.
            Assert.DoesNotContain(Hidden, eml.Header("To"));
            Assert.DoesNotContain(Hidden, eml.Header("Cc"));
        }
        finally { await CleanupRowsAsync(db); }
    }
}
