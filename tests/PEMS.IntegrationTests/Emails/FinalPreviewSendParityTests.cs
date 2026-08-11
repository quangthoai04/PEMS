using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// The approval check on its own — what a call site does BEFORE anything is written or sent.
    ///
    /// <para>
    /// <paramref name="scopeKey"/> stands in for the scope the calling handler recomputed from the ids it
    /// resolved itself (for an invitation: the visit instance and the participant). Passing one that
    /// differs from the approved one is how a replay is simulated; it is not something a client can set.
    /// </para>
    /// </summary>
    private async Task<SystemEmailContent> ResolveApprovedAsync(
        ApplicationDbContext db, string subject, string bodyText, string finalToken,
        string scopeKey, IReadOnlyList<EmailComposeAttachmentInput>? attachments = null)
    {
        var resolver = new ApprovedEmailContentResolver(
            db, Sender, Sanitizer, Normalizer(db), EmailEvidenceHarness.PreviewTokens());

        var approved = new ApprovedEmailContent(
            finalToken, subject, BodyText: bodyText, Attachments: attachments);
        return await resolver.ResolveAsync(approved, Template, scopeKey, CancellationToken.None);
    }

    /// <summary>Sends the approved content through the dispatcher, exactly as a call site does.</summary>
    private async Task<SystemEmailDispatchResult> SendApprovedAsync(
        ApplicationDbContext db, string subject, string bodyText, string finalToken,
        string? scopeKey = null)
    {
        var content = await ResolveApprovedAsync(
            db, subject, bodyText, finalToken, scopeKey ?? Scope());

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
            Assert.DoesNotContain("{{", view.EditableBodyHtml);

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
    /// An approval prepared for ONE recipient cannot send to another.
    ///
    /// <para>
    /// This is the property the whole scope mechanism exists for, and the one every invitation preview
    /// depends on: the send does not trust the scope in the request body, it recomputes the scope from
    /// the ids IT resolved and compares. So a token minted while looking at participant A's message is
    /// refused when the send resolves participant B — the wording a person approved for one invitee can
    /// never be replayed, word for word, at a different one.
    /// </para>
    /// <para>
    /// It is asserted here rather than assumed, because the frontend fix that made DEPT_SUPPORT previews
    /// produce a MATCHING scope only has value if a non-matching one is still rejected. Loosening this
    /// check would have "fixed" the same bug report and quietly removed the guarantee.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_approval_for_one_participant_cannot_send_to_another()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);
            const string Body = "Nhờ anh hỗ trợ đoàn Kyoto.";

            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = "Thư mời đã duyệt",
                    EditableBodyText = Body,
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            // Same actor, same template, same words — only the participant the send resolved differs.
            var otherRecipientScope = EmailPreviewFingerprint.Scope(
                ("visitInstance", 991_501UL), ("participant", 991_999UL));
            Assert.NotEqual(Scope(), otherRecipientScope);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => SendApprovedAsync(
                db, "Thư mời đã duyệt", Body, final.FinalPreviewToken, scopeKey: otherRecipientScope));

            Assert.True(ex is BusinessRuleException or ValidationException or ForbiddenException,
                $"A replayed approval must be refused by the scope check, but the failure was {ex.GetType().Name}: {ex.Message}");

            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// The files are part of what was approved. A send that swapped the attachment set after the Final
    /// Preview would deliver something the sender never saw — the body they read, carrying a document
    /// they did not choose — so the attachment hash is bound into the token alongside the content hash.
    /// </summary>
    [Fact]
    public async Task Attachments_changed_after_approval_are_refused()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);
            const string Body = "Gửi anh tài liệu kèm theo.";
            var approvedFiles = new List<EmailComposeAttachmentInput>
            {
                new() { FileId = 90_001, AttachmentType = "ATTACHMENT", DisplayOrder = 0 },
            };

            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = "Thư mời kèm tài liệu",
                    EditableBodyText = Body,
                    Attachments = approvedFiles,
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            // Re-sending the SAME set is accepted — the check is about change, not about attachments
            // being present at all, and a test that only proved refusal could pass by refusing both.
            var content = await ResolveApprovedAsync(
                db, "Thư mời kèm tài liệu", Body, final.FinalPreviewToken, Scope(), approvedFiles);
            Assert.NotNull(content);

            // A different file behind the same approval is refused.
            var swapped = new List<EmailComposeAttachmentInput>
            {
                new() { FileId = 90_002, AttachmentType = "ATTACHMENT", DisplayOrder = 0 },
            };
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => ResolveApprovedAsync(
                db, "Thư mời kèm tài liệu", Body, final.FinalPreviewToken, Scope(), swapped));
            Assert.True(ex is BusinessRuleException or ValidationException or ForbiddenException,
                $"A swapped attachment must be refused, but the failure was {ex.GetType().Name}: {ex.Message}");

            // …and so is dropping the file entirely.
            await Assert.ThrowsAnyAsync<Exception>(() => ResolveApprovedAsync(
                db, "Thư mời kèm tài liệu", Body, final.FinalPreviewToken, Scope(), attachments: null));

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

    // ── Where the buttons end up (V4 §9.1, §9.3, §12) ───────────────────────────────────────────

    /// <summary>
    /// ASCII markers on purpose: these assertions are about ORDER, and comparing positions inside a
    /// quoted-printable part is easier to trust when the needles cannot themselves be re-encoded.
    /// </summary>
    private const string Intro = "PARITY-INTRO please choose one of the options below";
    private const string Signature = "PARITY-SIGNATURE Tran Thi Ha";

    /// <summary>
    /// The action block arrives where the SENDER put it, not at the end of the message.
    ///
    /// <para>
    /// <b>The defect this pins.</b> An edited body used to have its action area cut out and the real block
    /// appended last. A message reading "please choose one of the options below" followed by the buttons
    /// therefore arrived with that sentence pointing at a signature and the buttons underneath it — and
    /// the sender could not correct it, because their copy of the message contained no action area to
    /// move. Position was the system's decision, silently, and §12 forbids exactly that.
    /// </para>
    /// <para>
    /// The assertion is deliberately about ORDER rather than exact markup: the block's own HTML differs
    /// between the preview (disabled, no token) and the send (real, tokenised), so pinning the string
    /// would test the wrong thing. What has to hold is that the buttons sit between the sentence that
    /// introduces them and the signature that follows them, in the preview AND in the delivered bytes.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_action_block_arrives_where_the_sender_placed_it()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);
            Assert.True(view.RuntimeEditable);

            // The editable copy hands the sender the action area as a movable node, in the position the
            // stored template gave it — rather than removing it and returning it separately.
            Assert.True(
                EmailSystemBlockNodes.HasActionNode(view.EditableBodyHtml),
                "the editable body carried no system-block node, so its position cannot be edited at all");

            // The sender writes around the node, leaving it in the MIDDLE of the message.
            var edited =
                $"<p>{Intro}</p>"
                + EmailSystemBlockNodes.ActionNodeHtml
                + $"<p>{Signature}</p>";

            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = "Parity: action block position",
                    EditableBodyHtml = edited,
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            AssertBlockSitsBetweenIntroAndSignature(
                final.FinalPreviewHtml,
                EmailComposition.ActionBlockStart,
                "the FINAL PREVIEW put the action block outside the sender's chosen position");

            // …and the delivered message agrees, with the real token this time.
            var resolver = new ApprovedEmailContentResolver(
                db, Sender, Sanitizer, Normalizer(db), EmailEvidenceHarness.PreviewTokens());

            var content = await resolver.ResolveAsync(
                new ApprovedEmailContent(final.FinalPreviewToken, final.Subject, BodyHtml: edited),
                Template, Scope(), CancellationToken.None);

            var sent = await _h.Dispatcher(db).SendAsync(new SystemEmailRequest(
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

            Assert.Equal(EmailDeliveryStatus.Sent, sent.Delivery.Status);

            AssertBlockSitsBetweenIntroAndSignature(
                _h.OnlyMessage().DecodedTextParts,
                AcceptUrl,
                "the DELIVERED message put the action block outside the sender's chosen position");
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// Content with no node still works, with the block appended as before.
    ///
    /// <para>
    /// Every send that does not go through the runtime editor — and every message composed before the
    /// node existed — arrives here without one. Refusing those would break sends that are perfectly
    /// correct, so the append is kept as a fallback rather than removed. This pins that it still happens,
    /// because a regression in it would be silent: the mail would go out with no buttons at all.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Content_without_a_node_still_receives_its_action_block()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);

            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = "Parity: no node",
                    EditableBodyHtml = $"<p>{Intro}</p><p>{Signature}</p>",
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            Assert.Contains(EmailComposition.ActionBlockStart, final.FinalPreviewHtml);
            // Appended: after the signature, which is the old behaviour and the right one here.
            Assert.True(
                final.FinalPreviewHtml.IndexOf(EmailComposition.ActionBlockStart, StringComparison.Ordinal)
                > final.FinalPreviewHtml.IndexOf(Signature, StringComparison.Ordinal),
                "a body with no node should get the block appended, not inserted");
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── The FIRST preview — what the eye icon opens ─────────────────────────────────────────────

    /// <summary>
    /// The preview the eye icon opens is the same message as the final preview of an untouched body.
    ///
    /// <para>
    /// <b>The defect this pins.</b> The prepare endpoint returned a bare body: no shell, and the action
    /// area held open by an empty node. The browser drew the read-only view itself by pasting the
    /// disabled block into that node, so the first stage showed an unbranded message and, for any body
    /// whose node had been lost, showed the buttons in a separate panel underneath the text. Most sends
    /// go straight from that stage — so the common path was approving a shape no recipient receives, and
    /// the only stage telling the truth was the one reached by editing.
    /// </para>
    /// <para>
    /// Compared as text rather than as bytes. The final preview runs the authored pipeline — sanitiser
    /// and image normaliser — over content that arrives from a browser, and the first preview assembles
    /// template output that never left the server. Requiring identical markup would be asserting that
    /// those two pipelines emit the same attribute spelling, which is not the promise; the promise is
    /// that the sender reads the same message either way.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_first_preview_is_the_message_the_final_preview_would_build_from_an_untouched_body()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);

            Assert.False(
                string.IsNullOrWhiteSpace(view.InitialFinalPreviewHtml),
                "the first preview must carry an assembled message, not leave the screen to build one");

            // Hands the EDITABLE half straight back, changing nothing — which is exactly what a sender
            // who opens the editor and presses "Xem trước kết quả" without typing does.
            var final = await Finalise(db).Handle(
                new BuildFinalEmailPreviewCommand
                {
                    PreviewToken = view.PreviewToken!,
                    Subject = view.Subject,
                    EditableBodyHtml = view.EditableBodyHtml,
                    Language = EmailLanguages.Vi,
                },
                CancellationToken.None);

            Assert.Equal(VisibleText(final.FinalPreviewHtml), VisibleText(view.InitialFinalPreviewHtml));
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// The first preview carries the branded shell, and its action buttons sit INSIDE the message —
    /// above the footer, not appended after it.
    ///
    /// <para>
    /// Both halves matter and neither implies the other. A preview could carry the shell and still bolt
    /// the buttons on at the end, which is precisely the old client-side rendering; and buttons in the
    /// right place inside no shell at all is the bare body this replaced.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_first_preview_carries_the_shell_with_the_action_block_inside_it()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);
            var html = view.InitialFinalPreviewHtml;

            Assert.Contains("PEMS — Campus Visit", html, StringComparison.Ordinal);
            Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);

            var block = html.IndexOf(EmailComposition.ActionBlockStart, StringComparison.Ordinal);
            var footer = html.IndexOf("© 2026 PEMS", StringComparison.Ordinal);

            Assert.True(block >= 0, "the first preview showed no action block at all");
            Assert.True(footer >= 0, "the first preview showed no branded footer");
            Assert.True(
                block < footer,
                $"the action block must sit inside the message, not after its footer. block={block}, footer={footer}");

            // Nothing pressable, and no credential: the same disabled copy the final preview shows.
            Assert.DoesNotContain(AcceptUrl, html, StringComparison.Ordinal);
            Assert.DoesNotContain("/public/email-actions/", html, StringComparison.OrdinalIgnoreCase);
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// A sender who changes nothing and presses send receives, at the far end, the message the first
    /// preview showed them.
    ///
    /// <para>
    /// This is the §5 promise for the path most messages take. The send carries no approved content — it
    /// re-renders the template — so nothing links the two beyond both being assembled by the same code;
    /// this test is what turns that into a checked fact.
    /// </para>
    /// <para>
    /// Everything OUTSIDE the action block is compared. Inside it the two differ by design: the preview
    /// holds inert spans, and the delivered message holds real one-time links plus the sentence about
    /// when they expire. Comparing that region would be asserting that the preview leaks a credential.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Sending_from_the_first_preview_without_editing_delivers_what_it_showed()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var view = await Prepare(db).Handle(ViewQuery(), CancellationToken.None);

            // No Content: the plain template send, which is what "Gửi" from VIEW does.
            var sent = await _h.Dispatcher(db).SendAsync(new SystemEmailRequest(
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
                SentBy = Sender.UserId,
            });

            Assert.Equal(EmailDeliveryStatus.Sent, sent.Delivery.Status);

            var delivered = _h.OnlyMessage().DecodedTextParts;

            Assert.Equal(
                VisibleText(OutsideActionBlock(view.InitialFinalPreviewHtml)),
                VisibleText(OutsideActionBlock(delivered)));
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// The readable words of an HTML message: tags dropped, entities decoded, whitespace flattened.
    ///
    /// <para>
    /// Markup is not compared because two pipelines that produce the same message may legitimately spell
    /// it differently — and a test that fails on attribute order teaches people to stop reading it.
    /// </para>
    /// <para>
    /// Entities are decoded for a difference that is real and harmless: the renderer HTML-encodes every
    /// substituted value, so the first preview carries <c>B&amp;#236;nh</c>, while the final preview has
    /// been through the sanitiser, which normalises that back to <c>Bình</c>. Both display the same name.
    /// Decoding happens AFTER tags are stripped, so an escaped <c>&amp;lt;p&amp;gt;</c> in somebody's
    /// text cannot be turned into a tag and then silently removed.
    /// </para>
    /// </summary>
    private static string VisibleText(string html)
    {
        var withoutTags = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(System.Net.WebUtility.HtmlDecode(withoutTags), @"\s+", " ").Trim();
    }

    /// <summary>Everything before the action block and everything after it, with the block itself cut out.</summary>
    private static string OutsideActionBlock(string html)
    {
        var start = html.IndexOf(EmailComposition.ActionBlockStart, StringComparison.Ordinal);
        var end = html.IndexOf(EmailComposition.ActionBlockEnd, StringComparison.Ordinal);

        // Asserted rather than tolerated: a caller that lost its block would otherwise be compared whole
        // against one that kept it, and the mismatch would be reported as a wording difference.
        Assert.True(start >= 0 && end > start, "no action block found to cut out");

        return html[..start] + html[(end + EmailComposition.ActionBlockEnd.Length)..];
    }

    private static void AssertBlockSitsBetweenIntroAndSignature(string haystack, string blockNeedle, string because)
    {
        var intro = haystack.IndexOf(Intro, StringComparison.Ordinal);
        var block = haystack.IndexOf(blockNeedle, StringComparison.Ordinal);
        var signature = haystack.IndexOf(Signature, StringComparison.Ordinal);

        Assert.True(intro >= 0, $"{because}: the intro sentence is missing entirely.");
        Assert.True(block >= 0, $"{because}: the action block is missing entirely.");
        Assert.True(signature >= 0, $"{because}: the signature is missing entirely.");

        Assert.True(
            intro < block && block < signature,
            $"{because}. intro={intro}, block={block}, signature={signature}.");
    }
}
