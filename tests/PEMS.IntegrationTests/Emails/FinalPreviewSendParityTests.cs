using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Commands.BuildFinalEmailPreview;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Preview;
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The promise the whole sender-variable change rests on: <b>what was approved in the Final Preview is
/// what arrives</b>.
///
/// <para>
/// Every other suite proves a piece — the token verifies, the renderer substitutes, the dispatcher
/// records. None of them closed the loop, because closing it means comparing the HTML a person looked at
/// against the bytes of a delivered message, and until now nothing read both. These tests go
/// VIEW → EDIT → FINAL_PREVIEW → send and then open the <c>.eml</c> off disk.
/// </para>
/// <para>
/// Why this matters more than it sounds: the sender may now EDIT the substituted text as ordinary prose.
/// The moment that is true, "the preview is representative" stops being good enough — the preview is the
/// only record of what the person consented to send, and a send that re-renders from the template would
/// quietly discard their words while still reporting success.
/// </para>
/// </summary>
public sealed class FinalPreviewSendParityTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("final-parity@partner.example.com");
    private static readonly HtmlSanitizerService Sanitizer = new();

    public void Dispose()
    {
        _h.Dispose();
        try { if (System.IO.Directory.Exists(_storageRoot)) System.IO.Directory.Delete(_storageRoot, recursive: true); }
        catch (System.IO.IOException) { /* a leaked temp dir must never fail a run */ }
    }

    /// <summary>An editable template: its capability permits both sender variables and a runtime edit.</summary>
    private const string Template = SystemEmailTemplates.VisitParticipantInvitation;

    private const string AcceptUrl = "https://pems.test/api/public/email-actions/RAW-PARITY-ACCEPT";
    private const string DeclineUrl = "https://pems.test/api/public/email-actions/RAW-PARITY-DECLINE";

    /// <summary>The scope this message belongs to — the same string the send recomputes and compares.</summary>
    private static string Scope() => EmailPreviewFingerprint.Scope(
        ("visitInstance", 991_501UL), ("participant", 991_502UL));

    private sealed class Actor : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => 3;
        public string? Email => "parity-actor@fpt.edu.vn";
        public ulong? RoleId => null;
        public string? RoleCode => "HO";
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? SessionId => null;
        public ulong? DepartmentId => null;
        public string? LoginPortal => null;
    }

    private static readonly Actor Sender = new();

    private readonly string _storageRoot =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pems-parity-files-" + Guid.NewGuid().ToString("N"));

    private sealed class NoHttpClients : System.Net.Http.IHttpClientFactory
    {
        public System.Net.Http.HttpClient CreateClient(string name) => new();
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private PEMS.Infrastructure.FileStorage.LocalFileStorageService Storage() => new(
        new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })
            .Build(),
        new NoHttpClients(), new NoServices(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<PEMS.Infrastructure.FileStorage.LocalFileStorageService>.Instance);

    /// <summary>No inline images in these messages, but the pipeline requires a real one all the same.</summary>
    private PEMS.Application.Emails.Utils.EmailImageLayoutNormalizer Normalizer(ApplicationDbContext db)
        => new(db, Storage());

    // ── Rig: the three stages, wired the way the container wires them ───────────────────────────

    private static PreviewEmailTemplateQueryHandler Prepare(ApplicationDbContext db)
        => new(db, Sender, new EmailTemplateRenderer(db),
               EmailEvidenceHarness.Senders(db), EmailEvidenceHarness.PreviewTokens());

    private BuildFinalEmailPreviewCommandHandler Finalise(ApplicationDbContext db)
        => new(db, Sender, Sanitizer, Normalizer(db), EmailEvidenceHarness.PreviewTokens());

    private static Dictionary<string, string> Variables() => new()
    {
        ["recipientName"] = "Nguyễn Văn Bình",
        ["delegationName"] = "Đoàn Đại học Kyoto",
        ["campusName"] = "FPT Đà Nẵng",
        ["plannedTime"] = "09:00 12/08/2026 - 11:30 12/08/2026",
        ["hostName"] = "Trần Thị Hà",
        ["roleLabel"] = "Staff hỗ trợ IC",
        ["hostMessage"] = "Nhờ anh hỗ trợ phần đón tiếp.",
    };

    private static PreviewEmailTemplateQuery ViewQuery() =>
        new(Template, Variables(), EmailLanguages.Vi) { ScopeKey = Scope() };

    /// <summary>Sends the approved content through the dispatcher, exactly as a call site does.</summary>
    private async Task<SystemEmailDispatchResult> SendApprovedAsync(
        ApplicationDbContext db, string subject, string bodyText, string finalToken)
    {
        var resolver = new ApprovedEmailContentResolver(
            db, Sender, Sanitizer, Normalizer(db), EmailEvidenceHarness.PreviewTokens());

        var approved = new ApprovedEmailContent(finalToken, subject, BodyText: bodyText);
        var content = await resolver.ResolveAsync(approved, Template, Scope(), CancellationToken.None);

        return await _h.Dispatcher(db).SendAsync(new SystemEmailRequest(
            Template,
            new EmailRecipient(_h.Marker, "Nguyễn Văn Bình"),
            Variables(),
            TrustedBlocks: new Dictionary<string, string>
            {
                [EmailTrustedBlocks.ActionBlock] = EmailComposition.AcceptDeclineBlock(AcceptUrl, DeclineUrl),
            },
            RelatedType: "VisitParticipant",
            RelatedId: 991_502)
        {
            Content = content,
            SentBy = Sender.UserId,
        });
    }

    // ── The round trip ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The sender's own words survive VIEW → EDIT → FINAL_PREVIEW → send and arrive in the delivered
    /// message, and the Reply-To the preview showed is the one on the envelope.
    /// </summary>
    [Fact]
    public async Task What_the_sender_approved_is_what_the_recipient_receives()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            // VIEW — the fully substituted message, read-only, plus the token that scopes the edit.
            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);

            Assert.True(view.RuntimeEditable, $"{Template} must be editable for this test to mean anything.");
            Assert.False(string.IsNullOrWhiteSpace(view.PreviewToken));
            // The sender's details were substituted BEFORE the sender ever saw the text — which is what
            // makes the next step ordinary prose editing rather than template authoring.
            Assert.DoesNotContain("{{", view.BodyHtml);

            // EDIT — the person rewrites it in their own words, including over substituted text.
            const string Edited =
                "Kính gửi anh Bình,\n\n" +
                "Nhờ anh hỗ trợ đón đoàn Đại học Kyoto tại FPT Đà Nẵng sáng 12/08.\n" +
                "Có gì anh trả lời thẳng email này giúp em.\n\n" +
                "Trần Thị Hà — Phòng Hợp tác Quốc tế";
            const string EditedSubject = "Nhờ anh hỗ trợ đón đoàn Kyoto 12/08";

            // FINAL_PREVIEW — the exact message, signed.
            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = EditedSubject,
                    EditableBodyText = Edited,
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            Assert.False(string.IsNullOrWhiteSpace(final.FinalPreviewToken));
            Assert.Equal(EditedSubject, final.Subject);
            Assert.Contains("Nhờ anh hỗ trợ đón đoàn Đại học Kyoto", final.FinalPreviewHtml);
            // The preview shows the buttons but never a live credential.
            Assert.DoesNotContain("RAW-PARITY-ACCEPT", final.FinalPreviewHtml);

            // SEND — with the approval, not with the template.
            var sent = await SendApprovedAsync(db, EditedSubject, Edited, final.FinalPreviewToken);
            Assert.Equal(EmailDeliveryStatus.Sent, sent.Delivery.Status);

            // …and what actually left the building.
            var eml = _h.OnlyMessage();

            Assert.Contains(EmlMessage.LiteralPrefix(EditedSubject), eml.DecodedHeader("Subject"));

            var delivered = eml.DecodedTextParts;
            foreach (var line in new[]
                     {
                         "Kính gửi anh Bình",
                         "Nhờ anh hỗ trợ đón đoàn Đại học Kyoto tại FPT Đà Nẵng sáng 12/08",
                         "Có gì anh trả lời thẳng email này giúp em",
                         "Trần Thị Hà — Phòng Hợp tác Quốc tế",
                     })
                Assert.Contains(line, delivered);

            // The template's own wording is NOT what went out — the edit replaced it rather than
            // being appended to it.
            Assert.DoesNotContain("Nhờ anh hỗ trợ phần đón tiếp.", delivered);

            // The locked block survived the edit, with its REAL token this time.
            Assert.Contains(AcceptUrl, delivered);
            Assert.Contains(DeclineUrl, delivered);

            // Reply-To: what the preview promised is what the envelope carries.
            var replyTo = eml.Header("Reply-To");
            if (!string.IsNullOrWhiteSpace(final.ReplyToEmail))
                Assert.Contains(final.ReplyToEmail!, replyTo);
            else
                Assert.True(string.IsNullOrWhiteSpace(replyTo),
                    $"The preview showed no Reply-To, but the message carries '{replyTo}'.");
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── What the token refuses ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Changing one character after approval is refused. This is the assertion that makes the round-trip
    /// test above mean something: without it, a send that ignored the approved content entirely would
    /// still pass, because it would happen to render similar text.
    /// </summary>
    [Fact]
    public async Task Content_altered_after_approval_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);
            const string Approved = "Nội dung đã được duyệt.";

            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = "Chủ đề đã duyệt",
                    EditableBodyText = Approved,
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => SendApprovedAsync(
                db, "Chủ đề đã duyệt", Approved + " Và một câu chưa ai duyệt.", final.FinalPreviewToken));

            Assert.True(ex is BusinessRuleException or ValidationException or ForbiddenException,
                $"A tampered body must be refused by the approval check, but the failure was {ex.GetType().Name}: {ex.Message}");

            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>A subject changed after approval is refused for the same reason the body is.</summary>
    [Fact]
    public async Task A_subject_altered_after_approval_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);
            const string Body = "Nội dung không đổi.";

            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = "Chủ đề đã duyệt",
                    EditableBodyText = Body,
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            await Assert.ThrowsAnyAsync<Exception>(() => SendApprovedAsync(
                db, "Chủ đề KHÁC hẳn", Body, final.FinalPreviewToken));

            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// The PREPARE token is not a send permit. Only the token the Final Preview issued is, which is what
    /// makes "you must look at it before it goes" a rule rather than a suggestion.
    /// </summary>
    [Fact]
    public async Task The_view_stage_token_cannot_authorise_a_send()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);

            await Assert.ThrowsAnyAsync<Exception>(() => SendApprovedAsync(
                db, "Chủ đề", "Nội dung chưa qua bước xem trước cuối.", view.PreviewToken!));

            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// A template edited between approval and send invalidates the approval.
    ///
    /// <para>
    /// The revision is bound into the token because the locked parts of the message — the shell, the
    /// action block wording — come from the template at SEND time. If an operator changed the template in
    /// between, the message that would go out is not the one that was approved, even though the sender's
    /// own words are byte-identical.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_template_edited_after_approval_invalidates_it()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);
            const string Body = "Nội dung đã duyệt trước khi ai đó sửa mẫu.";

            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = "Chủ đề đã duyệt",
                    EditableBodyText = Body,
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            // Somebody saves the template — only the revision needs to move.
            using (var editor = EmailEvidenceHarness.NewContext())
            {
                var row = await editor.EmailTemplates.SingleAsync(t => t.TemplateCode == Template);
                row.Revision += 1;
                await editor.SaveChangesAsync();
            }

            try
            {
                await Assert.ThrowsAnyAsync<Exception>(() => SendApprovedAsync(
                    db, "Chủ đề đã duyệt", Body, final.FinalPreviewToken));

                Assert.Empty(_h.Messages());
            }
            finally
            {
                using var restore = EmailEvidenceHarness.NewContext();
                var row = await restore.EmailTemplates.SingleAsync(t => t.TemplateCode == Template);
                row.Revision -= 1;
                await restore.SaveChangesAsync();
            }
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// A credential-bearing template offers no editor at all, so there is nothing to approve and no way
    /// to put words of one's own around a one-time link.
    /// </summary>
    [Fact]
    public async Task A_credential_template_is_not_runtime_editable()
    {
        EmailEvidenceHarness.RequireDb();

        using var db = EmailEvidenceHarness.NewContext();

        var view = await Prepare(db).Handle(
            new PreviewEmailTemplateQuery(
                SystemEmailTemplates.AccountEmailConfirmation,
                new Dictionary<string, string>
                {
                    ["fullName"] = "Nguyễn Văn A",
                    ["roleName"] = "Staff",
                    ["campusName"] = "HCM",
                    ["expiresInHours"] = "24",
                },
                EmailLanguages.Vi),
            CancellationToken.None);

        Assert.False(view.RuntimeEditable);
    }
}
