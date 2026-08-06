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
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Reports.Commands.SendDeptLeaderInvoiceToStaffLeader;
using PEMS.Application.Reports.Commands.SendDeptLeaderPersonnelReport;
using PEMS.Application.Reports.Commands.SendHoCampusReport;
using PEMS.Application.Reports.Commands.SendStaffLeaderDepartmentReport;
using PEMS.Application.Reports.Commands.SendStaffLeaderDeptInvoice;
using PEMS.Application.Reports.Commands.SendStaffLeaderPersonnelReport;
using PEMS.Application.Reports.Common;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.FileStorage;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Batch 9 — the six report/invoice senders, run for real: real handlers, a real database, the real
/// renderer and dispatcher, real file storage, and real MIME on disk.
///
/// <para>
/// Before this batch all six built their own HTML — the numbers WERE the email body — and none of them
/// attached anything. The four REPORT templates each say "đính kèm là báo cáo…", so the thing these
/// tests mostly exist to prove is that the sentence is true: a document was generated, stored, linked in
/// <c>sent_email_attachments</c>, and delivered as the same bytes.
/// </para>
/// </summary>
public sealed class ReportEmailEndToEndTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("batch9-evidence@partner.example.com");
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-report-files-" + Guid.NewGuid().ToString("N"));

    /// <summary>High, suite-private id range so the rows cannot collide with anything else.</summary>
    private const ulong Base = 990_900;
    private const ulong CampusId = Base + 1;
    private const ulong DeptId = Base + 2;
    private const ulong StaffLeaderId = Base + 3;
    private const ulong DeptLeaderId = Base + 4;
    private const ulong DeptStaffId = Base + 5;
    private const ulong StudentId = Base + 6;
    private const ulong HoId = Base + 7;
    private const ulong VisitRequestId = Base + 8;
    private const ulong VisitInstanceId = Base + 9;
    private const ulong LogisticsItemId = Base + 10;
    /// <summary>The Staff Leader's own home: a database trigger requires STAFF to have a department.</summary>
    private const ulong IcDeptId = Base + 11;

    /// <summary>
    /// The guest who submitted the request. This suite is about report email dispatch, not about the
    /// confirmation gate, so the campus is seeded self-matched (registrant = operational contact) and
    /// therefore already past the gate.
    /// </summary>
    private const ulong RegistrantId = Base + 12;

    private const string CampusName = "PEMS B9 Campus";
    private const string DeptName = "PEMS B9 Phòng Hành chính";

    /// <summary>
    /// Addresses are unique per user (the column is), so the suite marks its rows by a shared domain
    /// prefix instead and cleans up on that.
    /// </summary>
    private const string MailPrefix = "batch9-";
    private const string MailDomain = "@partner.example.com";
    private static string Mail(ulong userId) => $"{MailPrefix}{userId}{MailDomain}";

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
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId { get; init; }
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

    private LocalFileStorageService Storage() => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })
            .Build(),
        new NoHttpClients(),
        new NoServices(),
        NullLogger<LocalFileStorageService>.Instance);

    private ReportEmailSender Sender(ApplicationDbContext db, string? brokenHost = null)
        => new(db, Storage(), _h.Dispatcher(db, brokenHost));

    private static ICurrentUserService Ho => new FakeCurrentUser { UserId = HoId, RoleCode = "HO" };

    private static ICurrentUserService StaffLeader => new FakeCurrentUser
    {
        UserId = StaffLeaderId, RoleCode = "STAFF", SubRole = "LEADER", PrimaryCampusId = CampusId,
    };

    private static ICurrentUserService DeptLeader => new FakeCurrentUser
    {
        UserId = DeptLeaderId, RoleCode = "DEPARTMENT", SubRole = "LEADER", DepartmentId = DeptId,
    };

    // ── Seed ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The smallest world the six senders need. Raw SQL rather than EF: these are reference rows, and
    /// going through the tracked graph would drag in navigations none of these tests care about.
    /// </summary>
    private static async Task SeedAsync(ApplicationDbContext db)
    {
        await CleanupRowsAsync(db);

        var roleIds = await db.Database.SqlQueryRaw<RoleRow>(
                "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        ulong Role(string code) => roleIds.First(r => r.RoleCode == code).RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, {1}, {2}, 'ACTIVE')",
            CampusId, "B9", CampusName);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'GENERAL', 'ACTIVE')",
            DeptId, CampusId, DeptName);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            IcDeptId, CampusId, "PEMS B9 Văn phòng IC");

        // The nullable columns go in as literals: EF's raw-SQL parameters cannot bind a null, and these
        // values are test constants, never anything a caller supplied.
        static string Num(ulong? v) => v?.ToString() ?? "NULL";
        static string Str(string? v) => v is null ? "NULL" : $"'{v}'";

        async Task User(ulong id, string name, string roleCode, string? subRole, ulong? campusId, ulong? deptId)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, department_id, status) "
                + $"VALUES ({id}, {{0}}, {{1}}, {Role(roleCode)}, {Str(subRole)}, {Num(campusId)}, {Num(deptId)}, 'ACTIVE')",
                name, Mail(id));

        // Every internal account needs a home campus — a database trigger enforces it.
        await User(HoId, "PEMS B9 Head Office", "HO", null, CampusId, null);
        await User(StaffLeaderId, "PEMS B9 Staff Leader", "STAFF", "LEADER", CampusId, IcDeptId);
        await User(DeptLeaderId, "PEMS B9 Trưởng phòng", "DEPARTMENT", "LEADER", CampusId, DeptId);
        await User(DeptStaffId, "PEMS B9 Nhân sự phòng", "DEPARTMENT", "STAFF", CampusId, DeptId);
        await User(StudentId, "PEMS B9 Sinh viên", "STUDENT", null, CampusId, null);
        await User(RegistrantId, "PEMS B9 Người đăng ký", "VISITOR", null, null, null);

        // The department head the invoice goes to.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE departments SET head_user_id = {0} WHERE department_id = {1}", DeptLeaderId, DeptId);
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    /// <summary>One logistics line, for the two invoice senders. Kept out of any approved/operational
    /// instance status so the visit-instance triggers stay out of the way.</summary>
    private static async Task SeedLogisticsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_requests (visit_request_id, request_code, status, created_at, "
            + "registrant_user_id, registrant_full_name, registrant_organization, registrant_job_title, "
            + "registrant_phone, registrant_email, registrant_nationality) "
            + "VALUES ({0}, {1}, 'PENDING_APPROVAL', NOW(), {3}, 'B9 Người đăng ký', 'B9 Org', 'B9 Title', "
            + "'0900000000', {2}, 'Việt Nam')",
            VisitRequestId, "B9-REQ", Mail(RegistrantId), RegistrantId);

        // Self-matched contact: the registrant is this campus's operational contact, so the campus sits
        // past the confirmation gate. A campus beyond WAITING_CONTACT_CONFIRMATION with a NULL
        // operational_contact_user_id is refused by trg_visit_campuses_op_contact_guard_bi.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_request_campuses (visit_instance_id, visit_request_id, campus_id, status, "
            + "operational_contact_user_id, operational_contact_confirmed_at, operational_contact_confirmation_source, "
            + "planned_start_at, planned_end_at, created_at) "
            + "VALUES ({0}, {1}, {2}, 'WAITING_REQUEST_APPROVAL', {5}, NOW(), 'REGISTRANT_SELF_MATCH', {3}, {4}, NOW())",
            VisitInstanceId, VisitRequestId, CampusId,
            new DateTime(2026, 7, 10, 9, 0, 0), new DateTime(2026, 7, 10, 11, 30, 0), RegistrantId);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_logistics_items (logistics_item_id, visit_instance_id, title, item_type, "
            + "quantity, requested_to_department_id, status, created_at) "
            + "VALUES ({0}, {1}, {2}, 'OTHER', 2, {3}, 'DONE', NOW())",
            LogisticsItemId, VisitInstanceId, "Thuê màn LED", DeptId);
    }

    private static async Task CleanupRowsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM sent_email_attachments WHERE file_id IN "
            + "(SELECT file_id FROM files WHERE file_purpose = 'REPORT_ATTACHMENT' AND uploaded_by BETWEEN {0} AND {1})",
            Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM files WHERE file_purpose = 'REPORT_ATTACHMENT' AND uploaded_by BETWEEN {0} AND {1}",
            Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_logistics_items WHERE logistics_item_id = {0}", LogisticsItemId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_request_campuses WHERE visit_instance_id = {0}", VisitInstanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_requests WHERE visit_request_id = {0}", VisitRequestId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE departments SET head_user_id = NULL WHERE department_id = {0}", DeptId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM users WHERE user_id BETWEEN {0} AND {1}", Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM departments WHERE department_id IN ({0}, {1})", DeptId, IcDeptId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM campuses WHERE campus_id = {0}", CampusId);
    }

    private static async Task CleanupAsync()
    {
        using var db = EmailEvidenceHarness.NewContext();

        // History first: sent_email_attachments cascades from sent_emails, and files cannot go while an
        // attachment still references them (the foreign key is RESTRICT).
        await db.Database.ExecuteSqlRawAsync(
            "DELETE r, e FROM sent_emails e JOIN sent_email_recipients r ON r.sent_email_id = e.sent_email_id "
            + "WHERE r.recipient_email LIKE {0}", MailPrefix + "%" + MailDomain);

        await CleanupRowsAsync(db);
    }

    /// <summary>Runs <paramref name="body"/> against a seeded world and always tidies up after it.</summary>
    private async Task WithWorldAsync(Func<ApplicationDbContext, Task> body, bool withLogistics = false)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using (var seed = EmailEvidenceHarness.NewContext())
            {
                await SeedAsync(seed);
                if (withLogistics) await SeedLogisticsAsync(seed);
            }

            using var db = EmailEvidenceHarness.NewContext();
            await body(db);
        }
        finally { await CleanupAsync(); }
    }

    // ── Per-caller: the right template, the right variables, a real attachment ──

    private async Task<ulong> RunAsync(ApplicationDbContext db, Func<Task> send)
    {
        var before = await db.SentEmails.AsNoTracking().MaxAsync(e => (ulong?)e.SentEmailId) ?? 0;
        await send();
        return before;
    }

    private static async Task<PEMS.Domain.Entities.Emails.SentEmail> LastMessageAsync(ulong after)
    {
        using var db = EmailEvidenceHarness.NewContext();
        return await db.SentEmails.AsNoTracking()
            .Where(e => e.SentEmailId > after)
            .OrderBy(e => e.SentEmailId)
            .FirstAsync();
    }

    private static async Task<ulong> TemplateIdAsync(string code)
    {
        using var db = EmailEvidenceHarness.NewContext();
        return await db.EmailTemplates.AsNoTracking()
            .Where(t => t.TemplateCode == code).Select(t => t.EmailTemplateId).SingleAsync();
    }

    [Fact]
    public async Task C24_campus_report_uses_the_campus_template_and_attaches_the_document()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendHoCampusReportCommandHandler(db, Ho, Sender(db)).Handle(
                    new SendHoCampusReportCommand
                    {
                        CampusId = CampusId,
                        FromDate = new DateTime(2026, 7, 1),
                        ToDate = new DateTime(2026, 7, 31),
                        Note = "Ghi chú của Head Office",
                    }, CancellationToken.None));

            var message = await LastMessageAsync(before);
            Assert.Equal(await TemplateIdAsync(SystemEmailTemplates.ReportCampusOperation), message.EmailTemplateId);
            Assert.Contains(CampusName, message.Subject);
            Assert.Contains("01/07/2026", message.Subject);
            Assert.Contains("31/07/2026", message.Subject);
            await AssertOneRealPdfAsync(message.SentEmailId, "PEMS_BaoCao_VanHanh_Campus_");
        });

    [Fact]
    public async Task C25_department_report_uses_the_collaboration_template()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendStaffLeaderDepartmentReportCommandHandler(db, StaffLeader, Sender(db)).Handle(
                    new SendStaffLeaderDepartmentReportCommand
                    {
                        DepartmentId = DeptId,
                        FromDate = new DateTime(2026, 7, 1),
                        ToDate = new DateTime(2026, 7, 31),
                    }, CancellationToken.None));

            var message = await LastMessageAsync(before);
            Assert.Equal(
                await TemplateIdAsync(SystemEmailTemplates.ReportDepartmentCollaboration),
                message.EmailTemplateId);
            Assert.Contains(DeptName, message.Subject);
            await AssertOneRealPdfAsync(message.SentEmailId, "PEMS_BaoCao_PhoiHop_PhongBan_");
        });

    [Fact]
    public async Task C26_staff_leader_invoice_uses_the_shared_invoice_template()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendStaffLeaderDeptInvoiceCommandHandler(db, StaffLeader, Sender(db)).Handle(
                    new SendStaffLeaderDeptInvoiceCommand
                    {
                        DepartmentId = DeptId,
                        FromDate = new DateTime(2026, 7, 1),
                        ToDate = new DateTime(2026, 7, 31),
                        Items = new List<SendStaffLeaderDeptInvoiceItem>
                        {
                            new() { LogisticsItemId = LogisticsItemId, UnitPrice = 1_500_000m },
                        },
                    }, CancellationToken.None));

            var message = await LastMessageAsync(before);
            Assert.Equal(
                await TemplateIdAsync(SystemEmailTemplates.ReportDepartmentInvoice),
                message.EmailTemplateId);
            await AssertOneRealPdfAsync(message.SentEmailId, "PEMS_Department_Invoice_");
        }, withLogistics: true);

    [Fact]
    public async Task C27_campus_personnel_report_uses_the_performance_template_and_its_scope()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendStaffLeaderPersonnelReportCommandHandler(db, StaffLeader, Sender(db)).Handle(
                    new SendStaffLeaderPersonnelReportCommand
                    {
                        UserId = StudentId,
                        FromDate = new DateTime(2026, 7, 1),
                        ToDate = new DateTime(2026, 7, 31),
                    }, CancellationToken.None));

            var message = await LastMessageAsync(before);
            Assert.Equal(
                await TemplateIdAsync(SystemEmailTemplates.ReportPersonnelPerformance),
                message.EmailTemplateId);
            // A Student's scope is what they did — join visits — not hosting.
            Assert.Contains("tham gia tiếp khách", message.Subject);
            Assert.DoesNotContain("phụ trách đoàn khách", message.Subject);
            await AssertOneRealPdfAsync(message.SentEmailId, "PEMS_BaoCao_HieuSuat_CaNhan_");
        });

    [Fact]
    public async Task C28_department_personnel_report_shares_the_performance_template_with_its_own_scope()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendDeptLeaderPersonnelReportCommandHandler(db, DeptLeader, Sender(db)).Handle(
                    new SendDeptLeaderPersonnelReportCommand
                    {
                        UserId = DeptStaffId,
                        FromDate = new DateTime(2026, 7, 1),
                        ToDate = new DateTime(2026, 7, 31),
                    }, CancellationToken.None));

            var message = await LastMessageAsync(before);
            Assert.Equal(
                await TemplateIdAsync(SystemEmailTemplates.ReportPersonnelPerformance),
                message.EmailTemplateId);
            Assert.Contains("nhiệm vụ tiếp khách", message.Subject);
            await AssertOneRealPdfAsync(message.SentEmailId, "PEMS_BaoCao_HieuSuat_CaNhan_");
        });

    [Fact]
    public async Task C29_department_invoice_upwards_reuses_the_same_invoice_template()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendDeptLeaderInvoiceToStaffLeaderCommandHandler(db, DeptLeader, Sender(db)).Handle(
                    new SendDeptLeaderInvoiceToStaffLeaderCommand
                    {
                        FromDate = new DateTime(2026, 7, 1),
                        ToDate = new DateTime(2026, 7, 31),
                        Items = new List<SendDeptLeaderInvoiceLineItem>
                        {
                            new() { LogisticsItemId = LogisticsItemId, UnitPrice = 2_000_000m },
                        },
                    }, CancellationToken.None));

            var message = await LastMessageAsync(before);
            // The same template as C-26 — a second one would let the two directions drift apart.
            Assert.Equal(
                await TemplateIdAsync(SystemEmailTemplates.ReportDepartmentInvoice),
                message.EmailTemplateId);
            await AssertOneRealPdfAsync(message.SentEmailId, "PEMS_Department_Invoice_");
        }, withLogistics: true);

    // ── The attachment, in the database and in the MIME ─────────────────────

    /// <summary>
    /// Exactly one PDF, linked to a real <c>files</c> row, and the same document in the delivered MIME.
    /// </summary>
    private async Task AssertOneRealPdfAsync(ulong sentEmailId, string expectedNamePrefix)
    {
        using var db = EmailEvidenceHarness.NewContext();

        var link = Assert.Single(await db.SentEmailAttachments.AsNoTracking()
            .Where(a => a.SentEmailId == sentEmailId).ToListAsync());
        Assert.Equal(PEMS.Domain.Enums.EmailAttachmentType.ATTACHMENT, link.AttachmentType);
        Assert.Null(link.ContentId);
        Assert.StartsWith(expectedNamePrefix, link.DisplayName);
        Assert.EndsWith(".pdf", link.DisplayName);
        Assert.Equal(0u, link.DisplayOrder);

        var file = await db.Files.AsNoTracking().SingleAsync(f => f.FileId == link.FileId);
        Assert.Equal("application/pdf", file.MimeType);
        Assert.Equal(FilePurposeDbValues.ReportAttachment, file.FilePurpose);
        Assert.Equal(link.DisplayName, file.OriginalFilename);
        Assert.True(file.FileSize > 0);
        Assert.False(string.IsNullOrWhiteSpace(file.ChecksumSha256));

        // The stored blob is a real PDF of the recorded size.
        var stored = await File.ReadAllBytesAsync(
            Path.Combine(_storageRoot, file.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Equal(file.FileSize, stored.Length);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(stored, 0, 5));

        // …and the message that went out carries it as a downloadable file, not an inline image.
        var eml = _h.OnlyMessage();
        Assert.Contains("application/pdf", eml.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("attachment", eml.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content-ID:", eml.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cid:", eml.Raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_report_goes_to_one_visible_recipient_with_no_copies_and_mints_no_token()
        => await WithWorldAsync(async db =>
        {
            var tokensBefore = await db.EmailActionTokens.AsNoTracking().CountAsync();
            var before = await RunAsync(db, () =>
                new SendHoCampusReportCommandHandler(db, Ho, Sender(db)).Handle(
                    new SendHoCampusReportCommand { CampusId = CampusId }, CancellationToken.None));

            var eml = _h.OnlyMessage();
            Assert.Equal(1, eml.AddressCount("To"));
            Assert.Equal(string.Empty, eml.Header("Cc"));
            Assert.Equal(string.Empty, eml.Header("Bcc"));
            Assert.DoesNotContain("email-actions", eml.Body);

            using var verify = EmailEvidenceHarness.NewContext();
            Assert.Equal(tokensBefore, await verify.EmailActionTokens.AsNoTracking().CountAsync());

            var message = await LastMessageAsync(before);
            var recipient = Assert.Single(await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == message.SentEmailId).ToListAsync());
            Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);
            Assert.Equal(Mail(StaffLeaderId), recipient.RecipientEmail);
        });

    [Fact]
    public async Task The_stored_copy_is_the_whole_message_and_the_send_is_recorded_truthfully()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendHoCampusReportCommandHandler(db, Ho, Sender(db)).Handle(
                    new SendHoCampusReportCommand { CampusId = CampusId }, CancellationToken.None));

            var message = await LastMessageAsync(before);

            // Nothing in a report grants access on its own, so the body is kept in full.
            Assert.Equal(HistoryBodyPolicy.Full,
                SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.ReportCampusOperation));
            Assert.NotNull(message.BodySnapshot);
            Assert.Contains(CampusName, message.BodySnapshot!);
            Assert.DoesNotContain("{{", message.BodySnapshot!);

            Assert.Equal("SENT", message.Status);
            Assert.NotNull(message.SentAt);
            Assert.Equal(0u, message.RetryCount);

            using var verify = EmailEvidenceHarness.NewContext();
            var recipient = await verify.SentEmailRecipients.AsNoTracking()
                .SingleAsync(r => r.SentEmailId == message.SentEmailId);
            Assert.Equal("SENT", recipient.DeliveryStatus);
            // PEMS has no delivery webhook, so acceptance is never upgraded to delivery.
            Assert.Null(recipient.DeliveredAt);
        });

    [Fact]
    public async Task The_subject_and_the_attached_document_name_the_same_period()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendHoCampusReportCommandHandler(db, Ho, Sender(db)).Handle(
                    new SendHoCampusReportCommand
                    {
                        CampusId = CampusId,
                        FromDate = new DateTime(2026, 7, 1),
                        ToDate = new DateTime(2026, 7, 31),
                    }, CancellationToken.None));

            var message = await LastMessageAsync(before);

            // The queries filter [from, toExclusive); the reader is told the last day INSIDE the period.
            Assert.Contains("01/07/2026", message.Subject);
            Assert.Contains("31/07/2026", message.Subject);
            Assert.DoesNotContain("01/08/2026", message.Subject);
            Assert.Contains("01/07/2026", message.BodySnapshot!);
            Assert.Contains("31/07/2026", message.BodySnapshot!);
        });

    // ── Mandatory: a send that did not happen must not look like one ────────

    [Fact]
    public async Task A_provider_failure_fails_the_command_and_is_recorded_as_FAILED()
        => await WithWorldAsync(async db =>
        {
            var before = await db.SentEmails.AsNoTracking().MaxAsync(e => (ulong?)e.SentEmailId) ?? 0;

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                new SendHoCampusReportCommandHandler(db, Ho, Sender(db, brokenHost: "127.0.0.1")).Handle(
                    new SendHoCampusReportCommand { CampusId = CampusId }, CancellationToken.None));

            Assert.Equal(EmailErrorCodes.ReportDeliveryFailed, ex.ErrorCode);

            // The evidence of the attempt survives the failure.
            var message = await LastMessageAsync(before);
            Assert.Equal("FAILED", message.Status);
            Assert.Null(message.SentAt);
            Assert.False(string.IsNullOrWhiteSpace(message.ErrorMessage));
            // A safe sentence, not the provider's exception text.
            Assert.DoesNotContain("127.0.0.1", message.ErrorMessage!);
            Assert.DoesNotContain("SocketException", message.ErrorMessage!);
            Assert.DoesNotContain(Mail(StaffLeaderId), message.ErrorMessage!);
        });

    [Fact]
    public async Task An_inactive_template_stops_the_send_with_a_stable_error_and_no_history()
        => await WithWorldAsync(async db =>
        {
            var before = await db.SentEmails.AsNoTracking().MaxAsync(e => (ulong?)e.SentEmailId) ?? 0;

            await EmailEvidenceHarness.WithTemplateAsync(
                db, SystemEmailTemplates.ReportCampusOperation,
                t => t.Status = "INACTIVE",
                async () =>
                {
                    // Inactive is a deliberate operator action — a conflict with current state, and never
                    // a reason to fall back to content written in C#.
                    var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                        new SendHoCampusReportCommandHandler(db, Ho, Sender(db)).Handle(
                            new SendHoCampusReportCommand { CampusId = CampusId }, CancellationToken.None));

                    Assert.Equal(EmailErrorCodes.TemplateInactive, ex.ErrorCode);
                });

            // No message, nothing sent — and no file row left pointing at nothing.
            using var verify = EmailEvidenceHarness.NewContext();
            Assert.Empty(await verify.SentEmails.AsNoTracking().Where(e => e.SentEmailId > before).ToListAsync());
            Assert.Empty(_h.Messages());
            Assert.Empty(await verify.Files.AsNoTracking()
                .Where(f => f.FilePurpose == FilePurposeDbValues.ReportAttachment
                            && f.UploadedBy >= Base && f.UploadedBy <= Base + 100)
                .ToListAsync());
        });

    [Fact]
    public async Task Editing_the_template_changes_the_next_report_without_a_restart()
        => await WithWorldAsync(async db =>
        {
            await EmailEvidenceHarness.WithTemplateAsync(
                db, SystemEmailTemplates.ReportCampusOperation,
                t => t.SubjectVi = "[PEMS] Bản sửa nóng — {{campusName}} ({{periodFrom}} – {{periodTo}})",
                async () =>
                {
                    var before = await RunAsync(db, () =>
                        new SendHoCampusReportCommandHandler(db, Ho, Sender(db)).Handle(
                            new SendHoCampusReportCommand { CampusId = CampusId }, CancellationToken.None));

                    var message = await LastMessageAsync(before);
                    Assert.Contains("Bản sửa nóng", message.Subject);
                    Assert.Contains(CampusName, message.Subject);
                });
        });

    // ── One report, several readers ─────────────────────────────────────────

    [Fact]
    public async Task Every_department_leader_gets_their_own_message_and_their_own_stored_document()
        => await WithWorldAsync(async db =>
        {
            // A second leader in the same department: the report is one document, the messages are not.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET sub_role = 'LEADER' WHERE user_id = {0}", DeptStaffId);

            var before = await db.SentEmails.AsNoTracking().MaxAsync(e => (ulong?)e.SentEmailId) ?? 0;

            await new SendStaffLeaderDepartmentReportCommandHandler(db, StaffLeader, Sender(db)).Handle(
                new SendStaffLeaderDepartmentReportCommand { DepartmentId = DeptId }, CancellationToken.None);

            using var verify = EmailEvidenceHarness.NewContext();
            var messages = await verify.SentEmails.AsNoTracking()
                .Where(e => e.SentEmailId > before).OrderBy(e => e.SentEmailId).ToListAsync();
            Assert.Equal(2, messages.Count);

            // Two MIME messages, not one message with two addressees.
            Assert.Equal(2, _h.Messages().Length);

            var ids = messages.Select(m => m.SentEmailId).ToList();
            var links = await verify.SentEmailAttachments.AsNoTracking()
                .Where(a => ids.Contains(a.SentEmailId)).ToListAsync();
            Assert.Equal(2, links.Count);
            // Each message points at its own stored copy — no shared row, no reused stream.
            Assert.Equal(2, links.Select(l => l.FileId).Distinct().Count());

            foreach (var eml in _h.Messages().Select(f => new EmlMessage(File.ReadAllText(f))))
            {
                Assert.Equal(1, eml.AddressCount("To"));
                Assert.Equal(string.Empty, eml.Header("Cc"));
                Assert.Equal(string.Empty, eml.Header("Bcc"));
            }
        });

    [Fact]
    public async Task A_department_with_no_leader_to_write_to_is_refused_before_anything_is_generated()
        => await WithWorldAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET status = 'INACTIVE' WHERE user_id = {0}", DeptLeaderId);
            var before = await db.SentEmails.AsNoTracking().MaxAsync(e => (ulong?)e.SentEmailId) ?? 0;

            await Assert.ThrowsAsync<ValidationException>(() =>
                new SendStaffLeaderDepartmentReportCommandHandler(db, StaffLeader, Sender(db)).Handle(
                    new SendStaffLeaderDepartmentReportCommand { DepartmentId = DeptId }, CancellationToken.None));

            using var verify = EmailEvidenceHarness.NewContext();
            Assert.Empty(await verify.SentEmails.AsNoTracking().Where(e => e.SentEmailId > before).ToListAsync());
            Assert.Empty(_h.Messages());
        });

    // ── The Playwright sink sees the attachment too ─────────────────────────

    /// <summary>
    /// The file sink is what the real-stack browser tests read instead of a mailbox. A report whose
    /// attachment is invisible there cannot be checked end to end, so it records the same five facts
    /// the MIME carries.
    /// </summary>
    [Fact]
    public async Task The_test_sink_records_the_attachment_metadata()
        => await WithWorldAsync(async db =>
        {
            var sinkPath = Path.Combine(_storageRoot, "sink.jsonl");
            Directory.CreateDirectory(_storageRoot);
            var previous = Environment.GetEnvironmentVariable(FileSinkEmailService.PathEnvVar);
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, sinkPath);
            try
            {
                var sink = new FileSinkEmailService(NullLogger<FileSinkEmailService>.Instance);
                var sender = new ReportEmailSender(
                    db, Storage(),
                    new PEMS.Application.Emails.Common.SystemEmailDispatcher(
                        db, new PEMS.Infrastructure.Email.EmailTemplateRenderer(db), sink));

                await new SendHoCampusReportCommandHandler(db, Ho, sender).Handle(
                    new SendHoCampusReportCommand { CampusId = CampusId }, CancellationToken.None);

                var record = (await File.ReadAllLinesAsync(sinkPath)).Last();
                using var json = System.Text.Json.JsonDocument.Parse(record);
                var attachment = Assert.Single(json.RootElement.GetProperty("attachments").EnumerateArray().ToList());

                Assert.StartsWith("PEMS_BaoCao_VanHanh_Campus_", attachment.GetProperty("fileName").GetString());
                Assert.Equal("application/pdf", attachment.GetProperty("contentType").GetString());
                Assert.False(attachment.GetProperty("isInline").GetBoolean());
                Assert.Equal(System.Text.Json.JsonValueKind.Null, attachment.GetProperty("contentId").ValueKind);
                Assert.True(attachment.GetProperty("sizeBytes").GetInt32() > 0);

                // The document itself never lands in the record — a base64 PDF in a log line is noise
                // at best and a leak at worst.
                Assert.DoesNotContain("%PDF-", record);
            }
            finally { Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, previous); }
        });

    // ── Scope: a report about one person goes to that person only ───────────

    [Fact]
    public async Task A_personnel_report_is_addressed_to_the_person_it_is_about()
        => await WithWorldAsync(async db =>
        {
            var before = await RunAsync(db, () =>
                new SendDeptLeaderPersonnelReportCommandHandler(db, DeptLeader, Sender(db)).Handle(
                    new SendDeptLeaderPersonnelReportCommand { UserId = DeptStaffId }, CancellationToken.None));

            var message = await LastMessageAsync(before);
            Assert.Contains("PEMS B9 Nhân sự phòng", message.Subject);
            // Nobody else's name travels with it.
            Assert.DoesNotContain("PEMS B9 Sinh viên", message.BodySnapshot!);

            using var verify = EmailEvidenceHarness.NewContext();
            var recipient = await verify.SentEmailRecipients.AsNoTracking()
                .SingleAsync(r => r.SentEmailId == message.SentEmailId);
            Assert.Equal("PEMS B9 Nhân sự phòng", recipient.RecipientName);
        });

    [Fact]
    public async Task A_leader_cannot_report_on_somebody_outside_their_own_scope()
        => await WithWorldAsync(async db =>
        {
            // The Student belongs to the campus, not to this department.
            await Assert.ThrowsAsync<NotFoundException>(() =>
                new SendDeptLeaderPersonnelReportCommandHandler(db, DeptLeader, Sender(db)).Handle(
                    new SendDeptLeaderPersonnelReportCommand { UserId = StudentId }, CancellationToken.None));

            Assert.Empty(_h.Messages());
        });
}
