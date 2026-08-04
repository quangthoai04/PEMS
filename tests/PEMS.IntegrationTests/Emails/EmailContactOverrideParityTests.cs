using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Preview and send must agree about the reply contact — and the per-message override must not become a
/// way around anything the block was built to guarantee.
///
/// <para>
/// <b>The defect this suite pins.</b> The compose modal called the preview endpoint with a template code
/// and some variables and nothing else. With no visit in hand the handler substituted
/// <c>EmailContactHtmlRenderer.DisabledBlock</c> — a dashed box reading "Khối thông tin liên hệ — hệ
/// thống điền đầu mối…" — INTO the body it returned. That body went into a rich-text editor, came back as
/// authored content, and the dispatcher appended the REAL contact card underneath it. So the host approved
/// a preview showing a placeholder, and the recipient received a message carrying both the placeholder
/// and a card the host had never seen.
/// </para>
/// <para>
/// Everything below therefore compares the PREVIEW against the actual delivered MIME, over a real
/// database and a real SMTP pickup directory. Asserting the preview against itself would have passed
/// before the fix.
/// </para>
/// </summary>
public sealed class EmailContactOverrideParityTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("contact-parity@partner.example.com");
    private static readonly HtmlSanitizerService Sanitizer = new();

    public void Dispose() => _h.Dispose();

    // OPTIONAL / CAMPUS_DEFAULT: it resolves from a campus row alone, so the whole suite runs without
    // standing up a visit, a Host and a per-campus instance. Capability is SUPPORTED, which is what makes
    // it the right template for the override rules.
    private const string Code = SystemEmailTemplates.AccountRoleChanged;

    private const ulong CampusId = 986_010;
    private const ulong DepartmentId = 986_010;
    private const ulong ActorId = 986_011;
    private const ulong ColleagueId = 986_012;
    private const ulong OutsiderCampusId = 986_020;
    private const ulong OutsiderDepartmentId = 986_020;
    private const ulong OutsiderId = 986_021;

    private const string CampusEmail = "co.so.986@pems.test";
    private const string ColleagueEmail = "dong.nghiep.986@pems.test";

    private static Dictionary<string, string> Variables() => new(StringComparer.Ordinal)
    {
        ["fullName"] = "Người dùng",
        ["oldRoleName"] = "Staff",
        ["newRoleName"] = "Staff Leader",
        ["campusName"] = "PEMS Parity Campus",
    };

    private sealed class Actor : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => ActorId;
        public string? Email => "actor.986@pems.test";
        public ulong? RoleId => null;
        public string? RoleCode => "STAFF";
        public string? SubRole => "LEADER";
        public ulong? PrimaryCampusId => CampusId;
        public ulong? DepartmentId => DepartmentId;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static PreviewEmailTemplateQueryHandler Preview(ApplicationDbContext db)
        => new(db, new Actor(), new EmailTemplateRenderer(db),
               new EmailContactPolicyStore(db), EmailEvidenceHarness.Contacts(db));

    /// <summary>The operational preview: a real message, with the campus this send belongs to.</summary>
    private static PreviewEmailTemplateQuery OperationalQuery(EmailContactOverrideInput? over = null)
        => new(Code, Variables(), EmailLanguages.Vi, UseSampleData: false,
               VisitInstanceId: null, CampusId: CampusId, DepartmentId: null, ContactOverride: over);

    private SystemEmailRequest Send(
        EmailContactOverrideInput? over = null, SystemEmailContent? content = null)
        => new(
            Code,
            new EmailRecipient(_h.Marker, "Người dùng"),
            Variables(),
            RelatedType: "User",
            RelatedId: ActorId,
            SentBy: ActorId)
        {
            Content = content ?? SystemEmailContent.FromTemplate.Instance,
            ContactScope = new EmailContactScope(CampusId: CampusId),
            ContactOverride = over,
        };

    // ── Seed ────────────────────────────────────────────────────────────────

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        await CleanupAsync(db);

        var staffRole = (await db.Database
            .SqlQueryRaw<RoleRow>("SELECT role_id AS RoleId, role_code AS RoleCode FROM roles")
            .ToListAsync())
            .First(r => r.RoleCode == "STAFF").RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, email, phone, status) "
            + "VALUES ({0}, {1}, {2}, {3}, {4}, 'ACTIVE')",
            CampusId, "PAR1", "PEMS Parity Campus", CampusEmail, "0900000986");

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, {1}, {2}, 'ACTIVE')",
            OutsiderCampusId, "PAR2", "PEMS Parity Campus Khác");

        // A STAFF account must have a department (enforced by a trigger), so the "other campus" needs an
        // IC office of its own — which is also what makes the outsider a genuine out-of-scope account
        // rather than merely a department-less one.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE'), ({3}, {4}, {5}, 'IC', 'ACTIVE')",
            DepartmentId, CampusId, "PEMS Parity IC",
            OutsiderDepartmentId, OutsiderCampusId, "PEMS Parity IC Khác");

        async Task User(ulong id, string name, string email, ulong campus, ulong department)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, phone, role_id, sub_role, "
                + "primary_campus_id, department_id, status) "
                + $"VALUES ({id}, {{0}}, {{1}}, {{2}}, {staffRole}, 'STAFF', {campus}, "
                + $"{department}, 'ACTIVE')",
                name, email, "0900000" + (id % 1000));

        await User(ActorId, "PEMS Parity Người gửi", "actor.986@pems.test", CampusId, DepartmentId);
        await User(ColleagueId, "PEMS Parity Đồng nghiệp", ColleagueEmail, CampusId, DepartmentId);
        // Another campus AND another department: the account the scope rule must refuse.
        await User(OutsiderId, "PEMS Parity Người ngoài", "ngoai.986@pems.test",
            OutsiderCampusId, OutsiderDepartmentId);
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    private static async Task CleanupAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM audit_log_changes WHERE audit_log_id IN "
            + $"(SELECT audit_log_id FROM audit_logs WHERE actor_user_id = {ActorId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM audit_logs WHERE actor_user_id = {ActorId}");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM sent_emails WHERE sent_by = {ActorId}");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM users WHERE user_id IN ({ActorId}, {ColleagueId}, {OutsiderId})");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM departments WHERE department_id IN ({DepartmentId}, {OutsiderDepartmentId})");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM campuses WHERE campus_id IN ({CampusId}, {OutsiderCampusId})");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// How many times the contact appears in the delivered message. One is the whole point; two is the
    /// duplicate this work exists to make impossible.
    ///
    /// <para>
    /// Counted on the contact's ADDRESS rather than on the block's heading, and not for convenience: the
    /// renderer HTML-encodes every value it prints, so the Vietnamese heading arrives as
    /// <c>Th&amp;#244;ng tin li&amp;#234;n hệ</c> and matching the plain string would silently report zero
    /// for a message that carries the block perfectly well. An address is ASCII, unique to the contact,
    /// and is the thing a reader actually needs exactly one of.
    /// </para>
    /// </summary>
    private static int ContactCount(string body, string contactEmail)
        => Regex.Matches(body, Regex.Escape(contactEmail)).Count;

    /// <summary>
    /// Runs a test with <c>{{contactInformationBlock}}</c> present in the template's stored body, in both
    /// languages, and puts it back afterwards.
    ///
    /// <para>
    /// Needed because <c>ACCOUNT_ROLE_CHANGED</c> ships OPTIONAL and its seeded body does not ask for the
    /// block — so a TEMPLATE-mode send has nowhere to substitute it, and a test asserting the block
    /// reached the recipient would be asserting a property of the seed rather than of the pipeline.
    /// AUTHORED mode does not need this: there the block is appended to the author's text rather than
    /// substituted into a stored body, which is exactly why the two paths are tested separately.
    /// </para>
    /// </summary>
    private static Task WithBlockInBodyAsync(ApplicationDbContext db, Func<Task> body)
        => EmailEvidenceHarness.WithTemplateAsync(db, Code, row =>
        {
            row.BodyVi += EmailContactBlockText.Marker;
            row.BodyEn += EmailContactBlockText.Marker;
        }, body);

    /// <summary>
    /// The delivered message as readable text. <c>DecodedTextParts</c> rather than <c>Body</c> so a
    /// multipart message (the alternative text/HTML pair every branded mail carries) is searched in full.
    /// </summary>
    private static string Decoded(EmlMessage eml)
    {
        var parts = eml.DecodedTextParts;
        return string.IsNullOrWhiteSpace(parts) ? eml.Body : parts;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 1. The editable body no longer carries anything about the contact
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The heart of it. An operational preview returns the REAL block, separately, and the editable body
    /// contains neither it nor the dashed stand-in — so there is nothing about the contact for the host to
    /// edit and nothing for them to send back.
    /// </summary>
    [Fact]
    public async Task An_operational_preview_returns_the_real_block_outside_the_editable_body()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var result = await Preview(db).Handle(OperationalQuery(), CancellationToken.None);

            Assert.NotNull(result.Contact);
            Assert.True(result.Contact!.Supported);
            Assert.False(result.Contact.HasError);
            Assert.False(string.IsNullOrWhiteSpace(result.Contact.LockedContactBlockHtml));

            // The real campus, not an invented sample and not a stand-in.
            Assert.Contains(CampusEmail, result.Contact.LockedContactBlockHtml!, StringComparison.Ordinal);

            // …and none of it is in the editable body.
            var standIn = EmailContactHtmlRenderer.DisabledBlock(EmailLanguages.Vi);
            Assert.DoesNotContain(standIn, result.BodyHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("hệ thống điền đầu mối", result.BodyHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(EmailContactBlockText.Marker, result.BodyHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(CampusEmail, result.BodyHtml, StringComparison.Ordinal);
        }
        finally { await CleanupAsync(db); }
    }

    /// <summary>
    /// The template-management preview is unchanged. It has no visit, no campus and no recipient — an
    /// operator editing wording needs to see the LAYOUT, and resolving a real person there would show them
    /// somebody unrelated to the screen they are on.
    /// </summary>
    [Fact]
    public async Task A_template_management_preview_still_uses_the_stand_in_and_reports_no_contact()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var result = await Preview(db).Handle(
                new PreviewEmailTemplateQuery(Code, Variables(), EmailLanguages.Vi),
                CancellationToken.None);

            Assert.Null(result.Contact);
            Assert.DoesNotContain(CampusEmail, result.BodyHtml, StringComparison.Ordinal);
        }
        finally { await CleanupAsync(db); }
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. Preview and the delivered message resolve the same contact
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_default_preview_and_the_default_send_carry_the_same_contact()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            await WithBlockInBodyAsync(db, async () =>
            {
                var preview = await Preview(db).Handle(OperationalQuery(), CancellationToken.None);

                var delivery = await _h.Dispatcher(db).SendAsync(Send());
                Assert.Equal(EmailDeliveryStatus.Sent, delivery.Delivery.Status);

                var body = Decoded(_h.OnlyMessage());

                Assert.Contains(CampusEmail, preview.Contact!.LockedContactBlockHtml!, StringComparison.Ordinal);

                // Exactly one card, and no trace of the preview stand-in in the real message.
                Assert.Equal(1, ContactCount(body, CampusEmail));
                Assert.DoesNotContain("hệ thống điền đầu mối", body, StringComparison.Ordinal);
            });
        }
        finally { await CleanupAsync(db); }
    }

    /// <summary>
    /// The path the defect lived on: the host EDITS the message. The block must be appended exactly once,
    /// and the authored body must not have been able to bring one of its own.
    /// </summary>
    [Fact]
    public async Task An_authored_send_carries_exactly_one_contact_block()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var authored = SystemEmailContent.AuthoredByUser.Create(
                "Cập nhật vai trò tài khoản",
                "<p>Chào anh,</p><p>Vai trò của anh vừa được cập nhật.</p>",
                Sanitizer);

            // Deliberately NOT wrapped in WithBlockInBodyAsync: authored content replaces the stored body
            // entirely, and the block is APPENDED. That is the path the compose modal actually uses, and
            // the path the duplicate lived on.
            await _h.Dispatcher(db).SendAsync(Send(content: authored));

            var body = Decoded(_h.OnlyMessage());

            Assert.Equal(1, ContactCount(body, CampusEmail));
            Assert.DoesNotContain("hệ thống điền đầu mối", body, StringComparison.Ordinal);
            Assert.DoesNotContain(EmailContactBlockText.Marker, body, StringComparison.Ordinal);
        }
        finally { await CleanupAsync(db); }
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. Choosing somebody: preview and MIME show the SAME person
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Choosing_a_colleague_changes_both_the_preview_and_the_delivered_message()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var over = new EmailContactOverrideInput(
                Mode: EmailContactOverrideModes.SystemUser, UserId: ColleagueId);

            await WithBlockInBodyAsync(db, async () =>
            {
                var preview = await Preview(db).Handle(OperationalQuery(over), CancellationToken.None);

                Assert.False(preview.Contact!.HasError);
                Assert.Equal(EmailContactOverrideModes.SystemUser, preview.Contact.Mode);
                Assert.Contains(ColleagueEmail, preview.Contact.LockedContactBlockHtml!, StringComparison.Ordinal);

                await _h.Dispatcher(db).SendAsync(Send(over));
                var body = Decoded(_h.OnlyMessage());

                Assert.Equal(1, ContactCount(body, ColleagueEmail));
                // The policy's own contact is gone, not merely joined by a second card.
                Assert.DoesNotContain(CampusEmail, body, StringComparison.Ordinal);
            });
        }
        finally { await CleanupAsync(db); }
    }

    /// <summary>
    /// The scope rule, enforced at the SEND and not merely in the picker. A Staff Leader at one campus may
    /// not present an account from another as this message's contact, whatever the browser sends.
    /// </summary>
    [Fact]
    public async Task An_account_outside_the_senders_reach_is_refused_at_the_send()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var over = new EmailContactOverrideInput(
                Mode: EmailContactOverrideModes.SystemUser, UserId: OutsiderId);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => _h.Dispatcher(db).SendAsync(Send(over)));

            // Refused BEFORE anything was recorded: a message nobody may send leaves no history row.
            Assert.Empty(_h.Messages());
            Assert.False(await db.SentEmails.AnyAsync(e => e.SentBy == ActorId));
        }
        finally { await CleanupAsync(db); }
    }

    /// <summary>
    /// The preview reports the same refusal as a PANEL STATE rather than as a failed request — the host
    /// keeps their subject, body and attachments and fixes the one thing that is wrong.
    /// </summary>
    [Fact]
    public async Task The_preview_reports_an_out_of_scope_choice_without_losing_the_message()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var result = await Preview(db).Handle(
                OperationalQuery(new EmailContactOverrideInput(
                    Mode: EmailContactOverrideModes.SystemUser, UserId: OutsiderId)),
                CancellationToken.None);

            Assert.NotNull(result.Contact);
            Assert.True(result.Contact!.HasError);
            Assert.Equal(EmailErrorCodes.ContactOverrideUserNotAllowed, result.Contact.ErrorCode);
            // The message itself came back intact.
            Assert.False(string.IsNullOrWhiteSpace(result.BodyHtml));
            Assert.False(string.IsNullOrWhiteSpace(result.Subject));
        }
        finally { await CleanupAsync(db); }
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. Manual contact, and the Reply-To that must match it
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_manual_contact_appears_in_the_preview_and_in_the_delivered_message()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var over = new EmailContactOverrideInput(
                Mode: EmailContactOverrideModes.Manual,
                DisplayName: "Lê Thị Bếp",
                RoleLabel: "Điều phối tiệc",
                Email: "dieu.phoi@nhathau.invalid",
                Phone: "0912345678",
                ReplyToMode: EmailContactReplyToModes.Contact,
                Reason: "Đầu mối thực tế là nhà thầu, không có tài khoản PEMS");

            await WithBlockInBodyAsync(db, async () =>
            {
                var preview = await Preview(db).Handle(OperationalQuery(over), CancellationToken.None);

                Assert.False(preview.Contact!.HasError);
                Assert.Equal(EmailContactOverrideModes.Manual, preview.Contact.Mode);
                Assert.Contains("dieu.phoi@nhathau.invalid",
                    preview.Contact.LockedContactBlockHtml!, StringComparison.Ordinal);
                Assert.Equal("dieu.phoi@nhathau.invalid", preview.Contact.ReplyToDisplay);

                await _h.Dispatcher(db).SendAsync(Send(over));
                var eml = _h.OnlyMessage();
                var body = Decoded(eml);

                Assert.Equal(1, ContactCount(body, "dieu.phoi@nhathau.invalid"));
                Assert.DoesNotContain(CampusEmail, body, StringComparison.Ordinal);

                // The address shown in the block and the address replies go to are the same value — that
                // is the promise, and it is a header, so it is asserted on the header.
                Assert.Contains("dieu.phoi@nhathau.invalid",
                    eml.DecodedHeader("Reply-To"), StringComparison.Ordinal);
            });
        }
        finally { await CleanupAsync(db); }
    }

    [Fact]
    public async Task Reply_to_sender_puts_the_signed_in_account_on_the_header()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            await _h.Dispatcher(db).SendAsync(Send(new EmailContactOverrideInput(
                Mode: EmailContactOverrideModes.TemplateDefault,
                ReplyToMode: EmailContactReplyToModes.Sender)));

            Assert.Contains("actor.986@pems.test",
                _h.OnlyMessage().DecodedHeader("Reply-To"), StringComparison.Ordinal);
        }
        finally { await CleanupAsync(db); }
    }

    [Fact]
    public async Task Reply_to_none_leaves_the_header_off()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            await _h.Dispatcher(db).SendAsync(Send(new EmailContactOverrideInput(
                Mode: EmailContactOverrideModes.TemplateDefault,
                ReplyToMode: EmailContactReplyToModes.None)));

            Assert.True(string.IsNullOrWhiteSpace(_h.OnlyMessage().DecodedHeader("Reply-To")));
        }
        finally { await CleanupAsync(db); }
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. Hiding, and the boundaries the override may not cross
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Hiding_the_block_on_an_optional_template_sends_without_it()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            await WithBlockInBodyAsync(db, async () =>
            {
                await _h.Dispatcher(db).SendAsync(Send(new EmailContactOverrideInput(
                    Mode: EmailContactOverrideModes.TemplateDefault, HideForThisEmail: true)));

                var body = Decoded(_h.OnlyMessage());

                Assert.Equal(0, ContactCount(body, CampusEmail));
                // …and the placeholder was substituted away rather than shipped as literal braces.
                Assert.DoesNotContain(EmailContactBlockText.Marker, body, StringComparison.Ordinal);
            });
        }
        finally { await CleanupAsync(db); }
    }

    /// <summary>
    /// A credential-bearing template refuses the override at the send, not only on the screen that hides
    /// the button.
    /// </summary>
    [Fact]
    public async Task An_unsupported_template_refuses_an_override_at_the_send()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var request = new SystemEmailRequest(
                SystemEmailTemplates.AccountEmailConfirmation,
                new EmailRecipient(_h.Marker, "Người dùng"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["fullName"] = "Người dùng",
                    ["confirmUrl"] = "https://pems.test/xac-nhan/RAW-0001",
                    ["expiryHours"] = "24",
                },
                SentBy: ActorId)
            {
                ContactScope = new EmailContactScope(CampusId: CampusId),
                ContactOverride = new EmailContactOverrideInput(
                    Mode: EmailContactOverrideModes.SystemUser, UserId: ColleagueId),
            };

            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => _h.Dispatcher(db).SendAsync(request));

            Assert.Equal(EmailErrorCodes.ContactOverrideNotAllowed, ex.ErrorCode);
            Assert.Empty(_h.Messages());
        }
        finally { await CleanupAsync(db); }
    }

    /// <summary>
    /// A caller may not hand the dispatcher a contact block. Refused rather than overwritten: overwriting
    /// would work silently, so the caller that built one by hand would keep building one and the next
    /// reader would reasonably believe it reached the message.
    /// </summary>
    [Fact]
    public async Task A_caller_supplied_contact_block_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var forged = new SystemEmailRequest(
                Code,
                new EmailRecipient(_h.Marker, "Người dùng"),
                Variables(),
                TrustedBlocks: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EmailTrustedBlocks.ContactInformationBlock] =
                        "<table><tr><td>Giả mạo</td><td>gia.mao@example.invalid</td></tr></table>",
                },
                SentBy: ActorId)
            {
                ContactScope = new EmailContactScope(CampusId: CampusId),
            };

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(forged));

            Assert.Equal(EmailErrorCodes.ContactBlockSuppliedByCaller, ex.ErrorCode);
            Assert.Empty(_h.Messages());
        }
        finally { await CleanupAsync(db); }
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. An override is one message, and it is recorded
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The configuration is untouched. A host who names a colleague on today's message has not changed
    /// what tomorrow's will say — asserted against the stored rows, not against a second preview, because
    /// a preview reading a cached policy would agree with itself.
    /// </summary>
    [Fact]
    public async Task An_override_does_not_change_the_stored_policy_or_the_next_message()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var before = await db.EmailContactPolicies.AsNoTracking()
                .Where(p => p.ScopeKey == Code)
                .Select(p => new { p.Requirement, p.ContactSource, p.ReplyToSource })
                .ToListAsync();

            await WithBlockInBodyAsync(db, async () =>
            {
                await _h.Dispatcher(db).SendAsync(Send(new EmailContactOverrideInput(
                    Mode: EmailContactOverrideModes.SystemUser, UserId: ColleagueId)));
                _h.ClearMessages();

                var after = await db.EmailContactPolicies.AsNoTracking()
                    .Where(p => p.ScopeKey == Code)
                    .Select(p => new { p.Requirement, p.ContactSource, p.ReplyToSource })
                    .ToListAsync();

                Assert.Equal(before.Count, after.Count);
                Assert.Equal(before.Select(x => x.ToString()), after.Select(x => x.ToString()));

                // …and the NEXT send, with no override, is back to the campus.
                await _h.Dispatcher(db).SendAsync(Send());
                var body = Decoded(_h.OnlyMessage());

                Assert.Contains(CampusEmail, body, StringComparison.Ordinal);
                Assert.DoesNotContain(ColleagueEmail, body, StringComparison.Ordinal);
            });
        }
        finally { await CleanupAsync(db); }
    }

    /// <summary>
    /// An applied override is recorded — and what is recorded is the DECISION, not the message. The
    /// contact's address is deliberately absent: <c>audit_logs</c> is read by more people than the mail
    /// was, and the block itself is already in <c>sent_emails.body_snapshot</c> under that table's rules.
    /// </summary>
    [Fact]
    public async Task An_applied_override_is_audited_without_recording_the_contact_address()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            await _h.Dispatcher(db).SendAsync(Send(new EmailContactOverrideInput(
                Mode: EmailContactOverrideModes.Manual,
                DisplayName: "Lê Thị Bếp",
                RoleLabel: "Điều phối tiệc",
                Email: "dieu.phoi@nhathau.invalid",
                Reason: "Nhà thầu ngoài, không có tài khoản")));

            var rows = await db.AuditLogs.AsNoTracking()
                .Include(a => a.Changes)
                .Where(a => a.ActorUserId == ActorId && a.Action == "EMAIL_CONTACT_OVERRIDE_APPLIED")
                .ToListAsync();

            var row = Assert.Single(rows);
            Assert.Equal("SentEmail", row.EntityType);
            Assert.NotNull(row.EntityId);
            Assert.Contains("Nhà thầu ngoài", row.Reason!, StringComparison.Ordinal);

            // Parsed rather than substring-matched: System.Text.Json escapes non-ASCII by default, so
            // "Lê Thị Bếp" is stored as ê-style escapes and a plain Contains would report the name
            // missing from a row that records it perfectly well.
            var change = Assert.Single(row.Changes);
            using var recorded = System.Text.Json.JsonDocument.Parse(change.NewValueText!);
            var recordedRoot = recorded.RootElement;

            Assert.Equal("MANUAL", recordedRoot.GetProperty("mode").GetString());
            Assert.True(recordedRoot.GetProperty("manual").GetBoolean());
            Assert.False(recordedRoot.GetProperty("hidden").GetBoolean());
            Assert.Equal("Lê Thị Bếp", recordedRoot.GetProperty("contactDisplayName").GetString());

            // The address is deliberately absent: audit_logs is read by more people than the mail was.
            Assert.DoesNotContain("dieu.phoi", change.NewValueText!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nhathau", change.NewValueText!, StringComparison.OrdinalIgnoreCase);
        }
        finally { await CleanupAsync(db); }
    }

    /// <summary>
    /// No override, no audit row. A row per send would bury the interesting ones under thousands of "the
    /// policy applied, as always", and a log nobody can search is not a control.
    /// </summary>
    [Fact]
    public async Task An_ordinary_send_writes_no_override_audit_row()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            await _h.Dispatcher(db).SendAsync(Send());

            Assert.False(await db.AuditLogs.AnyAsync(
                a => a.ActorUserId == ActorId && a.Action == "EMAIL_CONTACT_OVERRIDE_APPLIED"));
        }
        finally { await CleanupAsync(db); }
    }

    // ════════════════════════════════════════════════════════════════════════
    // 7. Both languages
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_english_message_resolves_the_same_contact_as_the_vietnamese_one()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        try
        {
            var over = new EmailContactOverrideInput(
                Mode: EmailContactOverrideModes.SystemUser, UserId: ColleagueId);

            await WithBlockInBodyAsync(db, async () =>
            {
                var preview = await Preview(db).Handle(
                    new PreviewEmailTemplateQuery(Code, Variables(), EmailLanguages.En,
                        UseSampleData: false, VisitInstanceId: null, CampusId: CampusId,
                        DepartmentId: null, ContactOverride: over),
                    CancellationToken.None);

                Assert.False(preview.Contact!.HasError);
                Assert.Contains(ColleagueEmail, preview.Contact.LockedContactBlockHtml!, StringComparison.Ordinal);
                // The English block is the English one, not the Vietnamese block with an English wrapper.
                Assert.Contains(EmailContactPolicyDefaults.DefaultHeadingEn,
                    preview.Contact.LockedContactBlockHtml!, StringComparison.Ordinal);

                await _h.Dispatcher(db).SendAsync(Send(over) with { Language = EmailLanguages.En });
                var body = Decoded(_h.OnlyMessage());

                Assert.Equal(1, ContactCount(body, ColleagueEmail));
            });
        }
        finally { await CleanupAsync(db); }
    }
}
