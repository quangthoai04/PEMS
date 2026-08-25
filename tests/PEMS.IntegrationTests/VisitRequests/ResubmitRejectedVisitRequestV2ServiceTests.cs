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
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Resubmit-v2 SERVICE matrix (Phase C-2). Each test creates a v2 aggregate, drives it to fully-REJECTED
/// through the SAME order as the real flow (campus decisions first under the pending parent, then the parent
/// flips REJECTED — the campus triggers see legit transitions), then resubmits — all inside one rolled-back
/// transaction, so <c>pems_pr3_test</c> keeps v2_requests = 0. Covers: all-rejected-only gate, instance IDs
/// kept, campus set fixed, rejection history preserved, re-route to current leader, one-winner concurrency
/// (stale version → 409), per-campus canonical read-back, decision-field re-initialisation and revisions.
/// </summary>
public sealed class ResubmitRejectedVisitRequestV2ServiceTests
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

    private static CampusVisitFormDto Campus(string code, string delegation = "Đoàn Base",
        string visitorName = "Guest A", int startOffsetDays = 20, int durationMinutes = 120,
        // Defaults to the REGISTRANT'S own address so the campus self-matches at submit — confirmed with
        // no invitation, gate open. The tests here reject and resubmit campuses, and neither is possible
        // behind the confirmation gate. Cases that need a distinct contact pass one explicitly.
        string? contactEmail = null)
    {
        var start = Now.AddDays(startOffsetDays);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(durationMinutes), delegation, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new(visitorName, "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", contactEmail ?? V2SeedActor.Email(Registrant)),
            "EN", null, "DECLINED", null, null);
    }

    private static VisitRequestFormDataV2 CreateForm(params CampusVisitFormDto[] campuses)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());

    private static CampusVisitEditV2Dto Slot(
        VisitRequestCampus instance, CampusVisitFormDto content, ulong? overrideId = null,
        string? operationalContactClientMemberKey = null, ulong? operationalContactGuestMemberId = null)
        => new(overrideId ?? instance.VisitInstanceId, instance.RowVersion,
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.Notes, operationalContactClientMemberKey, operationalContactGuestMemberId);

    private static VisitRequestEditV2Dto Edit(VisitRequest request, params CampusVisitEditV2Dto[] campuses)
        => new(request.RowVersion,
            new RegistrantInputV2(request.RegistrantFullName, request.RegistrantNationality ?? "VN",
                request.RegistrantOrganization, request.RegistrantJobTitle ?? "Job",
                request.RegistrantPhone ?? "+8491", request.RegistrantEmail),
            request.PartnerId, campuses.ToList());

    private static VisitRequestCampus InstanceOf(VisitRequest r, string code)
        => r.CampusInstances.Single(c => c.CampusId == (code == "HN" ? 1UL : code == "HCM" ? 2UL : 3UL));

    /// <summary>Drives a freshly created request to fully-REJECTED using the real transition order.</summary>
    private static async Task RejectAllAsync(ApplicationDbContext db, VisitRequest r)
    {
        foreach (var instance in r.CampusInstances)
        {
            instance.Status = VisitInstanceStatuses.Rejected;
            instance.DecidedBy = instance.CoordinatorUserId;
            instance.DecidedAt = Now;
            instance.DecisionActorRole = "STAFF_LEADER";
            instance.DecisionSource = "STANDARD_CAMPUS_REVIEW";
            instance.DecisionNote = "Không phù hợp lịch cơ sở";
            instance.RowVersion += 1;
        }
        await db.SaveChangesAsync(); // campus decisions land under the still-pending parent
        r.Status = VisitRequestStatuses.Rejected;
        r.RowVersion += 1;
        await db.SaveChangesAsync(); // then the parent flips REJECTED
    }

    private static async Task RunAsync(
        Func<ApplicationDbContext, VisitRequestV2CreateService, VisitRequestV2EditService, Task> body)
    {
        RequireDb();
        using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await body(db, new VisitRequestV2CreateService(db), new VisitRequestV2EditService(db, new PEMS.Application.Delegations.Services.VisitRequestAggregateStatusService(db)));
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ── Matrix ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resubmit_keeps_instance_ids_clears_decisions_reroutes_and_preserves_history()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN", delegation: "Đoàn X"), Campus("HCM", delegation: "Đoàn Y")),
                Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            var hnId = hn.VisitInstanceId;
            var hcmId = hcm.VisitInstanceId;
            await RejectAllAsync(db, r);

            var result = await edit.ApplyResubmitAsync(r, Edit(r,
                Slot(hn, Campus("HN", delegation: "Đoàn X sửa", visitorName: "Guest Mới")),
                Slot(hcm, Campus("HCM", delegation: "Đoàn X sửa", visitorName: "Guest Mới"))), Registrant, Now, default);

            // Instance IDs KEPT (no delete/recreate).
            Assert.Equal(hnId, InstanceOf(r, "HN").VisitInstanceId);
            Assert.Equal(hcmId, InstanceOf(r, "HCM").VisitInstanceId);

            // Parent pending again; every instance WAITING with decisions cleared and current leader routed.
            Assert.Equal(VisitRequestStatuses.PendingApproval, r.Status);
            Assert.Equal(1u, r.ResubmissionCount);
            foreach (var instance in r.CampusInstances)
            {
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);
                Assert.Null(instance.DecidedBy);
                Assert.Null(instance.DecisionNote);
                Assert.Null(instance.DecisionActorRole);
                Assert.Null(instance.CurrentHostUserId);
                Assert.NotNull(instance.CoordinatorUserId); // re-routed to the CURRENT Staff Leader
            }

            // Canonical read-back per campus: the ACTIVE per-instance detail carries the resubmitted content.
            Assert.Equal("Đoàn X sửa", hn.FormDetail!.DelegationName);
            Assert.Equal("Đoàn X sửa", hcm.FormDetail!.DelegationName);
            Assert.Equal(2u, hn.FormDetail.FormRevision);
            Assert.False(result.HasMixed); // both campuses identical now
            Assert.Equal(VisitScopes.MultiCampus, result.VisitScope);

            // History preserved: CREATE revisions still there, RESUBMIT revisions added, audit snapshot kept.
            var hnRevisions = await db.VisitInstanceFormRevisionHistories
                .Where(h => h.VisitInstanceId == hnId).Select(h => h.SourceType).ToListAsync();
            Assert.Contains("CREATE", hnRevisions);
            Assert.Contains("RESUBMIT", hnRevisions);
            var auditChanges = await db.AuditLogs
                .Where(a => a.VisitRequestId == r.VisitRequestId && a.Action == "RESUBMIT_REJECTED_VISIT_REQUEST_V2")
                .SelectMany(a => a.Changes)
                .ToListAsync();
            var decisionSnapshot = auditChanges.Single(c => c.FieldName == "campus_decisions_before_resubmit_json");
            // Old decisions snapshotted (JSON escapes non-ASCII, so assert on the stable structural parts).
            Assert.Contains("\"oldStatus\":\"REJECTED\"", decisionSnapshot.OldValueText);
            Assert.Contains("\"decisionNote\":", decisionSnapshot.OldValueText);
            Assert.Contains($"\"visitInstanceId\":{hnId}", decisionSnapshot.OldValueText);
            Assert.Contains(auditChanges, c => c.FieldName == "resubmission_count" && c.NewValueText == "1");
            Assert.True(await db.VisitRequestRevisionHistories.AnyAsync(h =>
                h.VisitRequestId == r.VisitRequestId && h.SourceType == "RESUBMIT"));
        });
    }

    [Fact]
    public async Task Partially_rejected_request_cannot_resubmit()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            // Reject ONLY HCM (in-memory + flush); HN stays WAITING; parent stays PENDING.
            var hcm = InstanceOf(r, "HCM");
            hcm.Status = VisitInstanceStatuses.Rejected;
            hcm.DecidedBy = hcm.CoordinatorUserId;
            hcm.DecidedAt = Now;
            hcm.DecisionActorRole = "STAFF_LEADER";
            hcm.DecisionSource = "STANDARD_CAMPUS_REVIEW";
            hcm.DecisionNote = "Từ chối một cơ sở";
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, Edit(r,
                    Slot(InstanceOf(r, "HN"), Campus("HN")),
                    Slot(hcm, Campus("HCM"))), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.VisitRequestNotResubmittable, ex.ErrorCode);
        });
    }

    [Fact]
    public async Task Campus_set_change_is_rejected_both_directions()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            await RejectAllAsync(db, r);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");

            // Dropping a campus → rejected.
            var ex1 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, Campus("HN"))), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.ResubmitCampusListChanged, ex1.ErrorCode);

            // Adding a campus (slot without instance id) → rejected.
            var addSlot = new CampusVisitEditV2Dto(null, null,
                "DN", Now.AddDays(20), Now.AddDays(20).AddMinutes(120),
                "Đoàn Base", "MEETING", null, "Thăm", "Nội dung",
                new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
                new List<SupportTeamMemberDto>(),
                new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
                "EN", null, "DECLINED", null);
            var ex2 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, Campus("HN")), Slot(hcm, Campus("HCM")), addSlot),
                    Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.ResubmitCampusListChanged, ex2.ErrorCode);

            // Swapping an instance to a different campus code → rejected.
            var ex3 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, Campus("HCM")), Slot(hcm, Campus("HN"))),
                    Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.ResubmitCampusListChanged, ex3.ErrorCode);
        });
    }

    [Fact]
    public async Task Stale_versions_are_stable_409_one_winner()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            await RejectAllAsync(db, r);
            var hn = InstanceOf(r, "HN");

            // "Concurrent" resubmit deterministic proxy: the loser carries the PRE-winner request version.
            var loserPayload = Edit(r, Slot(hn, Campus("HN"))) with { ExpectedRequestRowVersion = r.RowVersion - 1 };
            var ex1 = await Assert.ThrowsAsync<ConflictException>(() =>
                edit.ApplyResubmitAsync(r, loserPayload, Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.RequestVersionConflict, ex1.ErrorCode);

            // Stale INSTANCE version with a correct request version.
            var staleInstance = Slot(hn, Campus("HN")) with { ExpectedRowVersion = hn.RowVersion - 1 };
            var ex2 = await Assert.ThrowsAsync<ConflictException>(() =>
                edit.ApplyResubmitAsync(r, Edit(r, staleInstance), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InstanceVersionConflict, ex2.ErrorCode);

            // The correct-version resubmit then WINS.
            var win = await edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, Campus("HN"))), Registrant, Now, default);
            Assert.Equal(VisitRequestStatuses.PendingApproval, r.Status);
            Assert.True(win.RequestRowVersion > 0);

            // And a REPLAY of the same (now-consumed) resubmit intent loses with a stable 409 — one winner.
            var replay = Edit(r, Slot(hn, Campus("HN"))) with { ExpectedRequestRowVersion = win.RequestRowVersion - 1 };
            var ex3 = await Assert.ThrowsAsync<ConflictException>(() =>
                edit.ApplyResubmitAsync(r, replay, Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.RequestVersionConflict, ex3.ErrorCode);
        });
    }

    /// <summary>
    /// A resubmit proposes a NEW schedule, and that schedule answers to the shared SCHEDULING floor —
    /// <see cref="VisitMutationPolicy.MinScheduleLeadHours"/>, the same one create and pending-edit use,
    /// measured from the moment of the resubmit rather than from when the request was first filed.
    ///
    /// <para>
    /// Asserted against the policy constant rather than a number copied beside it. It used to point at
    /// <c>RequiredLeadHours</c>, which is a different rule: how late the ACTION stays open, not how soon
    /// the visit may be. Reading the same constant for both quietly let a request be resubmitted into a
    /// slot it could never have been created for.
    /// </para>
    /// </summary>
    [Fact]
    public async Task New_schedule_inside_the_lead_time_is_rejected()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            await RejectAllAsync(db, r);
            var hn = InstanceOf(r, "HN");

            var tooSoon = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-1);
            var soon = Campus("HN") with { PlannedStartAt = tooSoon, PlannedEndAt = tooSoon.AddHours(2) };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, soon)), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
        });
    }

    /// <summary>
    /// The other half of the boundary: exactly on the lead time is INSIDE the window. Without this,
    /// nothing would catch the rule quietly becoming "strictly more than N hours".
    /// </summary>
    [Fact]
    public async Task New_schedule_exactly_on_the_lead_time_is_accepted()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            await RejectAllAsync(db, r);
            var hn = InstanceOf(r, "HN");

            var exactly = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours);
            var slot = Campus("HN") with { PlannedStartAt = exactly, PlannedEndAt = exactly.AddHours(2) };
            var result = await edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, slot)), Registrant, Now, default);
            Assert.NotNull(result);
        });
    }

    /// <summary>
    /// The rule that makes resubmit different from every other check here: validity is re-decided at the
    /// moment of the RESUBMIT, not inherited from when the request was filed (TC-TIME-05).
    ///
    /// <para>
    /// The schedule below never changes. It was comfortably valid when the request was created, sat
    /// through a rejection, and by the time the registrant comes back the visit is 71 hours away — so
    /// the unchanged content is now refused, and the registrant has to propose a new date. Nothing about
    /// the payload says this; only the clock does.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Unchanged_schedule_is_re_validated_against_the_time_of_the_resubmit()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var content = Campus("HN");
            var r = await create.CreateV2Async(CreateForm(content), Registrant, "VISITOR_SUBMITTED", Now, default);
            await RejectAllAsync(db, r);
            var hn = InstanceOf(r, "HN");

            // The registrant comes back with the visit 71 hours away — valid at create time, too late now.
            var lateResubmitAt = content.PlannedStartAt
                .AddHours(-(VisitMutationPolicy.MinScheduleLeadHours - 1));
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, content)), Registrant, lateResubmitAt, default));
            Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);

            // …and the same untouched content still goes through while there is enough notice left.
            var earlyResubmitAt = content.PlannedStartAt
                .AddHours(-(VisitMutationPolicy.MinScheduleLeadHours + 1));
            var ok = await edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, content)), Registrant, earlyResubmitAt, default);
            Assert.NotNull(ok);
        });
    }

    [Fact]
    public async Task Immutable_contact_email_still_enforced_on_resubmit()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            await RejectAllAsync(db, r);

            var bad = Edit(r, Slot(InstanceOf(r, "HN"), Campus("HN"))) with
            {
                CampusVisits = new List<CampusVisitEditV2Dto>
                {
                    // A rejected campus keeps the person who confirmed it: resubmitting is a second
                    // attempt at the same visit, not a way to hand it to a different address.
                    Slot(InstanceOf(r, "HN"), Campus("HN", contactEmail: "swapped@example.com")),
                },
            };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, bad, Registrant, Now, default));
            Assert.Equal("IMMUTABLE_CONTACT_IDENTITY", ex.ErrorCode);
        });
    }

    /// <summary>
    /// Resubmit rewrites every campus's content wholesale, which makes it the other path a contact edit
    /// could arrive through — so the same guard runs here, refusing the four detail fields under their
    /// own code (repair v3 §2.2, §17 "Request Edit backend").
    /// </summary>
    [Fact]
    public async Task Contact_details_cannot_be_edited_through_a_resubmit_either()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var instanceId = InstanceOf(r, "HN").VisitInstanceId;
            await RejectAllAsync(db, r);

            var bad = Edit(r, Slot(InstanceOf(r, "HN"), Campus("HN"))) with
            {
                CampusVisits = new List<CampusVisitEditV2Dto>
                {
                    // Same address, different details — the case that used to be allowed through.
                    Slot(InstanceOf(r, "HN"), Campus("HN") with
                    {
                        OperationalContact = new ContactPointDto(
                            "Tên Khác", "OpOrg", "Trưởng phòng Hợp tác", "+84987654321",
                            V2SeedActor.Email(Registrant)),
                    }),
                },
            };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, bad, Registrant, Now, default));
            Assert.Equal("IMMUTABLE_CONTACT_PROFILE", ex.ErrorCode);

            // Refused during validation: the request is still REJECTED and the contact still says what
            // it said. A half-applied resubmit would leave a request describing a visit nobody agreed to.
            var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceId);
            Assert.Equal("Op Contact", detail.OperationalContactFullName);
            Assert.Equal(VisitRequestStatuses.Rejected, r.Status);
        });
    }

    /// <summary>
    /// Patch 4 hardening H4-9 on the resubmit path: resubmit ALWAYS structurally rewrites every
    /// campus's member set (StageReplaceMembers runs unconditionally here, unlike pending-edit — see
    /// ApplyResubmitAsync's own comment on why it always writes a revision), so an unresolvable legacy
    /// nationality sitting on a member NOBODY touched must still pass through via MemberContentIndex —
    /// otherwise a request rejected years ago, before this patch existed, could never be resubmitted at
    /// all without first somehow fixing a nationality field the resubmit screen never asked about.
    /// </summary>
    [Fact]
    public async Task Resubmit_is_not_blocked_by_an_untouched_unresolvable_legacy_member_nationality()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var memberId = hn.GuestMemberLinks.Single().GuestMemberId;
            var member = r.GuestMembers.Single(m => m.GuestMemberId == memberId);
            member.Nationality = "Legacy Unrecognized Value";
            await db.SaveChangesAsync();
            await RejectAllAsync(db, r);

            var resubmitted = Campus("HN", delegation: "Đoàn X sửa") with
            {
                Visitors = new List<VisitorDto> { new("Guest A", "Legacy Unrecognized Value", "Guest", "GuestOrg") },
            };

            // Must NOT throw: the member's own content is echoed back byte-identical.
            await edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, resubmitted)), Registrant, Now, default);

            var newMemberId = InstanceOf(r, "HN").GuestMemberLinks.Single().GuestMemberId;
            var savedNationality = await db.VisitGuestMembers.AsNoTracking()
                .Where(m => m.GuestMemberId == newMemberId).Select(m => m.Nationality).SingleAsync();
            Assert.Equal("Legacy Unrecognized Value", savedNationality);
            Assert.Equal(VisitRequestStatuses.PendingApproval, r.Status);
        });
    }

    // ── RESUBMIT-SEC-01..06 (operational-contact consistency fix): Resubmit always full-rewrites every
    // campus's members (copy-on-write), so it goes through the SAME continuity proof Pending Edit's
    // content-changed branch uses — the live-request table (LiveRequestRelationError), never Pending
    // Edit's own vocabulary. ──

    [Fact]
    public async Task ResubmitSec01_introducing_a_relation_from_unlinked_is_rejected_with_zero_mutation()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var guestId = hn.GuestMemberLinks.Single().GuestMemberId;
            await RejectAllAsync(db, r);
            var revisionBefore = hn.FormDetail!.FormRevision;

            var resubmitted = Campus("HN", delegation: "Đoàn X sửa") with
            {
                Visitors = new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg", null, "k-a", guestId) },
            };

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(
                    r, Edit(r, Slot(hn, resubmitted, operationalContactClientMemberKey: "k-a")),
                    Registrant, Now, default));

            Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
            Assert.Null(hn.FormDetail.OperationalContactGuestMemberId);
            Assert.Equal(revisionBefore, hn.FormDetail.FormRevision);
            // The per-CAMPUS state this check guards is untouched (this check runs before that campus's
            // own ApplyFormDetail/StageReplaceMembers/status flip). The PARENT request's status legitimately
            // does flip to PENDING_APPROVAL earlier in the same call (Phase 1, required by the campus
            // trigger's own precondition before ANY instance can leave REJECTED) — that flush is real but
            // is not this test's concern, and is itself rolled back by the caller's transaction on throw.
            Assert.Equal(VisitInstanceStatuses.Rejected, hn.Status);
        });
    }

    [Fact]
    public async Task ResubmitSec02_repointing_kim_to_moon_is_rejected()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var twoVisitors = Campus("HN") with
            {
                Visitors = new List<VisitorDto>
                {
                    new("Kim", "VN", "Director", "Org"),
                    new("Moon", "VN", "Director", "Org"),
                },
            };
            var r = await create.CreateV2Async(CreateForm(twoVisitors), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var kimId = r.GuestMembers.Single(m => m.FullName == "Kim").GuestMemberId;
            var moonId = r.GuestMembers.Single(m => m.FullName == "Moon").GuestMemberId;
            hn.FormDetail!.OperationalContactGuestMemberId = kimId;
            await db.SaveChangesAsync();
            await RejectAllAsync(db, r);

            var resubmitted = Campus("HN", delegation: "Đoàn X sửa") with
            {
                Visitors = new List<VisitorDto>
                {
                    new("Kim", "VN", "Director", "Org", null, "k-kim", kimId),
                    new("Moon", "VN", "Director", "Org", null, "k-moon", moonId),
                },
            };

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(
                    r, Edit(r, Slot(hn, resubmitted, operationalContactClientMemberKey: "k-moon")),
                    Registrant, Now, default));

            Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
            Assert.Equal(kimId, hn.FormDetail.OperationalContactGuestMemberId);
        });
    }

    [Fact]
    public async Task ResubmitSec03_a_guestmemberid_from_another_request_never_satisfies_local_continuity()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var other = await create.CreateV2Async(
                CreateForm(Campus("HN", visitorName: "Foreign Guest")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var foreignId = other.GuestMembers.Single().GuestMemberId;

            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var localId = hn.GuestMemberLinks.Single().GuestMemberId;
            hn.FormDetail!.OperationalContactGuestMemberId = localId;
            await db.SaveChangesAsync();
            await RejectAllAsync(db, r);

            // The payload's ONLY row claims the FOREIGN request's real, persisted id — never the local
            // one — so it must never be read as "the local relation survived".
            var resubmitted = Campus("HN", delegation: "Đoàn X sửa") with
            {
                Visitors = new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg", null, "k-a", foreignId) },
            };

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(
                    r, Edit(r, Slot(hn, resubmitted, operationalContactClientMemberKey: "k-a")),
                    Registrant, Now, default));

            Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
            Assert.Equal(localId, hn.FormDetail.OperationalContactGuestMemberId);
            // The OTHER request's own aggregate is completely untouched.
            Assert.Equal("Foreign Guest", (await db.VisitGuestMembers.AsNoTracking()
                .SingleAsync(m => m.GuestMemberId == foreignId)).FullName);
        });
    }

    [Fact]
    public async Task ResubmitSec04_a_guestmemberid_from_a_sibling_campus_never_satisfies_local_continuity()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN"), Campus("HCM", visitorName: "HCM Guest")),
                Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            var hnLocalId = hn.GuestMemberLinks.Single().GuestMemberId;
            var hcmSiblingId = hcm.GuestMemberLinks.Single().GuestMemberId;
            hn.FormDetail!.OperationalContactGuestMemberId = hnLocalId;
            await db.SaveChangesAsync();
            await RejectAllAsync(db, r);

            // HN's own resubmit payload claims HCM's sibling member's id — HCM is a different campus
            // instance entirely, its members are never eligible evidence for HN's relation.
            var hnResubmitted = Campus("HN", delegation: "Đoàn X sửa") with
            {
                Visitors = new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg", null, "k-a", hcmSiblingId) },
            };
            var hcmResubmitted = Campus("HCM", visitorName: "HCM Guest");

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(
                    r, Edit(r,
                        Slot(hn, hnResubmitted, operationalContactClientMemberKey: "k-a"),
                        Slot(hcm, hcmResubmitted)),
                    Registrant, Now, default));

            Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
            Assert.Equal(hnLocalId, hn.FormDetail.OperationalContactGuestMemberId);
            // The whole submission is one transaction — HCM's sibling row is untouched too.
            Assert.Equal(hcmSiblingId, hcm.GuestMemberLinks.Single().GuestMemberId);
        });
    }

    [Fact]
    public async Task ResubmitSec05_an_old_client_payload_that_omits_GuestMemberId_fails_closed_as_stale_session()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var guestId = hn.GuestMemberLinks.Single().GuestMemberId;
            hn.FormDetail!.OperationalContactGuestMemberId = guestId;
            await db.SaveChangesAsync();
            await RejectAllAsync(db, r);

            // Old-client shape: the currently-linked row is echoed back under the SAME ClientMemberKey
            // the relation names, but its GuestMemberId is omitted (pre-upgrade wire shape) — never
            // silently trusted via the key alone.
            var resubmitted = Campus("HN", delegation: "Đoàn X sửa") with
            {
                Visitors = new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg", null, "k-a", null) },
            };

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(
                    r, Edit(r, Slot(hn, resubmitted, operationalContactClientMemberKey: "k-a")),
                    Registrant, Now, default));

            Assert.Equal(OperationalContactErrorCodes.StaleSessionRequiresReload, ex.ErrorCode);
            Assert.Equal(guestId, hn.FormDetail.OperationalContactGuestMemberId);
        });
    }

    [Fact]
    public async Task ResubmitSec06_valid_same_member_continuity_succeeds_and_syncs_the_contact_snapshot()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var guestId = hn.GuestMemberLinks.Single().GuestMemberId;
            // Link only — do not also rewrite the contact's own snapshot fields here:
            // EnsureContactSnapshotUnchanged freezes the contact profile itself through this path, so
            // whatever is stored must keep matching what `resubmitted` below echoes back (the campus
            // default "Op Contact"/"Trưởng phòng Hợp tác"/"OpOrg" — deliberately a different person than
            // the visitor; that pre-existing mismatch is fine, this test only checks the NEW value synced).
            hn.FormDetail!.OperationalContactGuestMemberId = guestId;
            await db.SaveChangesAsync();
            await RejectAllAsync(db, r);

            // Same logical member, renamed job title, echoed back with its OWN real persisted id.
            var resubmitted = Campus("HN", delegation: "Đoàn X sửa") with
            {
                Visitors = new List<VisitorDto>
                {
                    new("Guest A", "VN", "Senior Director", "GuestOrg", null, "k-a", guestId),
                },
            };

            await edit.ApplyResubmitAsync(
                r, Edit(r, Slot(hn, resubmitted, operationalContactClientMemberKey: "k-a")),
                Registrant, Now, default);

            var newId = InstanceOf(r, "HN").GuestMemberLinks.Single().GuestMemberId;
            Assert.NotEqual(guestId, newId); // COW minted a fresh persisted id
            Assert.Equal(newId, hn.FormDetail.OperationalContactGuestMemberId); // relation followed it
            Assert.Equal("Senior Director", hn.FormDetail.OperationalContactJobTitle); // synced from the member
            Assert.Equal(VisitRequestStatuses.PendingApproval, r.Status);
        });
    }
}
