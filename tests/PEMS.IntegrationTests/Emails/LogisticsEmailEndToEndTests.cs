using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Batch 7 — the four logistics messages, on real MIME and a real database.
///
/// <para>
/// Three of them carry one-time accept/decline links and are therefore sent to exactly one person and
/// stored with the links removed. The fourth — the expense reminder — carries only a login-required
/// detail link, grants nothing on its own, and is stored in full. That difference is not configured
/// anywhere: it falls out of the template's own classification, and these tests are what prove the two
/// stay in step.
/// </para>
/// </summary>
public sealed class LogisticsEmailEndToEndTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("batch7-evidence@partner.example.com");
    private static readonly HtmlSanitizerService Sanitizer = new();

    public void Dispose() => _h.Dispose();

    private const string AcceptUrl = "https://pems.test/api/public/email-actions/RAW-LOG-ACCEPT";
    private const string DeclineUrl = "https://pems.test/api/public/email-actions/RAW-LOG-DECLINE";
    private const string DetailUrl = "https://pems.test/dashboard/logistics/5501";

    // ── C-19 ────────────────────────────────────────────────────────────────

    private SystemEmailRequest Request(SystemEmailContent? content = null) => new(
        SystemEmailTemplates.LogisticsRequestToDepartment,
        new EmailRecipient(_h.Marker, "Phạm Thị Trưởng Phòng"),
        new Dictionary<string, string>
        {
            ["departmentLeaderName"] = "Phạm Thị Trưởng Phòng",
            ["requesterName"] = "Trần Thị Hà",
            ["logisticsTitle"] = "Màn LED sảnh A",
            ["logisticsItemType"] = "LED",
            ["quantity"] = "2",
            ["usageStartAt"] = "08:00 12/08/2026",
            ["usageEndAt"] = "12:00 12/08/2026",
            ["dueAt"] = "17:00 10/08/2026",
            ["coordinationNote"] = "Cần bật từ 7h30, nội dung do IC gửi sau.",
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] =
                EmailComposition.LogisticsActionBlock(AcceptUrl, DeclineUrl, DetailUrl),
        },
        RelatedType: "LogisticsItem",
        RelatedId: 5501)
    {
        Content = content ?? SystemEmailContent.FromTemplate.Instance,
    };

    // ── C-20 ────────────────────────────────────────────────────────────────

    private SystemEmailRequest Assignment() => new(
        SystemEmailTemplates.LogisticsAssigneeAssignment,
        new EmailRecipient(_h.Marker, "Lê Văn Nhân Sự"),
        new Dictionary<string, string>
        {
            ["assigneeName"] = "Lê Văn Nhân Sự",
            ["logisticsTitle"] = "Màn LED sảnh A",
            ["dueAt"] = "17:00 10/08/2026",
            ["campusName"] = "FPT Đà Nẵng",
            ["delegationName"] = "Đoàn Đại học Kyoto",
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] =
                EmailComposition.LogisticsAssigneeActionBlock(AcceptUrl, DeclineUrl, DetailUrl),
        },
        RelatedType: "LogisticsItem",
        RelatedId: 5502);

    // ── C-21 ────────────────────────────────────────────────────────────────

    private SystemEmailRequest Proposal() => new(
        SystemEmailTemplates.LogisticsChangeProposalToHost,
        new EmailRecipient(_h.Marker, "Trần Thị Hà"),
        new Dictionary<string, string>
        {
            ["hostName"] = "Trần Thị Hà",
            ["logisticsTitle"] = "Màn LED sảnh A",
            ["departmentName"] = "Phòng Hành chính",
            ["delegationName"] = "Đoàn Đại học Quốc gia",
            // The counter-offer itself, not just the reason for it.
            ["originalQuantity"] = "2",
            ["proposedQuantity"] = "1",
            ["proposedUsageStartAt"] = "08:00 01/08/2026",
            ["proposedUsageEndAt"] = "12:00 01/08/2026",
            ["proposedDescription"] = "Không đổi",
            ["proposalNote"] = "Kho chỉ còn 1 màn, xin giảm số lượng.",
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] =
                EmailComposition.LogisticsProposalActionBlock(AcceptUrl, DeclineUrl, DetailUrl),
        },
        RelatedType: "LogisticsItem",
        RelatedId: 5503);

    // ── C-22 ────────────────────────────────────────────────────────────────

    private SystemEmailRequest Reminder() => new(
        SystemEmailTemplates.LogisticsExpenseReportReminder,
        new EmailRecipient(_h.Marker, "Lê Văn Nhân Sự"),
        new Dictionary<string, string>
        {
            ["recipientName"] = "Lê Văn Nhân Sự",
            ["itemTitle"] = "Màn LED sảnh A",
            ["dueAt"] = "17:00 20/08/2026",
            ["delegationName"] = "Đoàn Đại học Kyoto",
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] =
                EmailComposition.DetailLinkBlock(DetailUrl, "Mở biên bản để kê khai chi phí"),
        },
        RelatedType: "LogisticsItem",
        RelatedId: 5504);

    // ── Each message is the template's, and reaches one person ──────────────

    [Fact]
    public async Task The_request_carries_every_detail_the_department_needs_to_decide()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var row = await db.EmailTemplates.AsNoTracking()
                .SingleAsync(t => t.TemplateCode == SystemEmailTemplates.LogisticsRequestToDepartment);

            var result = await _h.Dispatcher(db).SendAsync(Request());
            Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);

            var eml = _h.OnlyMessage();
            Assert.Contains(EmlMessage.LiteralPrefix(row.SubjectVi), eml.DecodedHeader("Subject"));

            var body = eml.Body;
            var encoded = (string s) => System.Net.WebUtility.HtmlEncode(s);
            Assert.Contains(encoded("Màn LED sảnh A"), body);
            Assert.Contains("LED", body);
            Assert.Contains("08:00 12/08/2026", body);
            Assert.Contains("17:00 10/08/2026", body);
            // The coordination note is the Host's own words, not a renderer's stand-in.
            Assert.Contains(encoded("Cần bật từ 7h30"), body);
            Assert.DoesNotContain("Chưa có thông tin", body);
            Assert.DoesNotContain("{{", body);

            // Three buttons, all three URLs, one canonical block.
            Assert.Contains(AcceptUrl, body);
            Assert.Contains(DeclineUrl, body);
            Assert.Contains(DetailUrl, body);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Theory]
    [InlineData(SystemEmailTemplates.LogisticsRequestToDepartment)]
    [InlineData(SystemEmailTemplates.LogisticsAssigneeAssignment)]
    [InlineData(SystemEmailTemplates.LogisticsChangeProposalToHost)]
    public async Task A_token_bearing_logistics_email_goes_to_one_person_with_no_copies(string templateCode)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await _h.Dispatcher(db).SendAsync(RequestFor(templateCode));

            var eml = _h.OnlyMessage();
            Assert.Equal(1, eml.AddressCount("To"));
            Assert.Equal(string.Empty, eml.Header("Cc"));
            Assert.Equal(string.Empty, eml.Header("Bcc"));
            Assert.Contains(AcceptUrl, eml.Body);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Theory]
    [InlineData(SystemEmailTemplates.LogisticsRequestToDepartment)]
    [InlineData(SystemEmailTemplates.LogisticsAssigneeAssignment)]
    [InlineData(SystemEmailTemplates.LogisticsChangeProposalToHost)]
    public async Task A_token_bearing_logistics_email_is_stored_without_its_links(string templateCode)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var templateId = await db.EmailTemplates.AsNoTracking()
                .Where(t => t.TemplateCode == templateCode)
                .Select(t => t.EmailTemplateId).SingleAsync();

            var result = await _h.Dispatcher(db).SendAsync(RequestFor(templateCode));

            Assert.Equal(
                HistoryBodyPolicy.ActionBlockStripped, SensitiveEmailHistory.PolicyFor(templateCode));

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            // The row says which template it is — for the proposal that is new, it used to be NULL.
            Assert.Equal(templateId, stored.EmailTemplateId);
            Assert.Equal("LogisticsItem", stored.RelatedType);
            Assert.Equal("SENT", stored.Status);
            Assert.Null(stored.DeliveredAt);

            Assert.NotNull(stored.BodySnapshot);
            Assert.DoesNotContain("RAW-LOG", stored.BodySnapshot!);
            Assert.DoesNotContain("email-actions", stored.BodySnapshot!);
            Assert.DoesNotContain(DetailUrl, stored.BodySnapshot!);
            Assert.DoesNotContain("PEMS_ACTION_BLOCK", stored.BodySnapshot!);
            // …while the message itself is still readable.
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Màn LED sảnh A"), stored.BodySnapshot!);

            var recipient = Assert.Single(await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync());
            Assert.Equal(_h.Marker, recipient.RecipientEmail);
            Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── The reminder is the odd one out, deliberately ───────────────────────

    [Fact]
    public async Task The_expense_reminder_keeps_its_whole_body_because_its_link_grants_nothing()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var templateId = await db.EmailTemplates.AsNoTracking()
                .Where(t => t.TemplateCode == SystemEmailTemplates.LogisticsExpenseReportReminder)
                .Select(t => t.EmailTemplateId).SingleAsync();

            var result = await _h.Dispatcher(db).SendAsync(Reminder());

            Assert.Equal(
                HistoryBodyPolicy.Full,
                SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.LogisticsExpenseReportReminder));

            var body = _h.OnlyMessage().Body;
            Assert.Contains(DetailUrl, body);
            Assert.DoesNotContain("email-actions", body);   // no one-time token anywhere

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            Assert.Equal(templateId, stored.EmailTemplateId);
            Assert.NotNull(stored.BodySnapshot);
            // Kept in full — a login-required link is not a credential, and an operator investigating a
            // dispute should see exactly what was sent.
            Assert.Contains(DetailUrl, stored.BodySnapshot!);
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Màn LED sảnh A"), stored.BodySnapshot!);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── No fallback: a broken template fails loudly ─────────────────────────

    [Theory]
    [InlineData(SystemEmailTemplates.LogisticsRequestToDepartment)]
    [InlineData(SystemEmailTemplates.LogisticsAssigneeAssignment)]
    public async Task An_inactive_template_stops_the_send_instead_of_falling_back(string templateCode)
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
                        () => _h.Dispatcher(db).SendAsync(RequestFor(templateCode)));

                    Assert.Equal(EmailErrorCodes.TemplateInactive, ex.ErrorCode);
                    // The old code would have sent a body written in C#. Nothing goes out now.
                    Assert.Empty(_h.Messages());

                    using var verify = EmailEvidenceHarness.NewContext();
                    Assert.False(await verify.SentEmailRecipients.AsNoTracking()
                        .AnyAsync(r => r.RecipientEmail == _h.Marker));
                });
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_missing_variable_fails_closed_rather_than_printing_a_stand_in()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var incomplete = Request().Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
            incomplete.Remove("coordinationNote");

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(Request() with { Variables = incomplete }));

            Assert.Equal(EmailErrorCodes.TemplateVariableMissing, ex.ErrorCode);
            Assert.Contains("coordinationNote", ex.Message);
            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task An_unexpected_variable_fails_closed_too()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var extra = Request().Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
            // The spelling the old caller used — it silently did nothing before.
            extra["itemType"] = "LED";

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(Request() with { Variables = extra }));

            Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, ex.ErrorCode);
            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task Editing_the_template_changes_the_very_next_message_without_a_restart()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await _h.Dispatcher(db).SendAsync(Request());
            var before = _h.OnlyMessage().DecodedHeader("Subject");
            _h.ClearMessages();

            await EmailEvidenceHarness.WithTemplateAsync(
                db, SystemEmailTemplates.LogisticsRequestToDepartment,
                t => t.SubjectVi = "[PEMS] Yêu cầu hậu cần — {{logisticsTitle}} (bản mới)",
                async () =>
                {
                    await _h.Dispatcher(db).SendAsync(Request());
                    var after = _h.OnlyMessage().DecodedHeader("Subject");

                    Assert.NotEqual(before, after);
                    Assert.Contains("bản mới", after);
                });
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── Security ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_subject_edited_to_include_the_action_block_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await EmailEvidenceHarness.WithTemplateAsync(
                db, SystemEmailTemplates.LogisticsRequestToDepartment,
                t => t.SubjectVi = "[PEMS] Yêu cầu: {{actionBlock}}",
                async () =>
                {
                    var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                        () => _h.Dispatcher(db).SendAsync(Request()));

                    Assert.Equal(EmailErrorCodes.TemplateSensitiveInSubject, ex.ErrorCode);
                    Assert.DoesNotContain(AcceptUrl, ex.Message);
                    Assert.Empty(_h.Messages());
                });
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_host_edited_subject_carrying_the_one_time_link_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var authored = SystemEmailContent.AuthoredByUser.Create(
                "Bấm vào đây: " + AcceptUrl, "<p>Nhờ phòng hỗ trợ.</p>", Sanitizer);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(Request(authored)));

            Assert.Equal(EmailErrorCodes.SubjectSecretLeak, ex.ErrorCode);
            Assert.DoesNotContain(AcceptUrl, ex.Message);
            Assert.DoesNotContain("RAW-LOG", ex.Message);
            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_host_edit_keeps_the_backend_buttons_and_still_hides_them_from_history()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var authored = SystemEmailContent.AuthoredByUser.Create(
                "Nhờ phòng hỗ trợ màn LED sảnh A",
                "<p>Nhờ phòng chuẩn bị giúp màn LED sảnh A trước 7h30 sáng thứ Tư.</p>",
                Sanitizer);

            var result = await _h.Dispatcher(db).SendAsync(Request(authored));

            var body = _h.OnlyMessage().Body;
            Assert.Contains("7h30", body);
            Assert.Contains(AcceptUrl, body);
            Assert.Contains(DeclineUrl, body);

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);
            Assert.DoesNotContain("RAW-LOG", stored.BodySnapshot ?? string.Empty);
            Assert.Contains("7h30", stored.BodySnapshot!);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_provider_failure_records_the_failure_and_still_hides_the_links()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db, brokenHost: "127.0.0.1").SendAsync(Assignment());

            Assert.Equal(EmailDeliveryStatus.Failed, result.Delivery.Status);

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);
            Assert.Equal("FAILED", stored.Status);
            Assert.Null(stored.SentAt);
            Assert.DoesNotContain("RAW-LOG", stored.BodySnapshot ?? string.Empty);
        }
        finally { await _h.CleanupAsync(); }
    }

    private SystemEmailRequest RequestFor(string templateCode) => templateCode switch
    {
        SystemEmailTemplates.LogisticsAssigneeAssignment => Assignment(),
        SystemEmailTemplates.LogisticsChangeProposalToHost => Proposal(),
        _ => Request(),
    };
}
