using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
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
            new List<VisitorDto> { new(visitorName, "VN", "Guest", "GuestOrg") },
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
    private static CampusVisitEditV2Dto Content(VisitRequestCampus instance, CampusVisitFormDto content)
        => new(instance.VisitInstanceId, instance.RowVersion,
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.Notes);

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
                Registrant, Now, actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, default);

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
                    Registrant, Now, actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, default));
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
                    Registrant, Now, actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, default));
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
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, default));
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
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, default);

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
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: true, default));
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
                    actorIsCampusLeader: true, overrideLeadTimeConfirmed: false, default));
            Assert.Equal(VisitMutationErrorCodes.LeadTimeOverrideConfirmationRequired, ex.ErrorCode);
            Assert.Equal(originalStart, hn.PlannedStartAt); // nothing applied while the question is open

            await edit.ApplyInstancePendingEditAsync(
                r, hn, Content(hn, moved), leader, Now,
                actorIsCampusLeader: true, overrideLeadTimeConfirmed: true, default);

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
                actorIsCampusLeader: true, overrideLeadTimeConfirmed: true, default);

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

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyInstancePendingEditAsync(
                    r, hn, Content(hn, Campus("HN")), Registrant, Now,
                    actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, default));
            Assert.Equal(revisionBefore, hn.FormDetail!.FormRevision);
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
                actorIsCampusLeader: false, overrideLeadTimeConfirmed: false, default);

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
}
