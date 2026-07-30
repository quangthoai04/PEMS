using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Batch 6 — <c>VISIT_DEPARTMENT_STAFF_ASSIGNMENT</c>, the message a Department Leader's own staff
/// receives when they are put on a visit.
///
/// <para>
/// It is deliberately NOT the invitation template, and the difference is the whole point: an invitation
/// asks, an assignment tells you and says on whose authority. Before this batch every assignment was
/// recorded against <c>VISIT_PARTICIPANT_INVITATION</c>, so the email history could not tell the two
/// apart and editing the assignment template changed nothing.
/// </para>
/// </summary>
public sealed class DepartmentStaffAssignmentEndToEndTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("batch6-evidence@partner.example.com");
    private static readonly HtmlSanitizerService Sanitizer = new();

    public void Dispose() => _h.Dispose();

    private const string AcceptUrl = "https://pems.test/api/public/email-actions/RAW-ASSIGN-ACCEPT";
    private const string DeclineUrl = "https://pems.test/api/public/email-actions/RAW-ASSIGN-DECLINE";

    private SystemEmailRequest Assignment(SystemEmailContent? content = null) => new(
        SystemEmailTemplates.VisitDepartmentStaffAssignment,
        new EmailRecipient(_h.Marker, "Lê Thị Mai"),
        new Dictionary<string, string>
        {
            ["recipientName"] = "Lê Thị Mai",
            ["delegationName"] = "Đoàn Đại học Kyoto",
            ["campusName"] = "FPT Đà Nẵng",
            ["plannedTime"] = "09:00 12/08/2026 - 11:30 12/08/2026",
            ["departmentName"] = "Phòng Hành chính",
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] = EmailComposition.AcceptDeclineBlock(AcceptUrl, DeclineUrl),
        },
        RelatedType: "VisitParticipant",
        RelatedId: 880201)
    {
        Content = content ?? SystemEmailContent.FromTemplate.Instance,
    };

    [Fact]
    public async Task The_assignment_names_the_department_that_assigned_it()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var row = await db.EmailTemplates.AsNoTracking()
                .SingleAsync(t => t.TemplateCode == SystemEmailTemplates.VisitDepartmentStaffAssignment);

            var result = await _h.Dispatcher(db).SendAsync(Assignment());
            Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);

            var eml = _h.OnlyMessage();
            Assert.Contains(EmlMessage.LiteralPrefix(row.SubjectVi), eml.DecodedHeader("Subject"));

            var body = eml.Body;
            // "You were assigned BY somebody" — without the department this reads like an invitation
            // from a stranger.
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Phòng Hành chính"), body);
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Đoàn Đại học Kyoto"), body);
            Assert.Contains(AcceptUrl, body);
            Assert.Contains(DeclineUrl, body);
            Assert.DoesNotContain("{{", body);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task It_is_recorded_against_the_assignment_template_not_the_invitation_one()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var assignmentTemplateId = await db.EmailTemplates.AsNoTracking()
                .Where(t => t.TemplateCode == SystemEmailTemplates.VisitDepartmentStaffAssignment)
                .Select(t => t.EmailTemplateId).SingleAsync();
            var invitationTemplateId = await db.EmailTemplates.AsNoTracking()
                .Where(t => t.TemplateCode == SystemEmailTemplates.VisitParticipantInvitation)
                .Select(t => t.EmailTemplateId).SingleAsync();

            var result = await _h.Dispatcher(db).SendAsync(Assignment());

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            Assert.Equal(assignmentTemplateId, stored.EmailTemplateId);
            Assert.NotEqual(invitationTemplateId, stored.EmailTemplateId);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task The_stored_copy_keeps_the_message_and_drops_the_response_links()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db).SendAsync(Assignment());

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            Assert.Equal(
                HistoryBodyPolicy.ActionBlockStripped,
                SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitDepartmentStaffAssignment));

            Assert.NotNull(stored.BodySnapshot);
            Assert.DoesNotContain("RAW-ASSIGN", stored.BodySnapshot!);
            Assert.DoesNotContain("email-actions", stored.BodySnapshot!);
            // …while what the person was actually told survives.
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Phòng Hành chính"), stored.BodySnapshot!);

            var recipient = Assert.Single(await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync());
            Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task It_goes_to_exactly_one_person_with_no_copies()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await _h.Dispatcher(db).SendAsync(Assignment());

            var eml = _h.OnlyMessage();
            Assert.Equal(1, eml.AddressCount("To"));
            Assert.Equal(string.Empty, eml.Header("Cc"));
            Assert.Equal(string.Empty, eml.Header("Bcc"));
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_leader_edit_replaces_the_words_and_keeps_the_response_buttons()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var authored = SystemEmailContent.AuthoredByUser.Create(
                "Mai hỗ trợ đoàn Kyoto giúp anh nhé",
                "<p>Em phụ trách phần đón tiếp buổi sáng, 8h30 có mặt ở sảnh.</p>",
                Sanitizer);

            await _h.Dispatcher(db).SendAsync(Assignment(authored));

            var body = _h.OnlyMessage().Body;
            Assert.Contains("8h30", body);
            Assert.DoesNotContain(System.Net.WebUtility.HtmlEncode("Phòng Hành chính"), body);
            // The Leader rewrote the message; the system still supplies the accept/decline links.
            Assert.Contains(AcceptUrl, body);
            Assert.Contains(DeclineUrl, body);
        }
        finally { await _h.CleanupAsync(); }
    }
}
