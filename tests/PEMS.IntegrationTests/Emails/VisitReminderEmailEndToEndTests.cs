using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Batch 8 — the two scheduled reminders, on real MIME and a real database.
///
/// <para>
/// These are the only visit emails that are NOT sensitive: their button is a login-required link, so
/// there is no credential in the message and the whole body is worth keeping in the history. The
/// classification decides that on its own — these tests are what prove the retention policy and the
/// registry stay in step, and that the reminder never mints a one-time token.
/// </para>
/// </summary>
public sealed class VisitReminderEmailEndToEndTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("batch8-evidence@partner.example.com");

    public void Dispose() => _h.Dispose();

    private const string DetailUrl = "https://pems.test/dashboard/visit/process/9001/9101";

    private static Dictionary<string, string> HostVariables() => new()
    {
        ["hostName"] = "Trần Thị Hà",
        ["delegationName"] = "Đoàn Đại học Kyoto",
        ["campusName"] = "FPT Đà Nẵng",
        ["plannedStart"] = "09:00 12/08/2026",
        ["plannedEnd"] = "11:30 12/08/2026",
    };

    private static Dictionary<string, string> ParticipantVariables(string name = "Lê Văn Nhân Sự") => new()
    {
        ["recipientName"] = name,
        ["delegationName"] = "Đoàn Đại học Kyoto",
        ["campusName"] = "FPT Đà Nẵng",
        ["plannedStart"] = "09:00 12/08/2026",
        ["plannedEnd"] = "11:30 12/08/2026",
    };

    private static Dictionary<string, string> DetailBlock() => new()
    {
        [EmailTrustedBlocks.ActionBlock] = EmailComposition.VisitDetailBlock(DetailUrl),
    };

    private SystemEmailRequest HostReminder() => new(
        SystemEmailTemplates.VisitReminderHost,
        new EmailRecipient(_h.Marker, "Trần Thị Hà"),
        HostVariables(),
        TrustedBlocks: DetailBlock(),
        RelatedType: "VISIT_INSTANCE",
        RelatedId: 9101);

    private SystemEmailRequest ParticipantReminder(string name = "Lê Văn Nhân Sự") => new(
        SystemEmailTemplates.VisitReminderParticipants,
        new EmailRecipient(_h.Marker, name),
        ParticipantVariables(name),
        TrustedBlocks: DetailBlock(),
        RelatedType: "VISIT_INSTANCE",
        RelatedId: 9101);

    // ── What each reminder actually says ────────────────────────────────────

    [Fact]
    public async Task The_host_reminder_states_both_ends_of_the_window()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var row = await db.EmailTemplates.AsNoTracking()
                .SingleAsync(t => t.TemplateCode == SystemEmailTemplates.VisitReminderHost);

            var result = await _h.Dispatcher(db).SendAsync(HostReminder());
            Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);

            var eml = _h.OnlyMessage();
            Assert.Contains(EmlMessage.LiteralPrefix(row.SubjectVi), eml.DecodedHeader("Subject"));

            var body = eml.Body;
            Assert.Contains("09:00 12/08/2026", body);
            // The end time used to reach recipients as the literal text "{{plannedEnd}}".
            Assert.Contains("11:30 12/08/2026", body);
            Assert.Contains(DetailUrl, body);
            Assert.DoesNotContain("{{", body);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Theory]
    [InlineData(SystemEmailTemplates.VisitReminderHost)]
    [InlineData(SystemEmailTemplates.VisitReminderParticipants)]
    public async Task A_reminder_goes_to_one_person_with_no_copies_and_no_token(string templateCode)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var before = await db.EmailActionTokens.AsNoTracking().CountAsync();

            await _h.Dispatcher(db).SendAsync(
                templateCode == SystemEmailTemplates.VisitReminderHost
                    ? HostReminder() : ParticipantReminder());

            var eml = _h.OnlyMessage();
            Assert.Equal(1, eml.AddressCount("To"));
            Assert.Equal(string.Empty, eml.Header("Cc"));
            Assert.Equal(string.Empty, eml.Header("Bcc"));

            var body = eml.Body;
            Assert.Contains(DetailUrl, body);
            // A reminder asks somebody to look at something they already have access to. There is
            // nothing for a one-time token to grant, so none is minted.
            Assert.DoesNotContain("email-actions", body);

            using var verify = EmailEvidenceHarness.NewContext();
            Assert.Equal(before, await verify.EmailActionTokens.AsNoTracking().CountAsync());
        }
        finally { await _h.CleanupAsync(); }
    }

    [Theory]
    [InlineData(SystemEmailTemplates.VisitReminderHost)]
    [InlineData(SystemEmailTemplates.VisitReminderParticipants)]
    public async Task A_reminder_is_recorded_in_full_against_its_own_template(string templateCode)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var templateId = await db.EmailTemplates.AsNoTracking()
                .Where(t => t.TemplateCode == templateCode)
                .Select(t => t.EmailTemplateId).SingleAsync();

            var result = await _h.Dispatcher(db).SendAsync(
                templateCode == SystemEmailTemplates.VisitReminderHost
                    ? HostReminder() : ParticipantReminder());

            Assert.Equal(HistoryBodyPolicy.Full, SensitiveEmailHistory.PolicyFor(templateCode));

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            Assert.Equal(templateId, stored.EmailTemplateId);
            Assert.Equal("VISIT_INSTANCE", stored.RelatedType);
            Assert.Equal(9101ul, stored.RelatedId);
            Assert.Equal("SENT", stored.Status);
            Assert.Null(stored.DeliveredAt);
            Assert.Equal(0u, stored.RetryCount);

            // Kept whole, detail link and all — nothing here is a credential.
            Assert.NotNull(stored.BodySnapshot);
            Assert.Contains(DetailUrl, stored.BodySnapshot!);
            Assert.Contains("11:30 12/08/2026", stored.BodySnapshot!);
            Assert.Contains("PEMS_ACTION_BLOCK", stored.BodySnapshot!);

            var recipient = Assert.Single(await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync());
            Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task The_stored_copy_is_the_message_that_was_sent()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db).SendAsync(ParticipantReminder());

            var delivered = _h.OnlyMessage().Body;

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            // Same sentences, same link, same button — an operator reading the history sees what the
            // recipient saw, which is the whole reason to keep a Full snapshot.
            foreach (var marker in new[] { DetailUrl, "09:00 12/08/2026", "11:30 12/08/2026" })
            {
                Assert.Contains(marker, delivered);
                Assert.Contains(marker, stored.BodySnapshot!);
            }
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_participants_reminder_names_only_its_own_recipient()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await _h.Dispatcher(db).SendAsync(ParticipantReminder("Lê Văn Nhân Sự"));

            var body = _h.OnlyMessage().Body;
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Lê Văn Nhân Sự"), body);
            // Nobody else's name, address, or a count of who else was invited.
            Assert.DoesNotContain(System.Net.WebUtility.HtmlEncode("Phạm Thị"), body);
            Assert.DoesNotContain("@fpt.edu.vn", body);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── No fallback ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SystemEmailTemplates.VisitReminderHost)]
    [InlineData(SystemEmailTemplates.VisitReminderParticipants)]
    public async Task An_inactive_reminder_template_stops_the_send(string templateCode)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await EmailEvidenceHarness.WithTemplateAsync(
                db, templateCode,
                t => t.Status = "INACTIVE",
                async () =>
                {
                    var ex = await Assert.ThrowsAsync<ConflictException>(
                        () => _h.Dispatcher(db).SendAsync(
                            templateCode == SystemEmailTemplates.VisitReminderHost
                                ? HostReminder() : ParticipantReminder()));

                    Assert.Equal(EmailErrorCodes.TemplateInactive, ex.ErrorCode);
                    // The old job fell back to the OTHER reminder template, then to the other language,
                    // then to a hard-coded subject. None of those paths exist now.
                    Assert.Empty(_h.Messages());
                });
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_reminder_missing_planned_end_fails_closed()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var incomplete = HostVariables();
            incomplete.Remove("plannedEnd");

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(HostReminder() with { Variables = incomplete }));

            Assert.Equal(EmailErrorCodes.TemplateVariableMissing, ex.ErrorCode);
            Assert.Contains("plannedEnd", ex.Message);
            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    [Theory]
    [InlineData("plannedStartAt")]   // the name the old job actually passed
    [InlineData("detailUrl")]        // the detail URL is a trusted block, never a variable
    public async Task A_variable_the_template_does_not_declare_fails_closed(string unexpected)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var extra = HostVariables();
            extra[unexpected] = "x";

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(HostReminder() with { Variables = extra }));

            Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, ex.ErrorCode);
            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task Editing_the_reminder_template_changes_the_next_message_without_a_restart()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await _h.Dispatcher(db).SendAsync(HostReminder());
            var before = _h.OnlyMessage().DecodedHeader("Subject");
            _h.ClearMessages();

            await EmailEvidenceHarness.WithTemplateAsync(
                db, SystemEmailTemplates.VisitReminderHost,
                t => t.SubjectVi = "[PEMS] Nhắc lịch — {{delegationName}} (bản mới)",
                async () =>
                {
                    await _h.Dispatcher(db).SendAsync(HostReminder());
                    var after = _h.OnlyMessage().DecodedHeader("Subject");

                    Assert.NotEqual(before, after);
                    Assert.Contains("bản mới", after);
                });
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_provider_failure_is_recorded_as_failed_and_never_as_sent()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db, brokenHost: "127.0.0.1").SendAsync(HostReminder());

            Assert.Equal(EmailDeliveryStatus.Failed, result.Delivery.Status);

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);
            Assert.Equal("FAILED", stored.Status);
            Assert.Null(stored.SentAt);
            Assert.Null(stored.DeliveredAt);
            // The first attempt is not a retry.
            Assert.Equal(0u, stored.RetryCount);
        }
        finally { await _h.CleanupAsync(); }
    }
}
