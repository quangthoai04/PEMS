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
/// Create-v2 SERVICE tests (Phase B-2): VisitRequestV2CreateService builds the whole per-campus aggregate in
/// the caller's transaction. Covers the DoD matrix — single / multi-same (mixed=0) / multi-mixed (mixed=1) /
/// campus+time-only (mixed=0) / member-copy independence / A==B ACTIVE / A!=B PENDING+INITIAL_CLAIM 72h /
/// duration 29m59s fail + 30m pass / end=start fail / duplicate campus fail / smallest-campus projection.
/// Runs against disposable <c>pems_pr3_test</c> (seed campuses HN/HCM/DN each have exactly one valid Staff
/// Leader), each test in a rolled-back transaction — nothing is committed.
/// </summary>
public sealed class CreateVisitRequestV2ServiceTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8; // a seeded VISITOR user
    private static bool? _dbUp;

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    private static VisitRequestV2CreateService Svc(ApplicationDbContext db) => new(db);
    private static readonly DateTime Now = DateTime.Now;

    // ── Builders ──
    private static ContactPointDto Contact(string email) => new("Contact Person", "Org", "+8490000", email);
    private static RegistrantInputV2 Reg(string email) => new("Registrant", "VN", "Org", "Job", "+8491111", email);
    private static VisitorDto V(string name) => new(name, "VN", "Guest", "GuestOrg");
    private static SupportTeamMemberDto S(string name) => new(name, "Support", "SupOrg", "VN");

    private static CampusVisitFormDto Campus(
        string code, string delegation = "Đoàn ABC", string type = "MEETING", string purpose = "Thăm",
        int startInDays = 20, int durationMin = 120, IList<VisitorDto>? visitors = null,
        IList<SupportTeamMemberDto>? support = null)
    {
        var start = Now.AddDays(startInDays);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(durationMin), delegation, type, null, purpose, "Nội dung",
            visitors ?? new List<VisitorDto> { V("Guest A") },
            support ?? new List<SupportTeamMemberDto>(),
            // Fixed operational contact so "same content" tests are genuinely identical — only fields a test
            // explicitly varies (e.g. delegation) drive has_mixed.
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);
    }

    private static VisitRequestFormDataV2 Form(string contactEmail, params CampusVisitFormDto[] campuses)
        => new(Guid.NewGuid().ToString("N"), Reg("registrant@example.com"), Contact(contactEmail), null, campuses.ToList());

    // ── Tests ──

    /// <summary>
    /// H-4 regression (caught by the real-stack public-create E2E): the operational contact organization and
    /// email are OPTIONAL. A blank value must persist as NULL — the DB CHECK (TRIM(x) &lt;&gt; '') rejects an
    /// empty string, so before the fix a blank operational-contact email produced a 500 at create. Name +
    /// phone stay required.
    /// </summary>
    [Fact]
    public async Task Blank_operational_contact_org_and_email_persist_as_null_not_a_check_violation()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var start = Now.AddDays(20);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddMinutes(30), "Đoàn Optional Op", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { V("Guest A") }, new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "", "+8410", ""), // blank org + email (name + phone present)
            "EN", null, "DECLINED", null, null, null);

        var req = await Svc(db).CreateV2Async(
            Form("registrant@example.com", campus), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        var instance = await db.VisitRequestCampuses.FirstAsync(c => c.VisitRequestId == req.VisitRequestId);
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instance.VisitInstanceId);
        Assert.Null(detail.OperationalContactEmail);         // blank → NULL (CHECK satisfied)
        Assert.Null(detail.OperationalContactOrganization);  // blank → NULL
        Assert.Equal("Op Contact", detail.OperationalContactFullName);
        Assert.Equal("+8410", detail.OperationalContactPhone);
    }

    [Fact]
    public async Task Single_campus_creates_request_instance_detail_members_revisions_audit()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var req = await Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        // Pure V2: there is no discriminator to assert. What proves the shape is that the single campus
        // instance owns its OWN form detail — content never lives on the request row.
        Assert.All(req.CampusInstances, c => Assert.NotNull(c.FormDetail));
        Assert.Equal(VisitScopes.SingleCampus, req.VisitScope);
        Assert.False(req.HasMixedCampusDetails);
        Assert.NotNull(req.BusinessFingerprint);
        var instances = await db.VisitRequestCampuses.Where(c => c.VisitRequestId == req.VisitRequestId).ToListAsync();
        Assert.Single(instances);
        Assert.Equal(1, await db.VisitInstanceFormDetails.CountAsync(d => d.VisitInstanceId == instances[0].VisitInstanceId));
        Assert.Equal(1, await db.VisitInstanceGuestMembers.CountAsync(l => l.VisitInstanceId == instances[0].VisitInstanceId));
        Assert.Equal(1, await db.VisitInstanceFormRevisionHistories.CountAsync(r => r.VisitRequestId == req.VisitRequestId && r.SourceType == "CREATE"));
        Assert.Equal(1, await db.VisitRequestRevisionHistories.CountAsync(r => r.VisitRequestId == req.VisitRequestId && r.SourceType == "CREATE"));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.VisitRequestId == req.VisitRequestId && a.Action == "VISIT_REQUEST_CREATED_V2"));
        // Coordinator routed to the campus Staff Leader.
        Assert.NotNull(instances[0].CoordinatorUserId);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Multi_campus_same_content_mixed_false()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var req = await Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.Equal(VisitScopes.MultiCampus, req.VisitScope);
        Assert.False(req.HasMixedCampusDetails);
        Assert.Equal(2, await db.VisitInstanceFormDetails.CountAsync(d =>
            db.VisitRequestCampuses.Where(c => c.VisitRequestId == req.VisitRequestId).Select(c => c.VisitInstanceId).Contains(d.VisitInstanceId)));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Multi_campus_mixed_content_mixed_true()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var req = await Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN", delegation: "Đoàn A"), Campus("HCM", delegation: "Đoàn B")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.True(req.HasMixedCampusDetails);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Only_campus_and_time_differ_mixed_false()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Same form content everywhere; only the campus code and the schedule differ.
        var req = await Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN", startInDays: 20), Campus("HCM", startInDays: 25)),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.False(req.HasMixedCampusDetails); // schedule/campus never count toward mixed
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Member_copy_creates_independent_ids()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // "Copy from campus A" → same person in both campuses, but independent guest_member_id rows.
        var same = new List<VisitorDto> { V("Nguyen Van A") };
        var req = await Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN", visitors: same), Campus("HCM", visitors: same)),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        var instanceIds = await db.VisitRequestCampuses.Where(c => c.VisitRequestId == req.VisitRequestId)
            .Select(c => c.VisitInstanceId).ToListAsync();
        var links = await db.VisitInstanceGuestMembers.Where(l => instanceIds.Contains(l.VisitInstanceId)).ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.NotEqual(links[0].GuestMemberId, links[1].GuestMemberId); // distinct member rows
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Contact_equals_registrant_access_active_no_identity_change()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var req = await Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.Equal("ACTIVE", req.PrimaryContactAccessStatus);
        Assert.Equal(Registrant, req.VisitorUserId);
        Assert.NotNull(req.PrimaryContactVerifiedAt);
        Assert.Equal(0, await db.VisitRequestIdentityChanges.CountAsync(x => x.VisitRequestId == req.VisitRequestId));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Contact_differs_pending_initial_claim_72h()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var req = await Svc(db).CreateV2Async(
            Form("someone-else@example.com", Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.Equal("PENDING_CONFIRMATION", req.PrimaryContactAccessStatus);
        Assert.Null(req.VisitorUserId); // no account granted until B claims
        var claim = await db.VisitRequestIdentityChanges.SingleAsync(x => x.VisitRequestId == req.VisitRequestId);
        Assert.Equal("INITIAL_CLAIM", claim.ChangeKind);
        Assert.Equal("PENDING", claim.Status);
        Assert.Equal(72, Math.Round((claim.ExpiresAt - claim.RequestedAt).TotalHours));
        Assert.DoesNotContain("someone-else@example.com", claim.NewEmailMasked); // masked
        Assert.Equal(1, await db.VisitRequestIdentityChangeEvents.CountAsync(e => e.IdentityChangeId == claim.IdentityChangeId));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Duration_29m59s_fails_and_30m_passes()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // 29 min → fail (start.AddMinutes(29))
        await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN", durationMin: 29)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));

        using var db2 = NewContext();
        using var tx2 = await db2.Database.BeginTransactionAsync();
        var ok = await Svc(db2).CreateV2Async(
            Form("registrant@example.com", Campus("HN", durationMin: 30)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        Assert.All(ok.CampusInstances, c => Assert.NotNull(c.FormDetail));
        await tx2.RollbackAsync();
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task End_equals_start_fails()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN", durationMin: 0)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Duplicate_campus_fails()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HN"), Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Mixed_content_keeps_each_campus_answering_with_its_own_name()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // HN = campus_id 1, HCM = 2, and HCM is submitted FIRST. Neither ordering nor campus_id may
        // elect a representative: the old create service used to snapshot the smallest campus_id onto
        // the request, and this asserts there is nothing of the sort left to snapshot.
        var req = await Svc(db).CreateV2Async(
            Form("registrant@example.com", Campus("HCM", delegation: "Đoàn HCM"), Campus("HN", delegation: "Đoàn HN")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.True(req.HasMixedCampusDetails);
        // Each campus keeps its OWN name, and the two differ — that is exactly what "mixed" means.
        var byCampus = req.CampusInstances.ToDictionary(c => c.CampusId, c => c.FormDetail!.DelegationName);
        Assert.Equal("Đoàn HN", byCampus[1UL]);
        Assert.Equal("Đoàn HCM", byCampus[2UL]);
        await tx.RollbackAsync();
    }
}
