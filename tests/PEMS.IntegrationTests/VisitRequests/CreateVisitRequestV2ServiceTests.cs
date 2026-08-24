using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Create-v2 SERVICE tests (Phase B-2): VisitRequestV2CreateService builds the whole per-campus aggregate in
/// the caller's transaction. Covers the DoD matrix — single / multi-same (mixed=0) / multi-mixed (mixed=1) /
/// campus+time-only (mixed=0) / member-copy independence / A==B self-matched /
/// A!=B PENDING + INITIAL_CONFIRMATION 72h /
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
    private static ContactPointDto Contact(string email) => new("Contact Person", "Org", "Trưởng phòng Hợp tác", "+8490000", email);
    private static RegistrantInputV2 Reg(string email) => new("Registrant", "VN", "Org", "Job", "+8491111", email);
    private static VisitorDto V(string name) => new(name, "VN", "Guest", "GuestOrg");
    private static SupportTeamMemberDto S(string name) => new(name, "Support", "SupOrg", "VN");

    private static CampusVisitFormDto Campus(
        string code, string delegation = "Đoàn ABC", string type = "MEETING", string purpose = "Thăm",
        int startInDays = 20, int durationMin = 120, IList<VisitorDto>? visitors = null,
        IList<SupportTeamMemberDto>? support = null, string contactEmail = "op@example.com")
    {
        var start = Now.AddDays(startInDays);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(durationMin), delegation, type, null, purpose, "Nội dung",
            visitors ?? new List<VisitorDto> { V("Guest A") },
            support ?? new List<SupportTeamMemberDto>(),
            // Fixed by default so "same content" tests are genuinely identical — only fields a test
            // explicitly varies (delegation, contact) drive has_mixed. The ADDRESS is what self-match
            // is decided against, so a test that cares passes its own.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", contactEmail),
            "EN", null, "DECLINED", null, null);
    }

    /// <summary>
    /// There is no request-level contact any more, so the payload is registrant + campuses. Callers that
    /// care about self-match set the address on the CAMPUS via <c>Campus(contactEmail: …)</c>.
    /// </summary>
    private static VisitRequestFormDataV2 Form(params CampusVisitFormDto[] campuses)
        => new(Guid.NewGuid().ToString("N"), Reg("registrant@example.com"), null, campuses.ToList());

    // ── Tests ──

    /// <summary>
    /// The operational contact ORGANIZATION is still optional and a blank one must persist as NULL — the DB
    /// CHECK (TRIM(x) &lt;&gt; '') rejects an empty string, so a blank value that reached the column as ""
    /// used to produce a 500 at create (H-4, caught by the real-stack public-create E2E).
    ///
    /// <para>
    /// The EMAIL is no longer in that group. It is the only thing a per-campus confirmation invitation can
    /// be bound to, so a campus without one could never leave WAITING_CONTACT_CONFIRMATION: the column is
    /// NOT NULL now and the service refuses the submit outright. This asserts both halves in one place, so
    /// "optional" cannot quietly grow back to include the email.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Blank_operational_contact_org_persists_as_null_but_a_blank_email_is_refused()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var start = Now.AddDays(20);

        CampusVisitFormDto CampusWith(string organization, string email) => new(
            "HN", start, start.AddMinutes(30), "Đoàn Optional Op", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { V("Guest A") }, new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", organization, "Trưởng phòng Hợp tác", "+8410", email),
            "EN", null, "DECLINED", null, null);

        var blankEmail = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Svc(db).CreateV2Async(
                Form(CampusWith("", "")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));
        Assert.Contains("email đầu mối vận hành", blankEmail.Message);

        var req = await Svc(db).CreateV2Async(
            Form(CampusWith("", "op-optional@example.com")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        var instance = await db.VisitRequestCampuses.FirstAsync(c => c.VisitRequestId == req.VisitRequestId);
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instance.VisitInstanceId);
        Assert.Null(detail.OperationalContactOrganization);  // blank → NULL
        Assert.Equal("op-optional@example.com", detail.OperationalContactEmail);
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
            Form(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

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
            Form(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

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
            Form(Campus("HN", delegation: "Đoàn A"), Campus("HCM", delegation: "Đoàn B")),
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
            Form(Campus("HN", startInDays: 20), Campus("HCM", startInDays: 25)),
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
            Form(Campus("HN", visitors: same), Campus("HCM", visitors: same)),
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
            Form(Campus("HN", contactEmail: "registrant@example.com")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        // The CAMPUS is linked, not the request: there is no request-level contact to be ACTIVE.
        var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitRequestId == req.VisitRequestId);
        Assert.Equal(Registrant, instance.OperationalContactUserId);
        Assert.Equal(OperationalContactSources.RegistrantSelfMatch, instance.OperationalContactConfirmationSource);
        Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);
        // Every campus self-matched, so the gate never closes.
        Assert.Equal(VisitRequestStatuses.PendingApproval, req.Status);
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
            Form(Campus("HN", contactEmail: "someone-else@example.com")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitRequestId == req.VisitRequestId);
        Assert.Null(instance.OperationalContactUserId); // nobody holds the campus until B accepts
        Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, instance.Status);
        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, req.Status);
        var claim = await db.VisitRequestIdentityChanges.SingleAsync(x => x.VisitRequestId == req.VisitRequestId);
        Assert.Equal(instance.VisitInstanceId, claim.VisitInstanceId); // bound to the CAMPUS
        Assert.Equal(IdentityChangeKinds.InitialConfirmation, claim.ChangeKind);
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
            Form(Campus("HN", durationMin: 29)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));

        using var db2 = NewContext();
        using var tx2 = await db2.Database.BeginTransactionAsync();
        var ok = await Svc(db2).CreateV2Async(
            Form(Campus("HN", durationMin: 30)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        Assert.All(ok.CampusInstances, c => Assert.NotNull(c.FormDetail));
        await tx2.RollbackAsync();
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Create answers to the SAME scheduling floor as pending-edit and resubmit
    /// (<see cref="VisitMutationPolicy.MinScheduleLeadHours"/>), enforced here against the server's own
    /// clock (TC-TIME-01/02/03).
    ///
    /// <para>
    /// This check used to be "not in the past" and nothing more, so the 72 hours lived only in the
    /// browser: a direct API call — or a form filled in while the deadline slipped past — could file a
    /// visit for tomorrow morning and leave the Staff Leader no time to arrange it. Exactly on the
    /// boundary is inside the window; one minute short of it is not.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Start_inside_the_scheduling_lead_time_fails_and_exactly_on_it_passes()
    {
        RequireDb();

        var tooSoon = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-1);
        using (var db = NewContext())
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
                Form(Campus("HN") with { PlannedStartAt = tooSoon, PlannedEndAt = tooSoon.AddHours(2) }),
                Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));
            Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
            // The message carries what the caller needs to fix it: the rule, and the earliest date that
            // would be accepted. BusinessRuleException has no metadata slot, so both travel in the text.
            Assert.Contains(VisitMutationPolicy.MinScheduleLeadHours.ToString(), ex.Message);
            await tx.RollbackAsync();
        }

        var exactly = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours);
        using (var db = NewContext())
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var ok = await Svc(db).CreateV2Async(
                Form(Campus("HN") with { PlannedStartAt = exactly, PlannedEndAt = exactly.AddHours(2) }),
                Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
            Assert.Single(ok.CampusInstances);
            await tx.RollbackAsync();
        }
    }

    // ── Short-notice capability (PEMS_INTERNAL_SELF_CREATE_SHORT_NOTICE_72H plan §7.1) ──
    // allowShortNoticeCreate exempts the 72h floor ONLY — the future-only guard and every other
    // invariant (duration, end>start) stay in force regardless of the flag.

    [Theory]
    [InlineData(1)]              // now + 1 minute
    [InlineData(60)]             // now + 1 hour
    [InlineData(24 * 60)]        // now + 24h
    [InlineData(71 * 60 + 59)]   // now + 71h59m — inside the floor, refused without the capability
    [InlineData(72 * 60)]        // now + 72h — already legal even without the capability
    public async Task Capability_true_allows_a_start_inside_the_72h_floor(int minutesFromNow)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var start = Now.AddMinutes(minutesFromNow);
        var ok = await Svc(db).CreateV2Async(
            Form(Campus("HN") with { PlannedStartAt = start, PlannedEndAt = start.AddHours(2) }),
            Registrant, "STAFF_CREATED", Now, CancellationToken.None,
            hostProposals: null, allowShortNoticeCreate: true);
        Assert.Single(ok.CampusInstances);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Capability_true_still_refuses_a_past_start()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var start = Now.AddMinutes(-1);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
            Form(Campus("HN") with { PlannedStartAt = start, PlannedEndAt = start.AddHours(2) }),
            Registrant, "STAFF_CREATED", Now, CancellationToken.None,
            hostProposals: null, allowShortNoticeCreate: true));
        Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Capability_false_refuses_a_past_start_the_same_way()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var start = Now.AddMinutes(-1);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
            Form(Campus("HN") with { PlannedStartAt = start, PlannedEndAt = start.AddHours(2) }),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));
        Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    /// <summary>Start exactly equal to "now" is refused for every actor, capability or not (plan §BE-3).</summary>
    [Fact]
    public async Task Start_exactly_equal_to_now_fails_even_with_the_capability()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
            Form(Campus("HN") with { PlannedStartAt = Now, PlannedEndAt = Now.AddHours(2) }),
            Registrant, "STAFF_CREATED", Now, CancellationToken.None,
            hostProposals: null, allowShortNoticeCreate: true));
        Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Capability_true_does_not_bypass_end_after_start_or_minimum_duration()
    {
        RequireDb();
        var start = Now.AddHours(1);

        using (var db = NewContext())
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            // End == start.
            await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
                Form(Campus("HN") with { PlannedStartAt = start, PlannedEndAt = start }),
                Registrant, "STAFF_CREATED", Now, CancellationToken.None,
                hostProposals: null, allowShortNoticeCreate: true));
            await tx.RollbackAsync();
        }

        using (var db = NewContext())
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            // 29m59s — still one second short of the 30-minute floor.
            await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
                Form(Campus("HN") with { PlannedStartAt = start, PlannedEndAt = start.AddMinutes(29).AddSeconds(59) }),
                Registrant, "STAFF_CREATED", Now, CancellationToken.None,
                hostProposals: null, allowShortNoticeCreate: true));
            await tx.RollbackAsync();
        }

        using (var db = NewContext())
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            // Exactly 30m passes.
            var ok = await Svc(db).CreateV2Async(
                Form(Campus("HN") with { PlannedStartAt = start, PlannedEndAt = start.AddMinutes(30) }),
                Registrant, "STAFF_CREATED", Now, CancellationToken.None,
                hostProposals: null, allowShortNoticeCreate: true);
            Assert.Single(ok.CampusInstances);
            await tx.RollbackAsync();
        }
    }

    /// <summary>MC-01: every campus under 72h and in the future succeeds together under the capability.</summary>
    [Fact]
    public async Task Capability_true_allows_a_multi_campus_request_with_every_campus_under_72h()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var req = await Svc(db).CreateV2Async(
            Form(
                Campus("HN") with { PlannedStartAt = Now.AddHours(12), PlannedEndAt = Now.AddHours(14) },
                Campus("HCM") with { PlannedStartAt = Now.AddHours(24), PlannedEndAt = Now.AddHours(26) }),
            Registrant, "STAFF_CREATED", Now, CancellationToken.None,
            hostProposals: null, allowShortNoticeCreate: true);

        Assert.Equal(2, req.CampusInstances.Count);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task End_equals_start_fails()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
            Form(Campus("HN", durationMin: 0)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Duplicate_campus_fails()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Svc(db).CreateV2Async(
            Form(Campus("HN"), Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None));
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
            Form(Campus("HCM", delegation: "Đoàn HCM"), Campus("HN", delegation: "Đoàn HN")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.True(req.HasMixedCampusDetails);
        // Each campus keeps its OWN name, and the two differ — that is exactly what "mixed" means.
        var byCampus = req.CampusInstances.ToDictionary(c => c.CampusId, c => c.FormDetail!.DelegationName);
        Assert.Equal("Đoàn HN", byCampus[1UL]);
        Assert.Equal("Đoàn HCM", byCampus[2UL]);
        await tx.RollbackAsync();
    }
}
