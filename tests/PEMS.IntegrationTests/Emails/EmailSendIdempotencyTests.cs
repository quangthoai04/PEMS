using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Idempotency;
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
/// G11 / R-103 — one user action produces at most one outbound message, proven against a real database,
/// the real renderer and dispatcher, and real MIME written to a pickup directory.
///
/// <para>
/// The whole point is behaviour that only exists when the pieces are real. A mocked store can be made to
/// return whatever a test wants; it cannot show that two concurrent requests serialise on a row lock, that
/// a replay skips PDF generation, or that a reservation survives the transaction the handler runs in. Those
/// are the claims, so those are what is measured here — by counting rows and files, not by asserting that
/// a fake was called.
/// </para>
/// <para>
/// What is deliberately NOT claimed: exactly-once delivery. Two of these tests exist precisely to show the
/// system admitting it does not know whether a message went out.
/// </para>
/// </summary>
public sealed class EmailSendIdempotencyTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g11-idem@partner.example.com");
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-g11-files-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Suite-private id range. Deliberately far above the other email suites (990_900 … 991_700) and,
    /// more importantly, far above anything AUTO_INCREMENT reaches during a run.
    ///
    /// <para>
    /// This started at 991_600 and collided: the visit-request resubmit suite creates its rows through
    /// EF with generated keys, and in a FULL run its AUTO_INCREMENT counter climbs into 991_6xx — so
    /// "suite-private" was only true when this class ran on its own. Measured mid-run: visit_requests
    /// 991_626…991_636 and visit_request_campuses 991_637…991_647 belonged to UT-RESUBMIT-*, and this
    /// suite's range cleanup then tried to delete a request another suite still had children for.
    /// </para>
    /// </summary>
    private const ulong Base = 8_400_000;
    private const ulong CampusId = Base + 1;
    private const ulong DeptId = Base + 2;
    private const ulong IcDeptId = Base + 3;
    private const ulong StaffLeaderId = Base + 4;
    private const ulong DeptLeaderId = Base + 5;
    private const ulong DeptStaffId = Base + 6;
    private const ulong StudentId = Base + 7;
    private const ulong HoId = Base + 8;
    private const ulong OtherHoId = Base + 9;
    private const ulong VisitRequestId = Base + 10;
    private const ulong VisitInstanceId = Base + 11;
    private const ulong LogisticsItemId = Base + 12;

    /// <summary>
    /// The guest who submitted the request. This suite is about email idempotency, not about the
    /// confirmation gate, so the campus is seeded self-matched (registrant = operational contact) and
    /// therefore already past the gate — the shortest fixture that a campus beyond
    /// WAITING_CONTACT_CONFIRMATION is allowed to have.
    /// </summary>
    private const ulong RegistrantId = Base + 13;

    private const string CampusName = "PEMS G11 Campus";
    private const string DeptName = "PEMS G11 Phòng Hành chính";
    private const string MailPrefix = "g11-idem-";
    private const string MailDomain = "@partner.example.com";
    private static string Mail(ulong userId) => $"{MailPrefix}{userId}{MailDomain}";

    private static readonly DateTime From = new(2026, 7, 1);
    private static readonly DateTime To = new(2026, 7, 31);

    public void Dispose()
    {
        _h.Dispose();
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); }
        catch (IOException) { /* a leaked temp dir must never fail a run */ }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email { get; init; }
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId { get; init; }
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedKey : IIdempotencyKeyAccessor
    {
        public FixedKey(string? key) => CurrentKey = key;
        public string? CurrentKey { get; }
    }

    private sealed class NoHttpClients : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static ICurrentUserService Ho => new FakeCurrentUser
    { UserId = HoId, RoleCode = "HO", Email = Mail(HoId) };

    private static ICurrentUserService OtherHo => new FakeCurrentUser
    { UserId = OtherHoId, RoleCode = "HO", Email = Mail(OtherHoId) };

    private static ICurrentUserService StaffLeader => new FakeCurrentUser
    { UserId = StaffLeaderId, RoleCode = "STAFF", SubRole = "LEADER", PrimaryCampusId = CampusId };

    private static ICurrentUserService DeptLeader => new FakeCurrentUser
    { UserId = DeptLeaderId, RoleCode = "DEPARTMENT", SubRole = "LEADER", DepartmentId = DeptId };

    private LocalFileStorageService Storage() => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })
            .Build(),
        new NoHttpClients(), new NoServices(), NullLogger<LocalFileStorageService>.Instance);

    /// <summary>
    /// The real pipeline: the behaviour wrapping the real handler, with the real store on the same
    /// context. This is what MediatR does — a behaviour and a delegate — so nothing here is a stand-in
    /// for the production path.
    /// </summary>
    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        ApplicationDbContext db,
        ICurrentUserService user,
        TRequest request,
        string? key,
        Func<ReportEmailSender, IRequestHandler<TRequest, TResponse>> handler,
        string? brokenHost = null)
        where TRequest : IRequest<TResponse>
    {
        var store = new EmailSendReservationStore(db);
        var attempt = new EmailSendAttempt(store);
        var sender = new ReportEmailSender(db, Storage(), _h.Dispatcher(db, brokenHost), attempt);

        var behaviour = new EmailSendIdempotencyBehaviour<TRequest, TResponse>(
            store, new FixedKey(key), user, attempt);

        return await behaviour.Handle(
            request,
            ct => handler(sender).Handle(request, ct),
            CancellationToken.None);
    }

    // The six calls, each as a one-liner so a test reads as "which action", not "how to build it".

    private Task<SendHoCampusReportResult> HoCampusAsync(
        ApplicationDbContext db, string? key, string? note = null, ulong? campusId = null,
        ICurrentUserService? user = null, string? brokenHost = null)
        => SendAsync<SendHoCampusReportCommand, SendHoCampusReportResult>(
            db, user ?? Ho,
            new SendHoCampusReportCommand
            { CampusId = campusId ?? CampusId, FromDate = From, ToDate = To, Note = note },
            key, s => new SendHoCampusReportCommandHandler(db, user ?? Ho, s), brokenHost);

    private Task<SendStaffLeaderPersonnelReportResult> SlPersonnelAsync(
        ApplicationDbContext db, string? key, string? note = null)
        => SendAsync<SendStaffLeaderPersonnelReportCommand, SendStaffLeaderPersonnelReportResult>(
            db, StaffLeader,
            new SendStaffLeaderPersonnelReportCommand
            { UserId = StudentId, FromDate = From, ToDate = To, Note = note },
            key, s => new SendStaffLeaderPersonnelReportCommandHandler(db, StaffLeader, s));

    private Task<SendStaffLeaderDepartmentReportResult> SlDepartmentAsync(
        ApplicationDbContext db, string? key, string? note = null)
        => SendAsync<SendStaffLeaderDepartmentReportCommand, SendStaffLeaderDepartmentReportResult>(
            db, StaffLeader,
            new SendStaffLeaderDepartmentReportCommand
            { DepartmentId = DeptId, FromDate = From, ToDate = To, Note = note },
            key, s => new SendStaffLeaderDepartmentReportCommandHandler(db, StaffLeader, s));

    private Task<SendStaffLeaderDeptInvoiceResult> SlInvoiceAsync(
        ApplicationDbContext db, string? key, decimal unitPrice = 1_500_000m)
        => SendAsync<SendStaffLeaderDeptInvoiceCommand, SendStaffLeaderDeptInvoiceResult>(
            db, StaffLeader,
            new SendStaffLeaderDeptInvoiceCommand
            {
                DepartmentId = DeptId, FromDate = From, ToDate = To,
                Items = new List<SendStaffLeaderDeptInvoiceItem>
                { new() { LogisticsItemId = LogisticsItemId, UnitPrice = unitPrice } },
            },
            key, s => new SendStaffLeaderDeptInvoiceCommandHandler(db, StaffLeader, s));

    private Task<SendDeptLeaderPersonnelReportResult> DlPersonnelAsync(
        ApplicationDbContext db, string? key, string? note = null)
        => SendAsync<SendDeptLeaderPersonnelReportCommand, SendDeptLeaderPersonnelReportResult>(
            db, DeptLeader,
            new SendDeptLeaderPersonnelReportCommand
            { UserId = DeptStaffId, FromDate = From, ToDate = To, Note = note },
            key, s => new SendDeptLeaderPersonnelReportCommandHandler(db, DeptLeader, s));

    private Task<SendDeptLeaderInvoiceToStaffLeaderResult> DlInvoiceAsync(
        ApplicationDbContext db, string? key, decimal unitPrice = 2_000_000m)
        => SendAsync<SendDeptLeaderInvoiceToStaffLeaderCommand, SendDeptLeaderInvoiceToStaffLeaderResult>(
            db, DeptLeader,
            new SendDeptLeaderInvoiceToStaffLeaderCommand
            {
                FromDate = From, ToDate = To,
                Items = new List<SendDeptLeaderInvoiceLineItem>
                { new() { LogisticsItemId = LogisticsItemId, UnitPrice = unitPrice } },
            },
            key, s => new SendDeptLeaderInvoiceToStaffLeaderCommandHandler(db, DeptLeader, s));

    // ── Counting what actually happened ─────────────────────────────────────────────────────────

    private sealed record Footprint(int Messages, int SentEmails, int Attachments, int Files, int Reservations);

    private async Task<Footprint> FootprintAsync()
    {
        using var db = EmailEvidenceHarness.NewContext();

        var sentEmailIds = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.RecipientEmail.StartsWith(MailPrefix))
            .Select(r => r.SentEmailId).Distinct().ToListAsync();

        return new Footprint(
            _h.Messages().Length,
            sentEmailIds.Count,
            await db.SentEmailAttachments.AsNoTracking().CountAsync(a => sentEmailIds.Contains(a.SentEmailId)),
            await db.Files.AsNoTracking().CountAsync(f => f.UploadedBy >= Base && f.UploadedBy <= Base + 100),
            await db.EmailSendIdempotencies.AsNoTracking().CountAsync(r => r.ActorUserId >= Base && r.ActorUserId <= Base + 100));
    }

    private static async Task<PEMS.Domain.Entities.Emails.EmailSendIdempotency> ReservationAsync(string operationCode)
    {
        using var db = EmailEvidenceHarness.NewContext();
        return await db.EmailSendIdempotencies.AsNoTracking()
            .Where(r => r.OperationCode == operationCode && r.ActorUserId >= Base && r.ActorUserId <= Base + 100)
            .OrderByDescending(r => r.EmailSendIdempotencyId)
            .FirstAsync();
    }

    private static string NewKey() => Guid.NewGuid().ToString();

    // ── Seed ────────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        await CleanupRowsAsync(db);

        var roleIds = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        ulong Role(string code) => roleIds.First(r => r.RoleCode == code).RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, {1}, {2}, 'ACTIVE')",
            CampusId, "G11", CampusName);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'GENERAL', 'ACTIVE')", DeptId, CampusId, DeptName);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')", IcDeptId, CampusId, "PEMS G11 Văn phòng IC");

        static string Num(ulong? v) => v?.ToString() ?? "NULL";
        static string Str(string? v) => v is null ? "NULL" : $"'{v}'";

        async Task User(ulong id, string name, string roleCode, string? subRole, ulong? campusId, ulong? deptId)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, department_id, status) "
                + $"VALUES ({id}, {{0}}, {{1}}, {Role(roleCode)}, {Str(subRole)}, {Num(campusId)}, {Num(deptId)}, 'ACTIVE')",
                name, Mail(id));

        await User(HoId, "PEMS G11 Head Office", "HO", null, CampusId, null);
        await User(OtherHoId, "PEMS G11 Head Office 2", "HO", null, CampusId, null);
        await User(StaffLeaderId, "PEMS G11 Staff Leader", "STAFF", "LEADER", CampusId, IcDeptId);
        await User(DeptLeaderId, "PEMS G11 Trưởng phòng", "DEPARTMENT", "LEADER", CampusId, DeptId);
        await User(DeptStaffId, "PEMS G11 Nhân sự phòng", "DEPARTMENT", "STAFF", CampusId, DeptId);
        await User(StudentId, "PEMS G11 Sinh viên", "STUDENT", null, CampusId, null);
        await User(RegistrantId, "PEMS G11 Người đăng ký", "VISITOR", null, null, null);

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE departments SET head_user_id = {0} WHERE department_id = {1}", DeptLeaderId, DeptId);

        // One logistics line for the two invoice senders.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_requests (visit_request_id, request_code, status, created_at, "
            + "registrant_user_id, registrant_full_name, registrant_organization, registrant_job_title, "
            + "registrant_phone, registrant_email, registrant_nationality) "
            + "VALUES ({0}, {1}, 'PENDING_APPROVAL', NOW(), {3}, 'G11 Người đăng ký', 'G11 Org', 'G11 Title', "
            + "'0900000000', {2}, 'Việt Nam')",
            VisitRequestId, "G11-REQ", Mail(RegistrantId), RegistrantId);

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

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    private static async Task CleanupRowsAsync(ApplicationDbContext db)
    {
        // Reservations first: they reference sent_emails and users.
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM email_send_idempotency WHERE actor_user_id BETWEEN {0} AND {1}", Base, Base + 100);
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

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM email_send_idempotency WHERE actor_user_id BETWEEN {0} AND {1}", Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE r, e FROM sent_emails e JOIN sent_email_recipients r ON r.sent_email_id = e.sent_email_id "
            + "WHERE r.recipient_email LIKE {0}", MailPrefix + "%" + MailDomain);

        await CleanupRowsAsync(db);
    }

    private async Task WithWorldAsync(Func<ApplicationDbContext, Task> body)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using (var seed = EmailEvidenceHarness.NewContext()) await SeedAsync(seed);
            using var db = EmailEvidenceHarness.NewContext();
            await body(db);
        }
        finally { await CleanupAsync(); }
    }

    // ═══ 1–2. The key is required, and must be usable ════════════════════════════════════════════

    [Fact]
    public async Task A_send_with_no_idempotency_key_is_refused_and_sends_nothing()
        => await WithWorldAsync(async db =>
        {
            var error = await Assert.ThrowsAsync<ValidationException>(() => HoCampusAsync(db, key: null));
            Assert.Equal(EmailErrorCodes.IdempotencyKeyRequired, error.ErrorCode);

            var after = await FootprintAsync();
            Assert.Equal(0, after.Messages);
            Assert.Equal(0, after.SentEmails);
            Assert.Equal(0, after.Reservations);
        });

    [Fact]
    public async Task A_send_with_a_header_injecting_key_is_refused_and_sends_nothing()
        => await WithWorldAsync(async db =>
        {
            var error = await Assert.ThrowsAsync<ValidationException>(
                () => HoCampusAsync(db, key: "abcdefgh\r\nBcc: attacker@example.com"));
            Assert.Equal(EmailErrorCodes.IdempotencyKeyInvalid, error.ErrorCode);

            Assert.Equal(0, (await FootprintAsync()).Messages);
        });

    // ═══ 3–4, 7–10. First send, then replay ══════════════════════════════════════════════════════

    [Fact]
    public async Task A_replay_returns_the_first_result_and_produces_nothing_new()
        => await WithWorldAsync(async db =>
        {
            var key = NewKey();

            var first = await HoCampusAsync(db, key);
            Assert.True(first.Success);

            var afterFirst = await FootprintAsync();
            Assert.Equal(1, afterFirst.Messages);
            Assert.Equal(1, afterFirst.SentEmails);
            Assert.Equal(1, afterFirst.Attachments);
            Assert.Equal(1, afterFirst.Files);

            var replay = await HoCampusAsync(db, key);

            Assert.True(replay.Success);
            Assert.Equal(first.Message, replay.Message);

            // Every one of these would have moved if the handler had run again: a second PDF written to
            // storage, a second files row, a second history row, a second attachment link, a second MIME.
            var afterReplay = await FootprintAsync();
            Assert.Equal(afterFirst, afterReplay);
        });

    [Fact]
    public async Task A_replay_is_recorded_as_one_reservation_that_ran_once()
        => await WithWorldAsync(async db =>
        {
            var key = NewKey();
            await HoCampusAsync(db, key);
            await HoCampusAsync(db, key);

            var reservation = await ReservationAsync(EmailSendOperations.HoCampusReport);

            Assert.Equal(EmailSendStates.Succeeded, reservation.State);
            Assert.Equal(1u, reservation.AttemptCount);
            Assert.NotNull(reservation.CompletedAt);
            Assert.NotNull(reservation.DispatchStartedAt);
            Assert.NotNull(reservation.SentEmailId);

            // The key itself is never stored — only its hash.
            Assert.Matches("^[0-9a-f]{64}$", reservation.IdempotencyKeyHash);
            Assert.DoesNotContain(key, reservation.IdempotencyKeyHash, StringComparison.OrdinalIgnoreCase);
            Assert.Matches("^[0-9a-f]{64}$", reservation.RequestFingerprint);
        });

    // ═══ 5. Same key, different request ══════════════════════════════════════════════════════════

    [Fact]
    public async Task The_same_key_with_a_different_request_is_refused_and_sends_nothing()
        => await WithWorldAsync(async db =>
        {
            var key = NewKey();
            await HoCampusAsync(db, key, note: "Ghi chú ban đầu");
            var afterFirst = await FootprintAsync();

            var error = await Assert.ThrowsAsync<ConflictException>(
                () => HoCampusAsync(db, key, note: "Ghi chú đã sửa"));

            Assert.Equal(EmailErrorCodes.IdempotencyKeyReused, error.ErrorCode);
            Assert.Equal(afterFirst, await FootprintAsync());
        });

    // ═══ 6, 16. Concurrency ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Two_concurrent_requests_with_one_key_produce_one_message()
        => await WithWorldAsync(async _ =>
        {
            var key = NewKey();

            // Separate contexts, as two HTTP requests would have. Sharing one DbContext would serialise
            // them in EF and prove nothing about the database.
            using var dbA = EmailEvidenceHarness.NewContext();
            using var dbB = EmailEvidenceHarness.NewContext();

            var a = Task.Run(() => HoCampusAsync(dbA, key));
            var b = Task.Run(() => HoCampusAsync(dbB, key));

            var outcomes = await Task.WhenAll(
                Settle<SendHoCampusReportResult>(a), Settle<SendHoCampusReportResult>(b));

            // Exactly one ran. The other either lost the race (in-progress) or arrived after the first
            // finished and was replayed — both are correct, and which one happens is a matter of timing.
            var succeeded = outcomes.Count(o => o.Error is null);
            var refusedInProgress = outcomes.Count(o =>
                o.Error is ConflictException c && c.ErrorCode == EmailErrorCodes.IdempotencyInProgress);

            Assert.Equal(2, succeeded + refusedInProgress);
            Assert.True(succeeded >= 1, "Neither request went through.");

            // A unique-constraint collision must never surface as a 500.
            Assert.DoesNotContain(outcomes, o => o.Error is not null and not ConflictException);

            var footprint = await FootprintAsync();
            Assert.Equal(1, footprint.Messages);
            Assert.Equal(1, footprint.SentEmails);
            Assert.Equal(1, footprint.Attachments);
            Assert.Equal(1, footprint.Files);
            Assert.Equal(1, footprint.Reservations);
        });

    private sealed record Outcome<T>(T? Value, Exception? Error);

    private static async Task<Outcome<T>> Settle<T>(Task<T> task)
    {
        try { return new Outcome<T>(await task, null); }
        catch (Exception ex) { return new Outcome<T>(default, ex); }
    }

    // ═══ 11. A clean failure may be retried under the same key ═══════════════════════════════════

    /// <summary>
    /// The scenario: the campus had no active Staff Leader, the send was refused, an administrator fixed
    /// it, and the user pressed the same button again. Nothing was generated or sent the first time, so
    /// this is one attempt that took two tries — not two sends.
    /// </summary>
    [Fact]
    public async Task A_failure_before_dispatch_can_be_retried_with_the_same_key()
        => await WithWorldAsync(async db =>
        {
            var key = NewKey();

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET status = 'INACTIVE' WHERE user_id = {0}", StaffLeaderId);

            // Refused by the handler long before anything is generated. The exception type is asserted
            // exactly — ThrowsAnyAsync would also swallow a failure in the store's own bookkeeping and
            // leave the next assertion to report it as a mysterious state.
            await Assert.ThrowsAsync<ValidationException>(() => HoCampusAsync(db, key));

            var refused = await ReservationAsync(EmailSendOperations.HoCampusReport);
            Assert.Equal(EmailSendStates.FailedBeforeDispatch, refused.State);
            Assert.Null(refused.DispatchStartedAt);
            Assert.Equal(0, (await FootprintAsync()).Messages);

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET status = 'ACTIVE' WHERE user_id = {0}", StaffLeaderId);

            // The SAME request under the SAME key: one attempt, resumed.
            var retry = await HoCampusAsync(db, key);
            Assert.True(retry.Success);

            var reservation = await ReservationAsync(EmailSendOperations.HoCampusReport);
            Assert.Equal(EmailSendStates.Succeeded, reservation.State);
            Assert.Equal(2u, reservation.AttemptCount);

            // One reservation, one message: the retry did not become a second send.
            var footprint = await FootprintAsync();
            Assert.Equal(1, footprint.Messages);
            Assert.Equal(1, footprint.Reservations);
        });

    /// <summary>
    /// Changing WHAT is being sent after a failure ends the attempt, even though the previous one sent
    /// nothing. The key names a request, not a button — and the frontend retires it on this exact code so
    /// the user's next click is a clean new attempt rather than a repeat of the same refusal.
    /// </summary>
    [Fact]
    public async Task Editing_the_request_after_a_clean_failure_needs_a_new_key()
        => await WithWorldAsync(async db =>
        {
            var key = NewKey();

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET status = 'INACTIVE' WHERE user_id = {0}", StaffLeaderId);
            await Assert.ThrowsAsync<ValidationException>(() => HoCampusAsync(db, key, note: "Bản nháp"));
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET status = 'ACTIVE' WHERE user_id = {0}", StaffLeaderId);

            var error = await Assert.ThrowsAsync<ConflictException>(
                () => HoCampusAsync(db, key, note: "Đã sửa lại ghi chú"));
            Assert.Equal(EmailErrorCodes.IdempotencyKeyReused, error.ErrorCode);
            Assert.Equal(0, (await FootprintAsync()).Messages);

            var fresh = await HoCampusAsync(db, NewKey(), note: "Đã sửa lại ghi chú");
            Assert.True(fresh.Success);
            Assert.Equal(1, (await FootprintAsync()).Messages);
        });

    // ═══ 12–13, 17. When the provider's answer is lost ═══════════════════════════════════════════

    [Fact]
    public async Task A_provider_failure_after_dispatch_started_is_recorded_as_an_unknown_outcome()
        => await WithWorldAsync(async db =>
        {
            var key = NewKey();

            // A real SmtpClient pointed at an unreachable host, in a Production environment so the send
            // is required rather than skipped. The exception it throws cannot distinguish "refused" from
            // "accepted, acknowledgement lost" — which is exactly the situation being modelled.
            var error = await Assert.ThrowsAsync<BusinessRuleException>(
                () => HoCampusAsync(db, key, brokenHost: "127.0.0.1"));
            Assert.Equal(EmailErrorCodes.ReportDeliveryFailed, error.ErrorCode);

            var reservation = await ReservationAsync(EmailSendOperations.HoCampusReport);
            Assert.Equal(EmailSendStates.OutcomeUnknown, reservation.State);
            Assert.NotNull(reservation.DispatchStartedAt);

            // The history keeps the truthful FAILED status — acceptance was never claimed.
            using var check = EmailEvidenceHarness.NewContext();
            var ids = await check.SentEmailRecipients.AsNoTracking()
                .Where(r => r.RecipientEmail.StartsWith(MailPrefix)).Select(r => r.SentEmailId).ToListAsync();
            var statuses = await check.SentEmails.AsNoTracking()
                .Where(e => ids.Contains(e.SentEmailId)).Select(e => e.Status).ToListAsync();
            Assert.All(statuses, s => Assert.NotEqual("SENT", s));
        });

    [Fact]
    public async Task An_unknown_outcome_is_never_retried_under_the_same_key()
        => await WithWorldAsync(async db =>
        {
            var key = NewKey();
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => HoCampusAsync(db, key, brokenHost: "127.0.0.1"));

            var afterFirst = await FootprintAsync();

            // A working provider this time — and it still must not send, because the FIRST attempt may
            // already have been delivered. Only the user, with a new key, may decide to send again.
            var error = await Assert.ThrowsAsync<ConflictException>(() => HoCampusAsync(db, key));
            Assert.Equal(EmailErrorCodes.IdempotencyOutcomeUnknown, error.ErrorCode);

            Assert.Equal(afterFirst.Messages, (await FootprintAsync()).Messages);
        });

    [Fact]
    public async Task A_new_key_after_an_unknown_outcome_is_a_new_send()
        => await WithWorldAsync(async db =>
        {
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => HoCampusAsync(db, NewKey(), brokenHost: "127.0.0.1"));

            var deliberate = await HoCampusAsync(db, NewKey());
            Assert.True(deliberate.Success);

            // Two reservations: the one nobody could resolve, and the one the user chose to make.
            Assert.Equal(2, (await FootprintAsync()).Reservations);
        });

    // ═══ 14. One actor's key cannot reach another's record ═══════════════════════════════════════

    [Fact]
    public async Task A_second_actor_using_the_same_key_gets_their_own_send()
        => await WithWorldAsync(async db =>
        {
            var key = NewKey();

            var first = await HoCampusAsync(db, key);
            Assert.True(first.Success);

            // The actor is part of the reservation's identity, so this is a different reservation — not
            // a replay of somebody else's, and not a refusal either.
            var second = await HoCampusAsync(db, key, user: OtherHo);
            Assert.True(second.Success);

            var footprint = await FootprintAsync();
            Assert.Equal(2, footprint.Messages);
            Assert.Equal(2, footprint.Reservations);

            using var check = EmailEvidenceHarness.NewContext();
            var actors = await check.EmailSendIdempotencies.AsNoTracking()
                .Where(r => r.ActorUserId >= Base && r.ActorUserId <= Base + 100)
                .Select(r => r.ActorUserId).OrderBy(a => a).ToListAsync();
            Assert.Equal(new List<ulong> { HoId, OtherHoId }.OrderBy(a => a).ToList(), actors);
        });

    // ═══ 15. A new key for the same payload is a legitimate second send ══════════════════════════

    [Fact]
    public async Task A_new_key_with_an_identical_payload_sends_again()
        => await WithWorldAsync(async db =>
        {
            await HoCampusAsync(db, NewKey());
            await HoCampusAsync(db, NewKey());

            // Deliberately re-sending the same report is a real thing people do. The contract stops
            // ACCIDENTAL duplicates, not intentional ones.
            var footprint = await FootprintAsync();
            Assert.Equal(2, footprint.Messages);
            Assert.Equal(2, footprint.SentEmails);
            Assert.Equal(2, footprint.Reservations);
        });

    // ═══ 21. Every one of the six routes ═════════════════════════════════════════════════════════

    /// <summary>
    /// The per-action evidence table. Each of the six is sent, replayed, and re-sent under a new key,
    /// with the file and row counts checked in between — so "the contract covers all six" is measured
    /// rather than asserted from the type system alone.
    /// </summary>
    [Theory]
    [InlineData(EmailSendOperations.HoCampusReport)]
    [InlineData(EmailSendOperations.StaffLeaderPersonnelReport)]
    [InlineData(EmailSendOperations.StaffLeaderDepartmentReport)]
    [InlineData(EmailSendOperations.StaffLeaderDepartmentInvoice)]
    [InlineData(EmailSendOperations.DeptLeaderPersonnelReport)]
    [InlineData(EmailSendOperations.DeptLeaderInvoiceToStaffLeader)]
    public async Task Every_send_action_replays_instead_of_sending_twice(string operation)
        => await WithWorldAsync(async db =>
        {
            // An async local, not ContinueWith: `Task.Result` on a faulted task wraps the real failure in
            // an AggregateException, so a refusal these tests need to identify by type would arrive
            // unrecognisable.
            async Task<string> Send(string? key) => operation switch
            {
                EmailSendOperations.HoCampusReport =>
                    (await HoCampusAsync(db, key)).Message,
                EmailSendOperations.StaffLeaderPersonnelReport =>
                    (await SlPersonnelAsync(db, key)).Message,
                EmailSendOperations.StaffLeaderDepartmentReport =>
                    (await SlDepartmentAsync(db, key)).Message,
                EmailSendOperations.StaffLeaderDepartmentInvoice =>
                    (await SlInvoiceAsync(db, key)).Message,
                EmailSendOperations.DeptLeaderPersonnelReport =>
                    (await DlPersonnelAsync(db, key)).Message,
                EmailSendOperations.DeptLeaderInvoiceToStaffLeader =>
                    (await DlInvoiceAsync(db, key)).Message,
                _ => throw new InvalidOperationException($"Unmapped operation {operation}"),
            };

            // No key at all: every route refuses. No legacy path anywhere.
            var missing = await Assert.ThrowsAsync<ValidationException>(() => Send(null));
            Assert.Equal(EmailErrorCodes.IdempotencyKeyRequired, missing.ErrorCode);
            Assert.Equal(0, (await FootprintAsync()).Messages);

            var key = NewKey();
            var first = await Send(key);
            var afterFirst = await FootprintAsync();
            Assert.Equal(1, afterFirst.Messages);
            Assert.Equal(1, afterFirst.SentEmails);
            Assert.Equal(1, afterFirst.Attachments);
            Assert.Equal(1, afterFirst.Files);

            var replay = await Send(key);
            Assert.Equal(first, replay);
            Assert.Equal(afterFirst, await FootprintAsync());

            var reservation = await ReservationAsync(operation);
            Assert.Equal(operation, reservation.OperationCode);
            Assert.Equal(EmailSendStates.Succeeded, reservation.State);
            Assert.Equal(1u, reservation.AttemptCount);

            await Send(NewKey());
            var afterNewKey = await FootprintAsync();
            Assert.Equal(2, afterNewKey.Messages);
            Assert.Equal(2, afterNewKey.SentEmails);
            Assert.Equal(2, afterNewKey.Reservations);
        });

    // ═══ The record keeps no secrets ═════════════════════════════════════════════════════════════

    /// <summary>
    /// The reservation keeps hashes and a result, not a copy of the request.
    ///
    /// <para>
    /// One thing it DOES keep is the success message, and that message names the addressee
    /// ("Đã gửi hóa đơn tới …"). That is deliberate and is not a leak: replaying "the result of the
    /// earlier send" is the contract, the only person who can trigger a replay is the actor who already
    /// saw that exact sentence, and <c>sent_email_recipients</c> stores the same address next to the
    /// message itself. What must NOT be there is a copy of the request — the note the user typed, the
    /// prices they entered, the key they generated — because none of it is needed to answer "same
    /// request?" and all of it would be a second home for data that already has one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_reservation_stores_hashes_and_a_result_never_a_copy_of_the_request()
        => await WithWorldAsync(async db =>
        {
            const string note = "Ghi chú nội bộ không được lưu lại";
            var key = NewKey();

            await SendAsync<SendStaffLeaderDeptInvoiceCommand, SendStaffLeaderDeptInvoiceResult>(
                db, StaffLeader,
                new SendStaffLeaderDeptInvoiceCommand
                {
                    DepartmentId = DeptId, FromDate = From, ToDate = To, Note = note,
                    Items = new List<SendStaffLeaderDeptInvoiceItem>
                    { new() { LogisticsItemId = LogisticsItemId, UnitPrice = 1_234_567m } },
                },
                key, s => new SendStaffLeaderDeptInvoiceCommandHandler(db, StaffLeader, s));

            var reservation = await ReservationAsync(EmailSendOperations.StaffLeaderDepartmentInvoice);
            var stored = string.Join("|",
                reservation.OperationCode, reservation.IdempotencyKeyHash, reservation.RequestFingerprint,
                reservation.State, reservation.ResultMessage ?? "", reservation.FailureCode ?? "");

            // No monetary value, in any of the formats it is rendered in.
            Assert.DoesNotContain("1234567", stored, StringComparison.Ordinal);
            Assert.DoesNotContain("1.234.567", stored, StringComparison.Ordinal);
            Assert.DoesNotContain("1,234,567", stored, StringComparison.Ordinal);

            // No note text.
            Assert.DoesNotContain(note, stored, StringComparison.Ordinal);
            Assert.DoesNotContain("Ghi chú nội bộ", stored, StringComparison.Ordinal);

            // No raw key — only its hash.
            Assert.DoesNotContain(key, stored, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(IdempotencyKey.Hash(key), reservation.IdempotencyKeyHash);
        });

    // ── The migration package's own invariants ──────────────────────────────────────────────────

    private static string ScriptsDirectory => Path.Combine(
        CanonicalSqlScript.FindRepositoryRoot(), "docs", "database", "scripts", "email_dispatch_idempotency");

    /// <summary>
    /// The migration declares its connection character set.
    ///
    /// <para>
    /// Its column comments are Vietnamese and its CHECK constraint's literals are stored with the
    /// creating connection's character set. The mysql client on Windows defaults to the console
    /// codepage, so without this the comments land as mojibake and the constraint is recorded with
    /// _cp850 literals — measured on a real run before the line was added, by comparing the raw bytes
    /// of the migrated table against a fresh canonical import.
    /// </para>
    /// <para>
    /// The same omission in the G7 template-sync script rewrote all thirty templates as mojibake, which
    /// is why this is asserted rather than trusted.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("02_up_additive.sql")]
    public void The_migration_sets_its_connection_character_set(string fileName)
    {
        var sql = File.ReadAllText(Path.Combine(ScriptsDirectory, fileName));
        var body = string.Join("\n", sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--")));

        Assert.Contains("SET NAMES utf8mb4", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The migration is additive: it creates one table and writes to nothing else. A DELETE, an ALTER
    /// or a DROP of anything but its own guard procedure would make "purely additive" untrue.
    /// </summary>
    [Fact]
    public void The_migration_touches_nothing_but_its_own_table()
    {
        var sql = File.ReadAllText(Path.Combine(ScriptsDirectory, "02_up_additive.sql"));
        var body = string.Join("\n", sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--")));

        // "ON DELETE RESTRICT" and "ON UPDATE CASCADE" are referential ACTIONS on the new table's own
        // foreign keys, not statements — removing them is what a bare substring check would demand, and
        // they are the very behaviour the audit trail depends on. Only the DML verbs are forbidden.
        Assert.DoesNotContain("DELETE FROM", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", body, StringComparison.OrdinalIgnoreCase);

        foreach (var table in new[]
        {
            "email_templates", "sent_emails", "sent_email_recipients", "sent_email_attachments",
            "email_action_tokens", "files", "users",
        })
        {
            foreach (var verb in new[] { "INSERT INTO", "UPDATE", "DELETE FROM" })
                Assert.DoesNotContain($"{verb} {table}", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The migration refuses to run without being told which database it is changing, and spends that
    /// confirmation on the way out so one authorisation cannot cover a second run on a pooled session.
    /// </summary>
    [Fact]
    public void The_migration_guards_its_target_and_spends_the_confirmation()
    {
        var sql = File.ReadAllText(Path.Combine(ScriptsDirectory, "02_up_additive.sql"));

        Assert.Contains("@pems_idem_confirm_database", sql, StringComparison.Ordinal);
        Assert.Contains("SIGNAL SQLSTATE '45000'", sql, StringComparison.Ordinal);
        Assert.Contains("SET @pems_idem_confirm_database = NULL;", sql, StringComparison.Ordinal);
    }

    /// <summary>The verify script fails the process rather than printing a table nobody reads.</summary>
    [Fact]
    public void The_verify_script_is_a_gate()
    {
        var sql = File.ReadAllText(Path.Combine(ScriptsDirectory, "03_verify.sql"));

        Assert.Contains("SIGNAL SQLSTATE '45000'", sql, StringComparison.Ordinal);
        Assert.Contains("verdict = 'FAIL'", sql, StringComparison.Ordinal);
    }
}
