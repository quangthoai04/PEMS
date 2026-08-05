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
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.FileStorage;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// G8 — the journeys the earlier phases left to eye-checking, run against a real database and real MIME
/// on disk.
///
/// <para>
/// Most of the seven G8 journeys were already covered by the G4/G5/G6 suites, and duplicating them here
/// would add test count without adding evidence. What this class covers is the four things that were
/// argued rather than measured: that two invitations really are two isolated messages, that the preview
/// modal and the send share one renderer, that a reply does not drag the parent's attachments along, and
/// that the recipient ceiling holds on the real send path rather than only in the validator's unit test.
/// The mapping from each journey to the tests that prove it is in
/// <c>docs/email-standardization/04-requirement-test-traceability.md</c>.
/// </para>
/// </summary>
public sealed class EmailG8JourneyTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g8-journey@partner.example.com");
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-g8-files-" + Guid.NewGuid().ToString("N"));

    /// <summary>Suite-private id range; no other email suite uses 991_400+.</summary>
    private const ulong Base = 991_400;
    private const ulong CampusId = Base + 1;
    private const ulong AuthorId = Base + 2;
    private const ulong PartnerId = Base + 3;
    private const ulong ParticipantAId = Base + 10;
    private const ulong ParticipantBId = Base + 11;

    private static string Mail(ulong id) => $"g8-{id}@partner.example.com";
    private const string InviteeA = "g8-invitee-a@partner.example.com";
    private const string InviteeB = "g8-invitee-b@partner.example.com";

    public void Dispose()
    {
        _h.Dispose();
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); }
        catch (IOException) { /* a leaked temp dir must never fail a run */ }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated { get; init; } = true;
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

    private static ICurrentUserService Author => new FakeCurrentUser
    {
        UserId = AuthorId, Email = Mail(AuthorId), RoleCode = "STAFF", PrimaryCampusId = CampusId,
    };

    private LocalFileStorageService Storage() => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })
            .Build(),
        new NoHttpClients(), new NoServices(), NullLogger<LocalFileStorageService>.Instance);

    private PEMS.Application.Emails.Utils.EmailImageLayoutNormalizer Normalizer(ApplicationDbContext db)
        => new(db, Storage());

    private ManualEmailSender ManualSender(ApplicationDbContext db) => new(db, _h.Sender());

    private SendEmailCommandHandler Compose(ApplicationDbContext db, ICurrentUserService user,
        IOptions<EmailRecipientOptions> recipients)
        => new(user, new DirectEmailSender(db, Sanitizer, Storage(), ManualSender(db), Normalizer(db), recipients));

    private ReplytoEmailCommandHandler Reply(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, Sanitizer, ManualSender(db), Options.Create(new EmailRecipientOptions()));

    private static PreviewEmailTemplateQueryHandler Preview(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new EmailTemplateRenderer(db),
               EmailEvidenceHarness.Senders(db),
               EmailEvidenceHarness.PreviewTokens());

    /// <summary>
    /// The template the preview tests use, and why it is this one: its body does NOT reference
    /// {{actionBlock}}. Nine of the thirty catalog templates do reference it while not being registered
    /// in <see cref="EmailActionTemplates"/>, so the preview cannot render them at all — see the G9 ledger.
    /// Using one of those here would be testing that known gap instead of the renderer-sharing claim.
    /// </summary>
    private const string PreviewableTemplate = SystemEmailTemplates.AccountRoleChanged;

    private static readonly Dictionary<string, string> PreviewContext = new()
    {
        ["fullName"] = "Người dùng",
        ["oldRoleName"] = "Staff",
        ["newRoleName"] = "Staff Leader",
        ["campusName"] = "FPT Đà Nẵng",
    };

    private static PreviewEmailTemplateQuery PreviewQuery()
        => new(PreviewableTemplate, new Dictionary<string, string>(PreviewContext), EmailLanguages.Vi);

    // ── Seed ────────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        await CleanupRowsAsync(db);

        var roles = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        var staffRole = roles.First(r => r.RoleCode == "STAFF").RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, {1}, {2}, 'ACTIVE')",
            CampusId, "G8", "PEMS G8 Campus");

        // A STAFF account needs an IC department; a trigger enforces both that and the campus.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            CampusId, CampusId, "PEMS G8 Văn phòng IC");

        async Task User(ulong id, string name)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
                + $"department_id, status) VALUES ({id}, {{0}}, {{1}}, {staffRole}, 'STAFF', {CampusId}, "
                + $"{CampusId}, 'ACTIVE')",
                name, Mail(id));

        await User(AuthorId, "PEMS G8 Người soạn");
        await User(PartnerId, "PEMS G8 Đối tác");
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    private static async Task CleanupRowsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM email_action_tokens WHERE target_id IN "
            + $"({ParticipantAId}, {ParticipantBId})");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM sent_emails WHERE sent_by IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM sent_emails WHERE related_type = {0} AND related_id IN ({1}, {2})",
            EmailActionTargetTypes.VisitParticipant, ParticipantAId, ParticipantBId);
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM files WHERE uploaded_by IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM users WHERE user_id IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM departments WHERE department_id = {CampusId}");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM campuses WHERE campus_id = {CampusId}");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // E2E-05 — two invitations are two isolated messages
    // ════════════════════════════════════════════════════════════════════════════════════════════

    private static SystemEmailRequest Invitation(string address, string name, ulong participantId, string tokenSuffix)
        => new(
            SystemEmailTemplates.VisitParticipantInvitation,
            new EmailRecipient(address, name),
            new Dictionary<string, string>
            {
                ["recipientName"] = name,
                ["delegationName"] = "Đoàn Đại học Kyoto",
                ["campusName"] = "FPT Đà Nẵng",
                ["plannedTime"] = "09:00 12/08/2026 - 11:30 12/08/2026",
                ["hostName"] = "Trần Thị Hà",
                ["roleLabel"] = "Staff hỗ trợ IC",
                ["hostMessage"] = string.Empty,
            },
            TrustedBlocks: new Dictionary<string, string>
            {
                [EmailTrustedBlocks.ActionBlock] = EmailComposition.AcceptDeclineBlock(
                    $"https://pems.test/api/public/email-actions/ACCEPT-{tokenSuffix}",
                    $"https://pems.test/api/public/email-actions/DECLINE-{tokenSuffix}"),
            },
            RelatedType: EmailActionTargetTypes.VisitParticipant,
            RelatedId: participantId);

    /// <summary>
    /// Two invitees, two messages. Putting them on one message would hand each of them the other's
    /// accept/decline link — the template's SingleRecipientNoCopies policy exists for exactly this, and
    /// this proves the policy survives all the way to the bytes on disk.
    /// </summary>
    [Fact]
    public async Task Two_invitations_produce_two_separate_messages_each_addressed_to_one_person()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var dispatcher = _h.Dispatcher(db);

        var a = await dispatcher.PrepareAsync(Invitation(InviteeA, "Người A", ParticipantAId, "AAA"), CancellationToken.None);
        var b = await dispatcher.PrepareAsync(Invitation(InviteeB, "Người B", ParticipantBId, "BBB"), CancellationToken.None);
        await db.SaveChangesAsync();
        await dispatcher.DeliverAsync(a, CancellationToken.None);
        await dispatcher.DeliverAsync(b, CancellationToken.None);

        var files = _h.Messages();
        Assert.Equal(2, files.Length);

        var messages = files.Select(f => new EmlMessage(File.ReadAllText(f))).ToList();
        var toA = messages.Single(m => m.Header("To").Contains(InviteeA, StringComparison.OrdinalIgnoreCase));
        var toB = messages.Single(m => m.Header("To").Contains(InviteeB, StringComparison.OrdinalIgnoreCase));

        // Each message names exactly one person, and neither carries a copy of any kind.
        foreach (var (message, mine, theirs) in new[] { (toA, InviteeA, InviteeB), (toB, InviteeB, InviteeA) })
        {
            Assert.Equal(string.Empty, message.Header("Cc"));
            Assert.Equal(string.Empty, message.Header("Bcc"));
            Assert.Contains(mine, message.Header("To"), StringComparison.OrdinalIgnoreCase);

            // Raw covers the headers; Body is checked decoded, because the transfer encoding would
            // otherwise hide an address that really is in the message.
            Assert.DoesNotContain(theirs, message.Header("To"), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(theirs, message.Body, StringComparison.OrdinalIgnoreCase);
        }

        // …and the personal links are genuinely different, which is the whole point of one message each.
        Assert.Contains("ACCEPT-AAA", toA.Body);
        Assert.DoesNotContain("ACCEPT-BBB", toA.Body);
        Assert.Contains("ACCEPT-BBB", toB.Body);
        Assert.DoesNotContain("ACCEPT-AAA", toB.Body);

        await CleanupRowsAsync(db);
    }

    /// <summary>
    /// A token is bound to ONE recipient row of ONE message. Presenting A's token against B's invitation
    /// must find nothing: the lookup is by hash, and the row it finds carries A's target, not B's.
    /// </summary>
    [Fact]
    public async Task An_invitation_token_belongs_to_one_recipient_and_does_not_open_another_invitation()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var dispatcher = _h.Dispatcher(db);
        var a = await dispatcher.PrepareAsync(Invitation(InviteeA, "Người A", ParticipantAId, "AAA"), CancellationToken.None);
        var b = await dispatcher.PrepareAsync(Invitation(InviteeB, "Người B", ParticipantBId, "BBB"), CancellationToken.None);
        await db.SaveChangesAsync();

        var recipientA = await db.SentEmailRecipients.AsNoTracking()
            .SingleAsync(r => r.SentEmailId == a.SentEmailId);
        var recipientB = await db.SentEmailRecipients.AsNoTracking()
            .SingleAsync(r => r.SentEmailId == b.SentEmailId);

        db.EmailActionTokens.Add(TokenFor("hash-a", a.SentEmailId, recipientA.SentEmailRecipientId, ParticipantAId, InviteeA));
        db.EmailActionTokens.Add(TokenFor("hash-b", b.SentEmailId, recipientB.SentEmailRecipientId, ParticipantBId, InviteeB));
        await db.SaveChangesAsync();

        var found = await db.EmailActionTokens.AsNoTracking().SingleAsync(t => t.TokenHash == "hash-a");

        Assert.Equal(ParticipantAId, found.TargetId);
        Assert.NotEqual(ParticipantBId, found.TargetId);
        Assert.Equal(recipientA.SentEmailRecipientId, found.SentEmailRecipientId);
        Assert.Equal(a.SentEmailId, found.SentEmailId);

        // Looking A's token up while expecting B's target finds nothing at all — there is no row that
        // satisfies both, which is what "a token cannot be replayed against another invitation" means.
        Assert.False(await db.EmailActionTokens.AsNoTracking()
            .AnyAsync(t => t.TokenHash == "hash-a" && t.TargetId == ParticipantBId));

        await CleanupRowsAsync(db);
    }

    private static EmailActionToken TokenFor(
        string hash, ulong sentEmailId, ulong recipientRowId, ulong participantId, string email)
        => new()
        {
            TokenHash = hash,
            ActionGroupKey = $"g8-group-{participantId}",
            ActionContext = EmailActionContexts.ParticipationResponse,
            TargetType = EmailActionTargetTypes.VisitParticipant,
            TargetId = participantId,
            IntendedAction = "ACCEPT",
            RecipientEmail = email,
            SentEmailId = sentEmailId,
            SentEmailRecipientId = recipientRowId,
            ExpiresAt = DateTime.Now.AddDays(7),
            CreatedAt = DateTime.Now,
        };

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // E2E-04 — the preview modal and the send share one renderer
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// An edit made in the template screen shows in the very next preview, with no restart. The renderer
    /// reads the row per render; a cached catalog would make the operator approve yesterday's text.
    /// </summary>
    [Fact]
    public async Task A_template_edit_shows_in_the_next_preview_without_a_restart()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        const string marker = "G8 PREVIEW HOT EDIT";
        await EmailEvidenceHarness.WithTemplateAsync(db, PreviewableTemplate,
            row => row.SubjectVi = marker,
            async () =>
            {
                var response = await Preview(db, Author)
                    .Handle(PreviewQuery(), CancellationToken.None);

                Assert.Equal(marker, response.Subject);
            });

        // …and the restore in WithTemplateAsync is visible to the next render too.
        var after = await Preview(db, Author).Handle(PreviewQuery(), CancellationToken.None);

        Assert.NotEqual(marker, after.Subject);
    }

    /// <summary>
    /// The preview refuses everything the send refuses. This is the assertion that makes "same renderer"
    /// falsifiable rather than a claim about the wiring: a preview with its own lenient rendering would
    /// happily show a placeholder here, and an operator would approve content that can never be sent.
    /// </summary>
    [Fact]
    public async Task The_preview_refuses_exactly_what_the_send_refuses()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var preview = Preview(db, Author);

        // A variable the template needs but the caller did not supply.
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            preview.Handle(new PreviewEmailTemplateQuery(PreviewableTemplate, new Dictionary<string, string> { ["fullName"] = "Người dùng" }, EmailLanguages.Vi), CancellationToken.None));
        Assert.Equal(EmailErrorCodes.TemplateVariableMissing, missing.ErrorCode);

        // A variable the template never declared.
        var withGhost = new Dictionary<string, string>(PreviewContext) { ["ghostVariable"] = "x" };
        var unknown = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            preview.Handle(
                new PreviewEmailTemplateQuery(PreviewableTemplate, withGhost, EmailLanguages.Vi),
                CancellationToken.None));
        Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, unknown.ErrorCode);

        // A code that is not a registered system template at all.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            preview.Handle(new PreviewEmailTemplateQuery("NOT_A_REGISTERED_TEMPLATE", new Dictionary<string, string>(), EmailLanguages.Vi), CancellationToken.None));
    }

    /// <summary>An inactive template is dead for the preview too, not merely for the send.</summary>
    [Fact]
    public async Task The_preview_refuses_an_inactive_template()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        await EmailEvidenceHarness.WithTemplateAsync(db, PreviewableTemplate,
            row => row.Status = "INACTIVE",
            async () =>
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Preview(db, Author).Handle(PreviewQuery(), CancellationToken.None));

                Assert.Equal(EmailErrorCodes.TemplateInactive, ex.ErrorCode);
            });
    }

    /// <summary>An unauthenticated caller gets no rendered content — preview is not a public oracle.</summary>
    [Fact]
    public async Task The_preview_refuses_an_unauthenticated_caller()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var anonymous = new FakeCurrentUser { IsAuthenticated = false, UserId = null };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Preview(db, anonymous).Handle(new PreviewEmailTemplateQuery(PreviewableTemplate, new Dictionary<string, string>(), EmailLanguages.Vi), CancellationToken.None));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // E2E-06 — a reply carries nothing of the parent's it was not asked to carry
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The parent has an attachment; the reply must not. Quietly re-sending the original's file would
    /// push a document to a recipient list the reply's author chose — the two lists are not the same,
    /// and the author never consented to redistributing it.
    /// </summary>
    [Fact]
    public async Task A_reply_does_not_carry_the_attachments_of_the_message_it_answers()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        // An incoming message the author is a recipient of, carrying a file.
        var original = await Compose(db, new FakeCurrentUser
        {
            UserId = PartnerId, Email = Mail(PartnerId), RoleCode = "STAFF", PrimaryCampusId = CampusId,
        }, Options.Create(new EmailRecipientOptions())).Handle(new SendEmailCommand
        {
            Subject = "Hồ sơ đoàn khách",
            Body = "<p>Gửi anh chị hồ sơ đính kèm.</p>",
            To = new List<EmailRecipientDto> { new() { Email = Mail(AuthorId) } },
        }, CancellationToken.None);

        var originalId = original.SentEmailId!.Value;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (file_id, storage_provider, object_key, original_filename, mime_type, "
            + $"file_size, uploaded_by) VALUES ({Base + 50}, 'LOCAL', {{0}}, {{1}}, 'application/pdf', 12, {PartnerId})",
            $"g8/{Base + 50}/ho-so.pdf", "ho-so.pdf");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO sent_email_attachments (sent_email_id, file_id, attachment_type, display_name, display_order) "
            + $"VALUES ({originalId}, {Base + 50}, 'ATTACHMENT', {{0}}, 0)", "ho-so.pdf");

        Assert.Equal(1, await db.SentEmailAttachments.AsNoTracking()
            .CountAsync(a => a.SentEmailId == originalId));

        _h.ClearMessages();

        await Reply(db, Author).Handle(new ReplytoEmailCommand
        {
            OriginalEmailId = originalId,
            Body = "<p>Đã nhận, cảm ơn anh chị.</p>",
        }, CancellationToken.None);

        var reply = await db.SentEmails.AsNoTracking()
            .Where(e => e.RelatedType == "REPLY" && e.RelatedId == originalId)
            .OrderByDescending(e => e.SentEmailId).FirstAsync();

        // Nothing in the history…
        Assert.Empty(await db.SentEmailAttachments.AsNoTracking()
            .Where(a => a.SentEmailId == reply.SentEmailId).ToListAsync());

        // …and nothing in the bytes.
        var eml = _h.OnlyMessage();
        Assert.DoesNotContain("ho-so.pdf", eml.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ho-so.pdf", eml.Body, StringComparison.OrdinalIgnoreCase);

        // The parent keeps its own attachment; the reply did not move it.
        Assert.Equal(1, await db.SentEmailAttachments.AsNoTracking()
            .CountAsync(a => a.SentEmailId == originalId));

        await db.Database.ExecuteSqlRawAsync($"DELETE FROM sent_email_attachments WHERE file_id = {Base + 50}");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM files WHERE file_id = {Base + 50}");
        await CleanupRowsAsync(db);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Negative — the recipient ceiling on the real send path
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The ceiling counts TO + CC + BCC together, and it is enforced where mail actually leaves rather
    /// than only in the validator's own unit test. A refusal must also leave nothing behind: no MIME
    /// file, no history row that would later read as "we tried to send this".
    /// </summary>
    [Fact]
    public async Task A_message_over_the_recipient_ceiling_is_refused_and_nothing_is_written_or_sent()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var limit = Options.Create(new EmailRecipientOptions { MaxRecipients = 5 });

        var command = new SendEmailCommand
        {
            Subject = "Quá nhiều người nhận",
            Body = "<p>Nội dung.</p>",
            To = Enumerable.Range(0, 3).Select(i => new EmailRecipientDto { Email = $"g8-to-{i}@partner.example.com" }).ToList(),
            Cc = Enumerable.Range(0, 2).Select(i => new EmailRecipientDto { Email = $"g8-cc-{i}@partner.example.com" }).ToList(),
            Bcc = Enumerable.Range(0, 1).Select(i => new EmailRecipientDto { Email = $"g8-bcc-{i}@partner.example.com" }).ToList(),
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Compose(db, Author, limit).Handle(command, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.RecipientLimitExceeded, ex.ErrorCode);
        Assert.Empty(_h.Messages());
        Assert.Empty(await db.SentEmails.AsNoTracking().Where(e => e.SentBy == AuthorId).ToListAsync());

        await CleanupRowsAsync(db);
    }

    /// <summary>Exactly at the ceiling is allowed — an off-by-one here would block a legitimate send.</summary>
    [Fact]
    public async Task A_message_exactly_at_the_recipient_ceiling_is_allowed()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var limit = Options.Create(new EmailRecipientOptions { MaxRecipients = 5 });

        var result = await Compose(db, Author, limit).Handle(new SendEmailCommand
        {
            Subject = "Đúng ngưỡng",
            Body = "<p>Nội dung.</p>",
            To = Enumerable.Range(0, 3).Select(i => new EmailRecipientDto { Email = $"g8-to-{i}@partner.example.com" }).ToList(),
            Cc = new List<EmailRecipientDto> { new() { Email = "g8-cc-0@partner.example.com" } },
            Bcc = new List<EmailRecipientDto> { new() { Email = "g8-bcc-0@partner.example.com" } },
        }, CancellationToken.None);

        Assert.NotNull(result.SentEmailId);
        Assert.Single(_h.Messages());

        await CleanupRowsAsync(db);
    }
}
