using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// NP-03 against the real schema — the campus's operational contact resolves to a REAL
/// <c>guest_member_id</c>, chosen by the submitting form's own per-row key.
///
/// <para>
/// The rows the key names do not exist when the payload is built, so the mapping key → inserted id
/// only exists inside the create transaction. That is exactly what these prove: that the id written
/// to <c>visit_instance_form_details.operational_contact_guest_member_id</c> is the row the user
/// pointed at, that a key naming nobody takes the whole transaction down rather than being absorbed,
/// and that the stored snapshot describes that member rather than whatever the payload claimed.
/// </para>
/// <para>
/// Written against a disposable copy of the canonical database, so the FK, the nullable column and
/// the member types are the real ones and not a fixture shaped to make the assertion easy.
/// </para>
/// </summary>
public sealed class OperationalContactMemberLinkTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Visitor = 8;         // VISITOR — external, may be the registrant
    private const ulong OtherVisitor = 22;   // VISITOR — external, used as the campus contact address

    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private static VisitorDto Guest(string name, string key) =>
        new(name, "VN", "Trưởng đoàn", "ABC University", null, key);

    private static SupportTeamMemberDto Support(string name, string key) =>
        new(name, "Phiên dịch", "ABC University", "VN", null, key);

    private static CampusVisitFormDto Campus(
        IList<VisitorDto> visitors,
        IList<SupportTeamMemberDto> support,
        string? contactKey,
        ContactPointDto contact)
        => new(
            "HN", Now.AddDays(20), Now.AddDays(20).AddMinutes(120),
            "Đoàn kiểm tra đầu mối", "MEETING", null, "Thăm", "Nội dung",
            visitors, support, contact,
            "EN", null, "DECLINED", null, null,
            OperationalContactClientMemberKey: contactKey);

    private static VisitRequestFormDataV2 Form(CampusVisitFormDto campus)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Visitor)),
            null,
            new List<CampusVisitFormDto> { campus });

    /// <summary>The contact address is a DIFFERENT external account, so no self-match shortcut fires.</summary>
    private static ContactPointDto Contact(string name = "Op Contact", string org = "OpOrg", string job = "Điều phối")
        => new(name, org, job, "+8410", V2SeedActor.Email(OtherVisitor));

    private static async Task CleanupAsync(ulong id)
    {
        if (id == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("UPDATE visit_instance_form_details SET operational_contact_guest_member_id = NULL WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE FROM audit_log_changes WHERE audit_log_id IN (SELECT audit_log_id FROM audit_logs WHERE visit_request_id = {0})");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    private static async Task<ulong> CreateAsync(CampusVisitFormDto campus)
    {
        using var db = NewContext();
        var result = await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(campus), Visitor, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        return result.VisitRequestId;
    }

    private sealed record Stored(
        ulong? LinkedGuestMemberId, string FullName, string? JobTitle, string? Organization);

    private static async Task<Stored> ReadContactAsync(ulong requestId)
    {
        using var db = NewContext();
        var detail = await db.VisitInstanceFormDetails.AsNoTracking()
            .Join(db.VisitRequestCampuses.AsNoTracking(),
                d => d.VisitInstanceId, c => c.VisitInstanceId, (d, c) => new { d, c })
            .Where(x => x.c.VisitRequestId == requestId)
            .Select(x => new Stored(
                x.d.OperationalContactGuestMemberId,
                x.d.OperationalContactFullName,
                x.d.OperationalContactJobTitle,
                x.d.OperationalContactOrganization))
            .FirstAsync();
        return detail;
    }

    private static async Task<(ulong Id, string Name, string Type)> MemberAsync(ulong requestId, string name)
    {
        using var db = NewContext();
        return await db.VisitGuestMembers.AsNoTracking()
            .Where(m => m.VisitRequestId == requestId && m.FullName == name)
            .Select(m => new ValueTuple<ulong, string, string>(m.GuestMemberId, m.FullName, m.MemberType))
            .FirstAsync();
    }

    // ── The key resolves to the row the user pointed at ──────────────────────────

    [Fact]
    public async Task The_picked_GUEST_becomes_the_stored_link()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus(
                new List<VisitorDto> { Guest("Nguyễn Văn A", "k-a"), Guest("Daniel Kim", "k-b") },
                new List<SupportTeamMemberDto>(),
                contactKey: "k-b",
                Contact()));

            var member = await MemberAsync(requestId, "Daniel Kim");
            var stored = await ReadContactAsync(requestId);

            Assert.Equal(member.Id, stored.LinkedGuestMemberId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_SUPPORT_member_may_hold_the_role()
    {
        // The interpreter travelling with the delegation is frequently who the campus rings, and the
        // picker offered guests only — so this answer could not be given at all before.
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus(
                new List<VisitorDto> { Guest("Nguyễn Văn A", "k-a") },
                new List<SupportTeamMemberDto> { Support("Lê Thị C", "k-c") },
                contactKey: "k-c",
                Contact()));

            var member = await MemberAsync(requestId, "Lê Thị C");
            var stored = await ReadContactAsync(requestId);

            Assert.Equal("EXTERNAL_SUPPORT", member.Type);
            Assert.Equal(member.Id, stored.LinkedGuestMemberId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task The_link_points_INSIDE_this_request_and_this_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus(
                new List<VisitorDto> { Guest("Daniel Kim", "k-b") },
                new List<SupportTeamMemberDto>(),
                contactKey: "k-b",
                Contact()));

            var stored = await ReadContactAsync(requestId);
            using var db = NewContext();

            // Belongs to this request …
            Assert.True(await db.VisitGuestMembers.AnyAsync(m =>
                m.GuestMemberId == stored.LinkedGuestMemberId && m.VisitRequestId == requestId));
            // … and is linked to this campus instance, which the FK alone does not guarantee.
            Assert.True(await db.VisitInstanceGuestMembers.AnyAsync(l =>
                l.GuestMemberId == stored.LinkedGuestMemberId && l.VisitRequestId == requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── The snapshot describes the member, not whatever the payload claimed ──────

    [Fact]
    public async Task The_snapshot_is_rebuilt_from_the_picked_member()
    {
        // A payload can carry one person's key beside another person's name — by tampering, or by a
        // form that copied the fields once and then let them be edited. What gets stored is one
        // person: the member.
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus(
                new List<VisitorDto> { Guest("Daniel Kim", "k-b") },
                new List<SupportTeamMemberDto>(),
                contactKey: "k-b",
                new ContactPointDto("Tên Khác", "Đơn vị khác", "Chức vụ khác", "+8410", V2SeedActor.Email(OtherVisitor))));

            var stored = await ReadContactAsync(requestId);

            Assert.Equal("Daniel Kim", stored.FullName);
            Assert.Equal("Trưởng đoàn", stored.JobTitle);
            Assert.Equal("ABC University", stored.Organization);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Without_a_pick_the_snapshot_the_user_typed_is_kept_verbatim()
    {
        // The counterweight: rewriting the snapshot is a consequence of PICKING somebody, not
        // something the create path does to every request.
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus(
                new List<VisitorDto> { Guest("Daniel Kim", "k-b") },
                new List<SupportTeamMemberDto>(),
                contactKey: null,
                new ContactPointDto("Người Ngoài Đoàn", "Tổ chức X", "Điều phối", "+8410", V2SeedActor.Email(OtherVisitor))));

            var stored = await ReadContactAsync(requestId);

            Assert.Equal("Người Ngoài Đoàn", stored.FullName);
            Assert.Equal("Điều phối", stored.JobTitle);
            // Nobody in the delegation matches that fingerprint, so there is no link — a normal answer.
            Assert.Null(stored.LinkedGuestMemberId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_contact_typed_to_MATCH_a_member_still_links_without_a_key()
    {
        // The fallback an amendment and any older client rely on. It is a guess and stays one, but
        // losing the link entirely would be worse.
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus(
                new List<VisitorDto> { Guest("Daniel Kim", "k-b") },
                new List<SupportTeamMemberDto>(),
                contactKey: null,
                new ContactPointDto("Daniel Kim", "ABC University", "Trưởng đoàn", "+8410", V2SeedActor.Email(OtherVisitor))));

            var member = await MemberAsync(requestId, "Daniel Kim");
            Assert.Equal(member.Id, (await ReadContactAsync(requestId)).LinkedGuestMemberId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── A key that cannot be resolved takes the whole transaction with it ────────

    [Fact]
    public async Task A_key_naming_nobody_refuses_the_create_and_writes_nothing()
    {
        RequireDb();
        int before;
        using (var count = NewContext()) before = await count.VisitRequests.CountAsync();

        using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new VisitRequestV2CreateService(db).CreateV2Async(
                Form(Campus(
                    new List<VisitorDto> { Guest("Daniel Kim", "k-b") },
                    new List<SupportTeamMemberDto>(),
                    contactKey: "k-deleted",
                    Contact())),
                Visitor, "VISITOR_SUBMITTED", Now, CancellationToken.None));
        await tx.RollbackAsync();

        Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
        // A half-saved delegation with no coordinator is worse than a refused submission.
        using var after = NewContext();
        Assert.Equal(before, await after.VisitRequests.CountAsync());
    }

    [Fact]
    public async Task Two_members_under_one_key_refuse_the_create()
    {
        RequireDb();
        using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new VisitRequestV2CreateService(db).CreateV2Async(
                Form(Campus(
                    new List<VisitorDto> { Guest("Nguyễn Văn A", "k-dup"), Guest("Daniel Kim", "k-dup") },
                    new List<SupportTeamMemberDto>(),
                    contactKey: "k-dup",
                    Contact())),
                Visitor, "VISITOR_SUBMITTED", Now, CancellationToken.None));
        await tx.RollbackAsync();

        Assert.Equal(OperationalContactErrorCodes.MemberAmbiguous, ex.ErrorCode);
    }

    [Fact]
    public async Task Two_members_who_share_a_NAME_are_told_apart_by_their_keys()
    {
        // Namesakes in one delegation are ordinary. The pick names one of them exactly; a fingerprint
        // could only shrug.
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus(
                new List<VisitorDto>
                {
                    new("Trần Thị B", "VN", "Thành viên", "XYZ University", null, "k-1"),
                    new("Trần Thị B", "VN", "Trưởng đoàn", "ABC University", null, "k-2"),
                },
                new List<SupportTeamMemberDto>(),
                contactKey: "k-2",
                Contact()));

            var stored = await ReadContactAsync(requestId);
            using var db = NewContext();
            var picked = await db.VisitGuestMembers.AsNoTracking()
                .FirstAsync(m => m.GuestMemberId == stored.LinkedGuestMemberId);

            Assert.Equal("Trưởng đoàn", picked.JobTitle);
            Assert.Equal("ABC University", picked.Organization);
            // Both rows survive: same name, different people, two members.
            Assert.Equal(2, await db.VisitGuestMembers.CountAsync(m =>
                m.VisitRequestId == requestId && m.FullName == "Trần Thị B"));
        }
        finally { await CleanupAsync(requestId); }
    }
}
