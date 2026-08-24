using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Per-campus PENDING EDIT (plan §17): editing ONE campus that is still waiting for its decision,
/// on a request whose other campuses may already be decided.
///
/// <para>
/// The suite exists because of a shape the whole-request edit cannot serve at all. With HN ASSIGNED and
/// HCM still WAITING, <c>ApplyPendingEditAsync</c> is refused — it rewrites data every campus shares —
/// and before this path existed HCM had no way to be corrected, while the refused DN had resubmit and
/// the approved HN had safe-edit and amendments.
/// </para>
/// <para>
/// Every test therefore asserts two things: the target campus changed, and every sibling did NOT — not
/// its status, host, decision, revision or row version. That second half is the regression gate the
/// plan calls mandatory (§71), because a mutation that quietly reaches a sibling undoes a decision
/// somebody already took.
/// </para>
/// </summary>
public sealed class UpdatePendingVisitInstanceV2ServiceTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8;
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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    // ── Builders (same shapes as the whole-request suite, so the two read alike) ─────────────────

    private static CampusVisitFormDto Campus(
        string code, string delegation = "Đoàn Base", string purpose = "Thăm",
        string visitorName = "Guest A", int startOffsetDays = 20, int durationMinutes = 120)
    {
        var start = Now.AddDays(startOffsetDays);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(durationMinutes), delegation, "MEETING", null, purpose, "Nội dung",
            new List<VisitorDto> { new(visitorName, "Việt Nam", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null);
    }

    private static VisitRequestFormDataV2 CreateForm(params CampusVisitFormDto[] campuses)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());

    /// <summary>The per-campus payload: the campus's full content plus ITS OWN row version.</summary>
    private static CampusVisitEditV2Dto Content(
        VisitRequestCampus instance, CampusVisitFormDto content,
        ulong? operationalContactGuestMemberId = null)
        => new(instance.VisitInstanceId, instance.RowVersion,
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.Notes, OperationalContactClientMemberKey: null,
            OperationalContactGuestMemberId: operationalContactGuestMemberId);

    private static async Task RunAsync(
        Func<ApplicationDbContext, VisitRequestV2CreateService, VisitRequestV2EditService, Task> body)
    {
        RequireDb();
        using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await body(db, new VisitRequestV2CreateService(db),
                new VisitRequestV2EditService(db, new PEMS.Application.Delegations.Services.VisitRequestAggregateStatusService(db)));
        }
        finally { await tx.RollbackAsync(); }
    }

    private static VisitRequestCampus InstanceOf(VisitRequest r, string code)
        => r.CampusInstances.Single(c => c.CampusId == (code == "HN" ? 1UL : code == "HCM" ? 2UL : 3UL));

    /// <summary>Everything about a sibling that no other campus's edit may move.</summary>
    private sealed record SiblingState(
        string Status, ulong? Host, ulong? DecidedBy, DateTime? DecidedAt,
        int RowVersion, uint FormRevision, uint ApprovalRevision, DateTime Start);

    private static SiblingState Snapshot(VisitRequestCampus c) => new(
        c.Status, c.CurrentHostUserId, c.DecidedBy, c.DecidedAt,
        c.RowVersion, c.FormDetail!.FormRevision, c.FormDetail.ApprovalRevision, c.PlannedStartAt);

    /// <summary>
    /// Moves a campus to WAITING_REQUEST_APPROVAL the way a confirmed contact leaves it.
    ///
    /// <para>
    /// The contact is not decoration. <c>trg_visit_campuses_op_contact_guard_bi/bu</c> refuses any campus
    /// that is past WAITING_CONTACT_CONFIRMATION while <c>operational_contact_user_id</c> is NULL, so a
    /// fixture that only set the status was describing a row the database will not hold — and every test
    /// here that got far enough to flush died on it rather than on its own subject. Setting the two
    /// together is what a real confirmation does.
    /// </para>
    /// </summary>
    private static void MarkWaitingApproval(VisitRequestCampus c, ulong contactUserId)
    {
        c.OperationalContactUserId = contactUserId;
        c.OperationalContactConfirmedAt = Now;
        c.OperationalContactConfirmationSource = OperationalContactSources.EmailConfirmation;
        c.Status = VisitInstanceStatuses.WaitingRequestApproval;
    }

    /// <summary>Decides ONE campus in memory, the way an approval leaves it. No flush: these tests
    /// exercise the service's gating on the tracked state, not the DB triggers.</summary>
    private static void MarkAssigned(VisitRequestCampus c, ulong hostUserId)
    {
        // ASSIGNED is past confirmation too, so the same guard applies — and the sibling in the mixed
        // fixture IS flushed by the target campus's edit.
        if (c.OperationalContactUserId is null) MarkWaitingApproval(c, hostUserId);

        c.Status = VisitInstanceStatuses.Assigned;
        c.CurrentHostUserId = hostUserId;
        c.HostAssignedBy = hostUserId;
        c.HostAssignedAt = Now;
        c.DecidedBy = hostUserId;
        c.DecidedAt = Now;
        c.DecisionActorRole = "STAFF_LEADER";
        // The campus trigger wants the decision fields present together when a row lands on ASSIGNED,
        // and the target campus's own edit flushes this alongside it.
        c.DecisionSource = "STANDARD_CAMPUS_REVIEW";
        c.RowVersion += 1;
    }

    // ── The case the action exists for ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Editing_the_waiting_campus_of_a_mixed_request_leaves_every_sibling_untouched()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            // ── Reaching "HN decided, HCM waiting" takes three steps in this order, because two triggers
            //    fence it from both sides and only this sequence satisfies both. Doing it in one
            //    SaveChanges cannot work whatever EF's flush order turns out to be. ──
            //
            // 1. Both campuses confirm. The contact and the status must move TOGETHER (a campus at
            //    WAITING_CONTACT_CONFIRMATION may not hold a contact, and one past it may not lack one),
            //    and this is still allowed while the request is gated: the campus guard only consults
            //    the request's status for DECIDED campuses.
            MarkWaitingApproval(hn, Registrant);
            MarkWaitingApproval(hcm, Registrant);
            await db.SaveChangesAsync();

            // 2. Only now may the request leave the gate — trg_visit_requests_contact_gate_guard_bu
            //    counts campuses with no contact in the TABLE, so this fails if step 1 is still pending
            //    in the change tracker.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_requests SET status = {0} WHERE visit_request_id = {1}",
                VisitRequestStatuses.PendingApproval, r.VisitRequestId);

            // 3. HN is decided. This one DOES consult the request's stored status, which step 2 just
            //    lifted. It stays in the change tracker on purpose: the point of the test is that the
            //    edit of HCM flushes HN untouched.
            MarkAssigned(hn, hn.CoordinatorUserId!.Value);   // HN decided; HCM still waiting

            var hnBefore = Snapshot(hn);

            var result = await edit.ApplyInstancePendingEditAsync(
                r, hcm, Content(hcm, Campus("HCM", delegation: "Đoàn HCM đã sửa", visitorName: "Guest HCM")),
                Registrant, Now, actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default);

            // The target moved, and stayed WAITING — editing is not deciding (§30.1).
            Assert.Equal("Đoàn HCM đã sửa", hcm.FormDetail!.DelegationName);
            Assert.Equal(2u, hcm.FormDetail.FormRevision);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, hcm.Status);
            Assert.True(await db.VisitInstanceFormRevisionHistories.AnyAsync(h =>
                h.VisitInstanceId == hcm.VisitInstanceId
                && h.SourceType == FormRevisionSourceTypes.PendingEdit));
            Assert.True(await db.AuditLogs.AnyAsync(a =>
                a.VisitInstanceId == hcm.VisitInstanceId && a.Action == "UPDATE_PENDING_VISIT_INSTANCE_V2"));

            // The mandatory regression gate: NOTHING about HN moved.
            Assert.Equal(hnBefore, Snapshot(hn));

            // And the aggregate is recomputed FROM the campuses rather than assumed — one approved, one
            // waiting, so PARTIALLY_APPROVED. Naming a value here is how an edit of one campus used to
            // drag the whole request back to PENDING.
            Assert.Equal(VisitRequestStatuses.PartiallyApproved, r.Status);
            Assert.Equal(r.RowVersion, result.RequestRowVersion);
            Assert.Equal(r.VisitScope, result.VisitScope);
        });
    }

    [Theory]
    [InlineData(VisitInstanceStatuses.Assigned)]
    [InlineData(VisitInstanceStatuses.BeforeVisit)]
    [InlineData(VisitInstanceStatuses.Rejected)]
    public async Task A_campus_that_is_no_longer_waiting_cannot_be_edited_here(string status)
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            hn.Status = status;

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, Campus("HN", delegation: "Đoàn sửa")),
                    Registrant, Now, actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));
            Assert.Equal(VisitRequestErrorCodes.PendingCampusNotEditable, ex.ErrorCode);
        });
    }

    [Fact]
    public async Task The_campus_itself_cannot_be_swapped()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, Campus("HCM")),   // same instance, different campus
                    Registrant, Now, actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));
            Assert.Equal(VisitRequestErrorCodes.CampusSetImmutable, ex.ErrorCode);
        });
    }

    /// <summary>
    /// The INSTANCE's own version is the guard, deliberately not the request's: a sibling being decided
    /// bumps the request row, and that must not brick an edit of a campus nobody has touched.
    /// </summary>
    [Fact]
    public async Task A_stale_instance_row_version_is_a_stable_409()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var stale = Content(hn, Campus("HN", delegation: "Đoàn sửa")) with { ExpectedRowVersion = hn.RowVersion + 5 };

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, stale, Registrant, Now,
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));
            Assert.Equal(VisitRequestErrorCodes.InstanceVersionConflict, ex.ErrorCode);
        });
    }

    // ── The 72-hour registration floor, and who may pass it (§28–§32) ────────────────────────────

    [Fact]
    public async Task Content_only_edit_is_not_held_to_the_registration_floor()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            // The date arrives: 24 hours out, well inside the floor.
            var near = Now.AddHours(24);
            hn.PlannedStartAt = near;
            hn.PlannedEndAt = near.AddHours(2);

            var sameSchedule = Campus("HN", delegation: "Đoàn HN (sửa chính tả)")
                with { PlannedStartAt = near, PlannedEndAt = near.AddHours(2) };
            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, sameSchedule), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default);

            Assert.Equal("Đoàn HN (sửa chính tả)", hn.FormDetail!.DelegationName);
            Assert.Equal(near, hn.PlannedStartAt);
        });
    }

    [Fact]
    public async Task The_requester_side_cannot_move_a_schedule_inside_the_floor()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var originalStart = hn.PlannedStartAt;

            var tooSoon = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-1);
            var moved = Campus("HN") with { PlannedStartAt = tooSoon, PlannedEndAt = tooSoon.AddHours(2) };

            // Even with the flag set by hand: the override belongs to a RELATION, and this actor
            // does not have it.
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, moved), Registrant, Now,
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: true, approveAfterSaveRequested: false, default));
            Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
            Assert.Equal(originalStart, hn.PlannedStartAt);
        });
    }

    /// <summary>
    /// The campus's Staff Leader is asked to confirm, not refused: the floor exists so nobody approves a
    /// visit they have no time to prepare, and they are that person. Unconfirmed → a 409 the client
    /// answers by re-sending; confirmed → applied, with its own audit row naming both starts.
    /// </summary>
    [Fact]
    public async Task The_campus_leader_is_asked_to_confirm_and_then_may_pass_the_floor()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var leader = hn.CoordinatorUserId!.Value;
            var originalStart = hn.PlannedStartAt;

            var tooSoon = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-1);
            var moved = Campus("HN") with { PlannedStartAt = tooSoon, PlannedEndAt = tooSoon.AddHours(2) };

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, moved), leader, Now,
                    actorIsCampusLeader: true, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));
            Assert.Equal(VisitMutationErrorCodes.LeadTimeOverrideConfirmationRequired, ex.ErrorCode);
            Assert.Equal(originalStart, hn.PlannedStartAt); // nothing applied while the question is open

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, moved), leader, Now,
                actorIsCampusLeader: true, overrideLeadTimeConfirmed: true, approveAfterSaveRequested: false, default);

            Assert.Equal(tooSoon, hn.PlannedStartAt);
            // The override is a decision somebody took, so it gets its own audit row rather than being
            // folded into the field diff.
            Assert.True(await db.AuditLogs.AnyAsync(a =>
                a.VisitInstanceId == hn.VisitInstanceId && a.Action == VisitAuditActions.LeadTimeOverride));
            // Saving is not deciding: the campus is still waiting for the leader's answer.
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, hn.Status);
        });
    }

    [Fact]
    public async Task A_leader_who_does_not_move_the_schedule_writes_no_override_audit()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var leader = hn.CoordinatorUserId!.Value;

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, Campus("HN", delegation: "Đoàn HN (leader sửa)")), leader, Now,
                actorIsCampusLeader: true, overrideLeadTimeConfirmed: true, approveAfterSaveRequested: false, default);

            Assert.False(await db.AuditLogs.AnyAsync(a =>
                a.VisitInstanceId == hn.VisitInstanceId && a.Action == VisitAuditActions.LeadTimeOverride));
        });
    }

    [Fact]
    public async Task An_edit_that_changes_nothing_is_refused_rather_than_bumping_a_revision()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var revisionBefore = hn.FormDetail!.FormRevision;

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, Campus("HN")), Registrant, Now,
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));
            Assert.Equal(VisitFormV2ErrorCodes.PendingCampusNoContentChanges, ex.ErrorCode);
            Assert.Equal(revisionBefore, hn.FormDetail!.FormRevision);
        });
    }

    /// <summary>
    /// The service-level half of the "Lưu và duyệt" no-diff fix: when the SAME command also asked to
    /// approve, a no-diff edit is a silent no-op instead of a refusal — the caller (the command handler)
    /// still has an approval to run. No revision, no audit row, no row-version bump; the result reports
    /// exactly the request's CURRENT scope/mixed/row-version, not anything freshly computed.
    /// </summary>
    [Fact]
    public async Task An_edit_that_changes_nothing_is_a_silent_noop_when_an_approval_is_riding_with_it()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var revisionBefore = hn.FormDetail!.FormRevision;
            var rowVersionBefore = hn.RowVersion;
            var requestRowVersionBefore = r.RowVersion;

            var result = await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, Campus("HN")), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: true, default);

            Assert.Equal(revisionBefore, hn.FormDetail!.FormRevision);
            Assert.Equal(rowVersionBefore, hn.RowVersion);
            Assert.Equal(requestRowVersionBefore, r.RowVersion);
            Assert.Equal(r.RowVersion, result.RequestRowVersion);
            Assert.False(await db.VisitInstanceFormRevisionHistories.AnyAsync(h =>
                h.VisitInstanceId == hn.VisitInstanceId && h.SourceType == FormRevisionSourceTypes.PendingEdit));
        });
    }

    // ── Schedule-only revision integrity (Fix Group A) — Case A3: per-campus pending edit ──────────

    /// <summary>
    /// A per-campus schedule-only save must advance THIS campus's FormRevision by one and write a
    /// history row, exactly like a content edit — before the fix it silently bumped only RowVersion,
    /// leaving the form-revision chain unaware the schedule had ever moved. Members must never be
    /// relinked (nothing changed about them) and the sibling campus must stay completely untouched.
    /// </summary>
    [Fact]
    public async Task A_schedule_only_per_campus_edit_still_advances_form_revision_and_preserves_members()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            MarkWaitingApproval(hn, Registrant);
            MarkWaitingApproval(hcm, Registrant);
            await db.SaveChangesAsync();

            var hcmBefore = Snapshot(hcm);
            var hnMemberIdsBefore = hn.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList();
            var revisionBefore = hn.FormDetail!.FormRevision;
            var newStart = hn.PlannedStartAt.AddDays(1);
            var newEnd = hn.PlannedEndAt.AddDays(1);
            var scheduleOnly = Campus("HN") with { PlannedStartAt = newStart, PlannedEndAt = newEnd };

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, scheduleOnly), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default);

            Assert.Equal(revisionBefore + 1, hn.FormDetail!.FormRevision);
            Assert.Equal(newStart, hn.PlannedStartAt);
            Assert.Equal(newEnd, hn.PlannedEndAt);
            Assert.Equal("Đoàn Base", hn.FormDetail.DelegationName); // content untouched

            // Members were never relinked: same guest_member_id set, read straight from the DB.
            var memberIdsAfter = await db.VisitInstanceGuestMembers.AsNoTracking()
                .Where(l => l.VisitInstanceId == hn.VisitInstanceId)
                .Select(l => l.GuestMemberId).OrderBy(x => x).ToListAsync();
            Assert.Equal(hnMemberIdsBefore, memberIdsAfter);

            var revision = await db.VisitInstanceFormRevisionHistories
                .SingleAsync(h => h.VisitInstanceId == hn.VisitInstanceId
                                   && h.SourceType == FormRevisionSourceTypes.PendingEdit);
            Assert.Equal(hn.FormDetail.FormRevision, revision.FormRevision);
            Assert.Contains("Guest A", revision.SnapshotJson); // snapshot has the real members, never []

            // Sibling campus (HCM) is completely untouched.
            Assert.Equal(hcmBefore, Snapshot(hcm));
        });
    }

    // ── Patch 4 hardening (H4-1..H4-9) — the per-campus instance-edit path shares the SAME
    //    VisitRequestV2Canonical.CanonicalContent / VisitRequestV2EditOps.StageReplaceMembers gate as
    //    the whole-request pending-edit path (see UpdatePendingVisitRequestV2ServiceTests, which covers
    //    the full VN/Vietnam/KR/South Korea alias matrix); these two prove the same fixed gate holds
    //    here too, not a second copy of the logic. ──────────────────────────────────────────────────

    /// <summary>
    /// This endpoint saves exactly ONE campus and refuses outright when nothing about it actually moved
    /// ("Không có thay đổi nào để lưu cho cơ sở này." — see line ~709 of VisitRequestV2EditService),
    /// unlike the whole-request pending edit, which silently skips an untouched campus among several.
    /// An alias-only nationality respelling must land in THAT bucket — recognized as no real change —
    /// rather than being treated as content that changed. Before the H4-1 fix, the raw-text compare
    /// would have seen "Hàn Quốc" vs "South Korea" as different and let this proceed into a full member
    /// replace + FormRevision bump instead of surfacing the correct "nothing to save" refusal.
    /// </summary>
    [Fact]
    public async Task Member_nationality_alias_only_resubmit_is_recognized_as_no_real_change_on_the_instance_edit_path()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var initial = Campus("HN") with
            {
                Visitors = new List<VisitorDto> { new("Guest A", "Hàn Quốc", "Guest", "GuestOrg") },
            };
            var r = await create.CreateV2Async(CreateForm(initial), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            await db.SaveChangesAsync();
            var memberIdsBefore = hn.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList();
            var revisionBefore = hn.FormDetail!.FormRevision;

            var resubmitted = initial with
            {
                Visitors = new List<VisitorDto> { new("Guest A", "South Korea", "Guest", "GuestOrg") },
            };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, resubmitted), Registrant, Now,
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));
            Assert.Equal(VisitFormV2ErrorCodes.PendingCampusNoContentChanges, ex.ErrorCode);

            // Refused BEFORE anything was written: revision, members and stored spelling are untouched.
            Assert.Equal(revisionBefore, hn.FormDetail!.FormRevision);
            Assert.Equal(memberIdsBefore, hn.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList());
            Assert.False(await db.VisitInstanceFormRevisionHistories.AnyAsync(h =>
                h.VisitInstanceId == hn.VisitInstanceId && h.SourceType == FormRevisionSourceTypes.PendingEdit));
            var savedNationality = await db.VisitGuestMembers.AsNoTracking()
                .Where(m => memberIdsBefore.Contains(m.GuestMemberId)).Select(m => m.Nationality).SingleAsync();
            Assert.Equal("Hàn Quốc", savedNationality);
        });
    }

    [Fact]
    public async Task Instance_edit_of_purpose_leaves_an_unresolvable_legacy_member_nationality_untouched()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var memberId = hn.GuestMemberLinks.Single().GuestMemberId;
            var member = r.GuestMembers.Single(m => m.GuestMemberId == memberId);
            member.Nationality = "Legacy Unrecognized Value";
            await db.SaveChangesAsync();

            var edited = Campus("HN", purpose: "Mục đích mới") with
            {
                Visitors = new List<VisitorDto> { new("Guest A", "Legacy Unrecognized Value", "Guest", "GuestOrg") },
            };

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, edited), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default); // must not throw

            var newMemberId = hn.GuestMemberLinks.Single().GuestMemberId;
            var savedNationality = await db.VisitGuestMembers.AsNoTracking()
                .Where(m => m.GuestMemberId == newMemberId).Select(m => m.Nationality).SingleAsync();
            Assert.Equal("Legacy Unrecognized Value", savedNationality);
            Assert.Equal("Mục đích mới", hn.FormDetail!.Purpose);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // "Đầu mối hiện tại có nằm trong danh sách đoàn không?" — plan CanhIter3FixBug, pending-edit
    // lifecycle stage. Root gap: the relation is deliberately NOT part of CanonicalContent, so a
    // save that ONLY repoints who the contact is (member list otherwise untouched) used to compute
    // contentChanged=false, scheduleChanged=false and be refused as PendingCampusNoContentChanges —
    // silently dropping a real business change. Every test below proves the fix without going
    // through StageReplaceMembers/LinkMembers: the member list is never replaced by these saves.
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RelationOnly_outside_to_existing_member_registers_and_applies()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var guestId = hn.GuestMemberLinks.Single().GuestMemberId;
            Assert.Null(hn.FormDetail!.OperationalContactGuestMemberId);
            var revisionBefore = hn.FormDetail.FormRevision;
            var rowVersionBefore = hn.RowVersion;

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, Campus("HN"), operationalContactGuestMemberId: guestId), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default);

            Assert.Equal(guestId, hn.FormDetail.OperationalContactGuestMemberId);
            // Exactly +1 — a relationship-only save is still one save (plan §21), never zero, never two.
            Assert.Equal(revisionBefore + 1, hn.FormDetail.FormRevision);
            Assert.Equal(rowVersionBefore + 1, hn.RowVersion);
            // The member itself was never touched: still the same one row, same id.
            Assert.Equal(guestId, hn.GuestMemberLinks.Single().GuestMemberId);
            Assert.True(await db.AuditLogs.AnyAsync(a =>
                a.VisitInstanceId == hn.VisitInstanceId
                && a.Changes.Any(c => c.FieldName == $"instance[{hn.VisitInstanceId}].operational_contact_relation")));
            Assert.True(await db.VisitInstanceFormRevisionHistories.AnyAsync(h =>
                h.VisitInstanceId == hn.VisitInstanceId && h.FormRevision == hn.FormDetail.FormRevision));
        });
    }

    [Fact]
    public async Task RelationOnly_existing_member_to_outside_clears_the_relation_and_keeps_the_member()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var guestId = hn.GuestMemberLinks.Single().GuestMemberId;
            hn.FormDetail!.OperationalContactGuestMemberId = guestId;
            await db.SaveChangesAsync();
            var revisionBefore = hn.FormDetail.FormRevision;

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, Campus("HN"), operationalContactGuestMemberId: null), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default);

            Assert.Null(hn.FormDetail.OperationalContactGuestMemberId);
            Assert.Equal(revisionBefore + 1, hn.FormDetail.FormRevision);
            // The member still exists — clearing the relation is not deleting the person.
            Assert.Equal(guestId, hn.GuestMemberLinks.Single().GuestMemberId);
        });
    }

    [Fact]
    public async Task RelationOnly_switching_between_two_existing_members_moves_the_relation_only()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var twoVisitors = Campus("HN") with
            {
                Visitors = new List<VisitorDto>
                {
                    new("Guest A", "Việt Nam", "Guest", "GuestOrg"),
                    new("Guest B", "Việt Nam", "Guest", "GuestOrg"),
                },
            };
            var r = await create.CreateV2Async(CreateForm(twoVisitors), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var ids = hn.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList();
            var (guestA, guestB) = (ids[0], ids[1]);
            hn.FormDetail!.OperationalContactGuestMemberId = guestA;
            await db.SaveChangesAsync();

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, twoVisitors, operationalContactGuestMemberId: guestB), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default);

            Assert.Equal(guestB, hn.FormDetail.OperationalContactGuestMemberId);
            // Both rows survive, unchanged ids — this was never a member-list edit.
            Assert.Equal(ids, hn.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList());
        });
    }

    [Fact]
    public async Task RelationOnly_unchanged_relation_with_nothing_else_changed_is_refused_as_no_content_changes()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var guestId = hn.GuestMemberLinks.Single().GuestMemberId;
            hn.FormDetail!.OperationalContactGuestMemberId = guestId;
            await db.SaveChangesAsync();
            var revisionBefore = hn.FormDetail.FormRevision;

            // Echoes the SAME relation back — a reason/UI interaction alone is never a business change.
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, Campus("HN"), operationalContactGuestMemberId: guestId), Registrant, Now,
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));

            Assert.Equal(VisitFormV2ErrorCodes.PendingCampusNoContentChanges, ex.ErrorCode);
            Assert.Equal(revisionBefore, hn.FormDetail.FormRevision);
        });
    }

    [Fact]
    public async Task RelationOnly_a_nonexistent_member_id_is_refused_at_submit_with_zero_mutation()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var revisionBefore = hn.FormDetail!.FormRevision;

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, Campus("HN"), operationalContactGuestMemberId: 9_999_999UL), Registrant, Now,
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));

            Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
            Assert.Equal(revisionBefore, hn.FormDetail.FormRevision);
            Assert.Null(hn.FormDetail.OperationalContactGuestMemberId);
        });
    }

    [Fact]
    public async Task RelationOnly_a_sibling_campuss_member_id_is_refused_never_accepted_because_it_exists_elsewhere()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN"), Campus("HCM", visitorName: "Guest HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            MarkWaitingApproval(hn, Registrant);
            MarkWaitingApproval(hcm, Registrant);
            var hcmGuestId = hcm.GuestMemberLinks.Single().GuestMemberId;
            var revisionBefore = hn.FormDetail!.FormRevision;

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, Campus("HN"), operationalContactGuestMemberId: hcmGuestId), Registrant, Now,
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default));

            Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
            Assert.Equal(revisionBefore, hn.FormDetail.FormRevision);
            // HCM itself is completely unaffected by HN's refused edit.
            Assert.Equal(hcmGuestId, hcm.GuestMemberLinks.Single().GuestMemberId);
        });
    }

    [Fact]
    public async Task RelationOnly_never_mutates_the_contact_profile_or_partner_links()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            MarkWaitingApproval(hn, Registrant);
            var guestId = hn.GuestMemberLinks.Single().GuestMemberId;
            var profileBefore = (
                hn.FormDetail!.OperationalContactFullName, hn.FormDetail.OperationalContactOrganization,
                hn.FormDetail.OperationalContactJobTitle, hn.FormDetail.OperationalContactPhone,
                hn.FormDetail.OperationalContactEmail);

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, Campus("HN"), operationalContactGuestMemberId: guestId), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default);

            var profileAfter = (
                hn.FormDetail.OperationalContactFullName, hn.FormDetail.OperationalContactOrganization,
                hn.FormDetail.OperationalContactJobTitle, hn.FormDetail.OperationalContactPhone,
                hn.FormDetail.OperationalContactEmail);
            // The one thing this whole feature must never do: turn "which member is this" into
            // "redescribe the contact". The picked member's own name/org/job title never touch the
            // profile columns — picking "Guest A" must not make the contact BECOME "Guest A".
            Assert.Equal(profileBefore, profileAfter);
        });
    }

    [Fact]
    public async Task RelationOnly_a_sibling_campus_is_completely_unaffected()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN"), Campus("HCM", visitorName: "Guest HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            MarkWaitingApproval(hn, Registrant);
            MarkWaitingApproval(hcm, Registrant);
            await db.SaveChangesAsync();
            MarkAssigned(hcm, hcm.CoordinatorUserId!.Value);
            var hcmBefore = Snapshot(hcm);
            var guestId = hn.GuestMemberLinks.Single().GuestMemberId;

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, Campus("HN"), operationalContactGuestMemberId: guestId), Registrant, Now,
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, approveAfterSaveRequested: false, default);

            Assert.Equal(guestId, hn.FormDetail!.OperationalContactGuestMemberId);
            // The mandatory regression gate: nothing about HCM moved — status, host, decision,
            // revision, row version all byte-for-byte the same as before HN's relation-only save.
            Assert.Equal(hcmBefore, Snapshot(hcm));
        });
    }

    [Fact]
    public async Task RelationOnly_whole_request_edit_registers_a_relation_only_change_for_one_sibling_and_skips_the_other()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN"), Campus("HCM", visitorName: "Guest HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            MarkWaitingApproval(hn, Registrant);
            MarkWaitingApproval(hcm, Registrant);
            await db.SaveChangesAsync();
            var hnGuestId = hn.GuestMemberLinks.Single().GuestMemberId;
            var hcmRevisionBefore = hcm.FormDetail!.FormRevision;

            var editDto = new VisitRequestEditV2Dto(
                r.RowVersion,
                new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
                null,
                new List<CampusVisitEditV2Dto>
                {
                    Content(hn, Campus("HN"), operationalContactGuestMemberId: hnGuestId), // relation-only
                    Content(hcm, Campus("HCM", visitorName: "Guest HCM")),                 // byte-identical echo
                });

            await edit.ApplyPendingEditAsync(r, editDto, Registrant, Now, default);

            Assert.Equal(hnGuestId, hn.FormDetail!.OperationalContactGuestMemberId);
            // HCM's echo carried no change of any kind (content, schedule or relation) — it must be
            // skipped exactly like before this fix, never given a spurious revision bump.
            Assert.Equal(hcmRevisionBefore, hcm.FormDetail!.FormRevision);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // TASK C (plan CanhIter3FixBug, this pass) — dedicated concurrency test for the relation-only
    // mutation. Two independent DbContexts/connections race for the SAME instance row, both starting
    // from the SAME RowVersion; exactly one must win. Written against the REAL lock the relation path
    // already inherits (AssertCurrentInstanceVersionAsync's SELECT ... FOR UPDATE) — not a fake stand-
    // in — by holding that row's lock in one transaction while the real ApplyInstancePendingEditAsync
    // call races against it in a second, concurrently-running Task, matching the exact pattern
    // ParentLifecycleCancelConcurrencyTests already uses for the same underlying guard.
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(600);

    /// <summary>A committed (non-rollback) single-campus request, WAITING_REQUEST_APPROVAL with two
    /// real guest rows — real commits are required here because the two racing tasks below need
    /// INDEPENDENT connections/transactions, which a single shared, rolled-back RunAsync cannot give.</summary>
    private static async Task<(ulong RequestId, ulong InstanceId, ulong GuestA, ulong GuestB)> SeedRaceableCampusAsync()
    {
        var now = DateTime.Now;
        ulong requestId;
        using (var db = NewContext())
        {
            var create = new VisitRequestV2CreateService(db);
            var twoVisitors = Campus("HN") with
            {
                Visitors = new List<VisitorDto>
                {
                    new("Guest A", "Việt Nam", "Guest", "GuestOrg"),
                    new("Guest B", "Việt Nam", "Guest", "GuestOrg"),
                },
            };
            var r = await create.CreateV2Async(CreateForm(twoVisitors), Registrant, "VISITOR_SUBMITTED", now, default);
            requestId = r.VisitRequestId;
        }
        ulong instanceId, guestA, guestB;
        using (var db = NewContext())
        {
            var r = await db.VisitRequests.Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
                .Include(v => v.CampusInstances).ThenInclude(c => c.GuestMemberLinks)
                .SingleAsync(v => v.VisitRequestId == requestId);
            var hn = r.CampusInstances.Single();
            MarkWaitingApproval(hn, Registrant);
            await db.SaveChangesAsync();
            instanceId = hn.VisitInstanceId;
            var ids = hn.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList();
            (guestA, guestB) = (ids[0], ids[1]);
        }
        return (requestId, instanceId, guestA, guestB);
    }

    /// <summary>Manually acquires the SAME row lock ApplyInstancePendingEditAsync's own
    /// AssertCurrentInstanceVersionAsync would, holds it for <paramref name="hold"/>, then performs a
    /// REAL relation-only mutation (outside → Guest A) and commits — simulating a genuinely concurrent
    /// writer that reaches the row first.</summary>
    private static async Task HoldLockThenApplyRelationAsync(
        ulong instanceId, ulong guestId, TaskCompletionSource holding, TimeSpan hold)
    {
        using var db = NewContext();
        var locks = new MySqlUserMutationLockService(db);
        await using var tx = await db.Database.BeginTransactionAsync();

        await locks.LockVisitRequestCampusesAsync(new[] { instanceId }, CancellationToken.None);
        holding.SetResult();
        await Task.Delay(hold);

        var instance = await db.VisitRequestCampuses.Include(c => c.FormDetail)
            .SingleAsync(c => c.VisitInstanceId == instanceId);
        instance.FormDetail!.OperationalContactGuestMemberId = guestId;
        instance.FormDetail.FormRevision += 1;
        instance.FormDetail.RowVersion += 1;
        instance.RowVersion += 1;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    [Fact]
    public async Task RelationOnly_two_concurrent_relation_changes_from_the_same_RowVersion_exactly_one_wins()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var guestA, var guestB) = await SeedRaceableCampusAsync();

            int rowVersionBefore;
            uint formRevisionBefore;
            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.Include(c => c.FormDetail).AsNoTracking()
                    .SingleAsync(c => c.VisitInstanceId == instanceId);
                rowVersionBefore = instance.RowVersion;
                formRevisionBefore = instance.FormDetail!.FormRevision;
                Assert.Null(instance.FormDetail.OperationalContactGuestMemberId);
            }

            // A: holds the row lock, then applies outside → Guest A (a REAL relation-only mutation).
            var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var taskA = Task.Run(() => HoldLockThenApplyRelationAsync(instanceId, guestA, holding, Hold));

            // B: the path actually under test — waits for A to be holding the lock, then races in with
            // outside → Guest B, from the SAME original RowVersion. It must BLOCK on A's lock, then
            // lose: by the time it reads the row, A has already committed the bump.
            var taskB = Task.Run(async () =>
            {
                await holding.Task;
                using var db = NewContext();
                var request = await db.VisitRequests.Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
                    .Include(v => v.CampusInstances).ThenInclude(c => c.GuestMemberLinks)
                    .Include(v => v.GuestMembers)
                    .SingleAsync(v => v.VisitRequestId == requestId);
                var instance = request.CampusInstances.Single(c => c.VisitInstanceId == instanceId);
                var edit = new VisitRequestV2EditService(db, new VisitRequestAggregateStatusService(db));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var ex = await Record.ExceptionAsync(() => edit.ApplyInstancePendingEditAsync(
                    request, instance, Content(instance, Campus("HN") with
                    {
                        Visitors = new List<VisitorDto>
                        {
                            new("Guest A", "Việt Nam", "Guest", "GuestOrg"),
                            new("Guest B", "Việt Nam", "Guest", "GuestOrg"),
                        },
                    }, operationalContactGuestMemberId: guestB) with { ExpectedRowVersion = rowVersionBefore },
                    Registrant, DateTime.Now, actorIsCampusLeader: false, overrideLeadTimeConfirmed: false,
                    approveAfterSaveRequested: false, default));
                sw.Stop();
                return (Exception: ex, Waited: sw.Elapsed);
            });

            await taskA.WaitAsync(LockWait);
            var (ex, waited) = await taskB.WaitAsync(LockWait);

            // Proves B actually CONTENDED on A's lock rather than racing past it unlocked.
            Assert.True(waited >= TimeSpan.FromMilliseconds(300),
                $"B returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the row lock A held for {Hold.TotalMilliseconds:F0} ms.");
            Assert.NotNull(ex);
            Assert.IsType<ConflictException>(ex);
            Assert.Equal(VisitRequestErrorCodes.InstanceVersionConflict, ((ConflictException)ex!).ErrorCode);

            // Exactly one write landed — A's — and it landed exactly once.
            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.Include(c => c.FormDetail).Include(c => c.GuestMemberLinks)
                    .AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceId);
                Assert.Equal(guestA, instance.FormDetail!.OperationalContactGuestMemberId);
                Assert.Equal(formRevisionBefore + 1, instance.FormDetail.FormRevision);
                Assert.Equal(rowVersionBefore + 1, instance.RowVersion);
                // No duplicate revision-history row, no lost/duplicated audit, member ids/profile untouched.
                Assert.Equal(2, instance.GuestMemberLinks.Count);
                Assert.Equal("Op Contact", instance.FormDetail.OperationalContactFullName);
            }
            using (var db = NewContext())
            {
                // A's stand-in mutation (HoldLockThenApplyRelationAsync) deliberately does not write a
                // revision-history row itself — it exists only to hold the real lock a genuine
                // concurrent writer would. What this proves is B's half: its LOST race produced no
                // history row of its own, so the table carries none for this bump at all.
                var historyCount = await db.VisitInstanceFormRevisionHistories
                    .CountAsync(h => h.VisitInstanceId == instanceId && h.FormRevision == formRevisionBefore + 1);
                Assert.Equal(0, historyCount); // B's failed attempt wrote no history row at all
            }
        }
        finally { await CleanupRaceAsync(requestId); }
    }

    private static async Task CleanupRaceAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }
}
