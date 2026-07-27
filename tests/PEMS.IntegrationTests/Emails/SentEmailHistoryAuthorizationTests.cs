using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Queries.ViewEmail;
using PEMS.Application.Emails.Queries.ViewEmailList;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Giai đoạn 5 — who may read a sent message, proved through the real query handlers against a real
/// database.
///
/// <para>
/// One message runs through every case: sender <b>A</b>, primary recipient <b>B</b>, carbon copy
/// <b>C</b>, blind copies <b>D</b> and <b>E</b>, visit host <b>F</b> who reaches it through the linked
/// object, unrelated <b>G</b>, and the two senior roles <b>H</b> (HO) and <b>I</b> (Admin).
/// </para>
/// <para>
/// The defect being closed was not subtle. <c>ViewEmailQueryHandler</c> had its sender/recipient filter
/// deliberately removed, with a comment explaining that HO and Staff Leader need to "manage sent email",
/// so any holder of an internal role could read any message by id — body, attachments, failure text and
/// the entire blind-copy list included. The list query beside it had been scoped all along, so the two
/// surfaces disagreed about who may read what.
/// </para>
/// </summary>
public sealed class SentEmailHistoryAuthorizationTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g5-authz@partner.example.com");

    private const ulong Base = 991_300;
    private const ulong CampusId = Base + 1;
    private const ulong IcDeptId = Base + 2;

    private const ulong SenderA = Base + 10;
    private const ulong RecipientB = Base + 11;
    private const ulong CopiedC = Base + 12;
    private const ulong BlindD = Base + 13;
    private const ulong BlindE = Base + 14;
    private const ulong HostF = Base + 15;
    private const ulong UnrelatedG = Base + 16;
    private const ulong HoH = Base + 17;
    private const ulong StaffLeaderI = Base + 18;

    private const ulong VisitRequestId = Base + 30;
    private const ulong VisitInstanceId = Base + 31;
    private const ulong ParticipantId = Base + 32;

    private const string MailPrefix = "g5authz-";
    private const string MailDomain = "@partner.example.com";
    private static string Mail(ulong userId) => $"{MailPrefix}{userId}{MailDomain}";

    public void Dispose() => _h.Dispose();

    // ── Rig ─────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email { get; init; }
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static ICurrentUserService Viewer(
        ulong id, string roleCode = "STAFF", string? subRole = null, ulong? campusId = CampusId)
        => new FakeCurrentUser
        {
            UserId = id, Email = Mail(id), RoleCode = roleCode, SubRole = subRole, PrimaryCampusId = campusId,
        };

    private static ICurrentUserService Ho => Viewer(HoH, RoleCodes.Ho);
    private static ICurrentUserService StaffLeader => Viewer(StaffLeaderI, RoleCodes.Staff, UserSubRoles.Leader);

    /// <summary>The real object-scope rule, so the linked-object branch is exercised, not simulated.</summary>
    private static ViewEmailQueryHandler Detail(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new SentEmailObjectScope(db, user));

    private static async Task<ViewEmailDto> Read(
        ApplicationDbContext db, ICurrentUserService user, ulong emailId)
        => await Detail(db, user).Handle(new ViewEmailQuery { Id = emailId }, CancellationToken.None);

    private static IReadOnlyList<string> BccOf(ViewEmailDto dto)
        => dto.Recipients.Where(r => r.RecipientType == "BCC").Select(r => r.RecipientEmail).ToList();

    // ── Seed ────────────────────────────────────────────────────────────────

    private static async Task<ulong> SeedAsync(ApplicationDbContext db)
    {
        await CleanupRowsAsync(db);

        var roles = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        ulong Role(string code) => roles.First(r => r.RoleCode == code).RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, {1}, {2}, 'ACTIVE')",
            CampusId, "G5A", "PEMS G5 Authz Campus");

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            IcDeptId, CampusId, "PEMS G5 Authz IC");

        static string Str(string? v) => v is null ? "NULL" : $"'{v}'";

        async Task User(ulong id, string name, string roleCode, string? subRole, bool inIcDept)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
                + $"department_id, status) VALUES ({id}, {{0}}, {{1}}, {Role(roleCode)}, {Str(subRole)}, "
                + $"{CampusId}, {(inIcDept ? IcDeptId.ToString() : "NULL")}, 'ACTIVE')",
                name, Mail(id));

        await User(SenderA, "PEMS G5 A Người gửi", "STAFF", "STAFF", true);
        await User(RecipientB, "PEMS G5 B Người nhận", "STAFF", "STAFF", true);
        await User(CopiedC, "PEMS G5 C Đồng gửi", "STAFF", "STAFF", true);
        await User(BlindD, "PEMS G5 D Gửi ẩn", "STAFF", "STAFF", true);
        await User(BlindE, "PEMS G5 E Gửi ẩn", "STAFF", "STAFF", true);
        await User(HostF, "PEMS G5 F Chủ trì", "STAFF", "STAFF", true);
        await User(UnrelatedG, "PEMS G5 G Không liên quan", "STAFF", "STAFF", true);
        await User(HoH, "PEMS G5 H Head Office", "HO", null, false);
        await User(StaffLeaderI, "PEMS G5 I Trưởng bộ phận", "STAFF", "LEADER", true);

        // A visit F hosts, with one participant — the business object a linked message hangs off.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_requests (visit_request_id, request_code, status, created_at, "
            + "registrant_full_name, registrant_organization, registrant_job_title, registrant_phone, "
            + "registrant_email, registrant_nationality, contact_person_full_name, "
            + "contact_person_organization, contact_person_phone, contact_person_email) "
            + "VALUES ({0}, {1}, 'PENDING_APPROVAL', NOW(), 'G5 Người đăng ký', 'G5 Org', 'G5 Title', "
            + "'0900000000', {2}, 'Việt Nam', 'G5 Đầu mối', 'G5 Org', '0900000001', {2})",
            VisitRequestId, "G5A-REQ", MailPrefix + "visitor" + MailDomain);

        // ASSIGNED is the earliest status that may carry a host — and the triggers require the full
        // decision record alongside it, from a same-campus Staff Leader.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_request_campuses (visit_instance_id, visit_request_id, campus_id, status, "
            + "current_host_user_id, host_assigned_by, host_assigned_at, decided_by, decided_at, "
            + "decision_actor_role, decision_source, planned_start_at, planned_end_at, created_at) "
            + $"VALUES ({{0}}, {{1}}, {{2}}, 'ASSIGNED', {HostF}, {StaffLeaderI}, NOW(), {StaffLeaderI}, "
            + "NOW(), 'STAFF_LEADER', 'STANDARD_CAMPUS_REVIEW', {3}, {4}, NOW())",
            VisitInstanceId, VisitRequestId, CampusId,
            new DateTime(2026, 8, 12, 9, 0, 0), new DateTime(2026, 8, 12, 11, 30, 0));

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_participants (participant_id, visit_instance_id, user_id, participant_role, "
            + "status, created_at) VALUES ({0}, {1}, {2}, 'IC_SUPPORT', 'INVITED', NOW())",
            ParticipantId, VisitInstanceId, RecipientB);

        return await SeedMessageAsync(db, "GENERAL", null);
    }

    /// <summary>The message under test: A → B, cc C, bcc D and E.</summary>
    private static async Task<ulong> SeedMessageAsync(
        ApplicationDbContext db, string relatedType, ulong? relatedId)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO sent_emails (related_type, related_id, subject, body_snapshot, body_format, "
            + $"status, sent_by, sent_at, created_at) VALUES ({{0}}, {(relatedId?.ToString() ?? "NULL")}, "
            + $"{{1}}, {{2}}, 'HTML', 'SENT', {SenderA}, NOW(), NOW())",
            relatedType, "Trao đổi nội bộ", "<p>Nội dung trao đổi.</p>");

        var emailId = await db.SentEmails.AsNoTracking()
            .Where(e => e.SentBy == SenderA)
            .OrderByDescending(e => e.SentEmailId)
            .Select(e => e.SentEmailId)
            .FirstAsync();

        async Task Recipient(ulong userId, string type)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO sent_email_recipients (sent_email_id, recipient_email, recipient_name, "
                + "recipient_type, delivery_status, created_at) VALUES ({0}, {1}, {2}, {3}, 'SENT', NOW())",
                emailId, Mail(userId), "Người nhận", type);

        await Recipient(RecipientB, "TO");
        await Recipient(CopiedC, "CC");
        await Recipient(BlindD, "BCC");
        await Recipient(BlindE, "BCC");

        return emailId;
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    private static async Task CleanupRowsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM sent_emails WHERE sent_by BETWEEN {Base} AND {Base + 100}");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM visit_participants WHERE visit_instance_id = {VisitInstanceId}");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM visit_request_campuses WHERE visit_instance_id = {VisitInstanceId}");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM visit_requests WHERE visit_request_id = {VisitRequestId}");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM users WHERE user_id BETWEEN {Base} AND {Base + 100}");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM departments WHERE department_id = {IcDeptId}");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM campuses WHERE campus_id = {CampusId}");
    }

    // ── 1. The sender sees what they chose ───────────────────────────────────

    [Fact]
    public async Task The_sender_sees_both_blind_copies()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        var dto = await Read(db, Viewer(SenderA), id);

        Assert.Equal(new[] { Mail(BlindD), Mail(BlindE) }, BccOf(dto));
        Assert.Equal(4, dto.Recipients.Count);

        await CleanupRowsAsync(db);
    }

    // ── 2-3. Visible recipients see their own copy's headers ─────────────────

    [Fact]
    public async Task The_primary_recipient_sees_the_message_but_no_blind_copy()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        var dto = await Read(db, Viewer(RecipientB), id);

        Assert.Empty(BccOf(dto));
        Assert.Equal(
            new[] { Mail(RecipientB), Mail(CopiedC) },
            dto.Recipients.Select(r => r.RecipientEmail));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task The_carbon_copy_sees_the_message_but_no_blind_copy()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        var dto = await Read(db, Viewer(CopiedC), id);

        Assert.Empty(BccOf(dto));
        Assert.DoesNotContain(Mail(BlindD), dto.Recipients.Select(r => r.RecipientEmail));

        await CleanupRowsAsync(db);
    }

    // ── 4-5. Each blind copy sees itself only ────────────────────────────────

    [Fact]
    public async Task A_blind_copy_sees_its_own_entry_and_not_the_other()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        var d = await Read(db, Viewer(BlindD), id);
        Assert.Equal(new[] { Mail(BlindD) }, BccOf(d));

        var e = await Read(db, Viewer(BlindE), id);
        Assert.Equal(new[] { Mail(BlindE) }, BccOf(e));

        await CleanupRowsAsync(db);
    }

    // ── 6. Object scope opens the message, not the blind copies ──────────────

    [Fact]
    public async Task The_visit_host_reads_a_linked_message_without_its_blind_copies()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        // A message about a participant of the visit F hosts.
        var linkedId = await SeedMessageAsync(db, EmailActionTargetTypes.VisitParticipant, ParticipantId);

        var dto = await Read(db, Viewer(HostF), linkedId);

        Assert.Empty(BccOf(dto));
        Assert.Equal(
            new[] { Mail(RecipientB), Mail(CopiedC) },
            dto.Recipients.Select(r => r.RecipientEmail));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task Object_scope_does_not_reach_a_message_with_no_business_object()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        // Personal correspondence between other people. Hosting a visit grants nothing here.
        await Assert.ThrowsAsync<ForbiddenException>(() => Read(db, Viewer(HostF), id));

        await CleanupRowsAsync(db);
    }

    // ── 7-9. Nobody gets in on a role ────────────────────────────────────────

    [Fact]
    public async Task An_unrelated_colleague_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => Read(db, Viewer(UnrelatedG), id));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task Ho_cannot_read_a_manual_message_it_was_not_party_to()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => Read(db, Ho, id));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_staff_leader_cannot_read_a_manual_message_it_was_not_party_to()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => Read(db, StaffLeader, id));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task Ho_reaching_a_linked_message_through_the_visit_still_gets_no_blind_copies()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        var linkedId = await SeedMessageAsync(db, EmailActionTargetTypes.VisitParticipant, ParticipantId);

        // HO may view the visit, so the message opens — as a linked-object reader, never as a superuser.
        var dto = await Read(db, Ho, linkedId);
        Assert.Empty(BccOf(dto));

        var leader = await Read(db, StaffLeader, linkedId);
        Assert.Empty(BccOf(leader));

        await CleanupRowsAsync(db);
    }

    // ── 10-12. The surfaces agree, and none of them counts the hidden ────────

    [Fact]
    public async Task The_list_and_the_detail_agree_about_who_may_read_the_message()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        async Task<bool> ListsIt(ICurrentUserService user)
        {
            var page = await new ViewEmailListQueryHandler(db, user).Handle(
                new ViewEmailListQuery { MailBox = "all", Page = 1, PageSize = 50 }, CancellationToken.None);
            return page.Items.Any(i => i.Id == id);
        }

        async Task<bool> OpensIt(ICurrentUserService user)
        {
            try { await Read(db, user, id); return true; }
            catch (ForbiddenException) { return false; }
        }

        foreach (var user in new[]
                 {
                     Viewer(SenderA), Viewer(RecipientB), Viewer(CopiedC), Viewer(BlindD),
                     Viewer(BlindE), Viewer(UnrelatedG), Ho, StaffLeader,
                 })
        {
            Assert.Equal(await ListsIt(user), await OpensIt(user));
        }

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task Searching_by_a_blind_address_never_surfaces_the_message_to_anyone_else()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        // The keyword filter searches the subject and the counterpart, never the recipient rows, so a
        // blind address cannot be used as a probe.
        async Task<int> Search(ICurrentUserService user, string keyword)
        {
            var page = await new ViewEmailListQueryHandler(db, user).Handle(
                new ViewEmailListQuery { MailBox = "all", Keyword = keyword, Page = 1, PageSize = 50 },
                CancellationToken.None);
            return page.Items.Count(i => i.Id == id);
        }

        Assert.Equal(0, await Search(Viewer(RecipientB), Mail(BlindD)));
        Assert.Equal(0, await Search(Viewer(CopiedC), Mail(BlindD)));
        Assert.Equal(0, await Search(Ho, Mail(BlindD)));
        Assert.Equal(0, await Search(Viewer(UnrelatedG), Mail(BlindD)));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task The_detail_payload_carries_no_count_that_would_betray_a_blind_copy()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var id = await SeedAsync(db);

        var seen = await Read(db, Viewer(RecipientB), id);

        // Filtering the list but publishing "2 người nhận ẩn" beside it would give the game away just as
        // completely. The only recipient information in the payload is the filtered list itself.
        var properties = typeof(ViewEmailDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(properties, n => n.Contains("Bcc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, n => n.Contains("Hidden", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, n => n.Contains("Total", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, n => n.Contains("RecipientCount", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, seen.Recipients.Count);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_message_that_does_not_exist_reports_not_found_rather_than_a_null_payload()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() => Read(db, Viewer(SenderA), 999_999_999));

        await CleanupRowsAsync(db);
    }
}
