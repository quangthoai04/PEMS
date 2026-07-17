using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
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
    private const string ConnString =
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None";
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
        string visitorName = "Guest A", int startOffsetDays = 20, int durationMinutes = 120)
    {
        var start = Now.AddDays(startOffsetDays);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(durationMinutes), delegation, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new(visitorName, "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);
    }

    private static VisitRequestFormDataV2 CreateForm(params CampusVisitFormDto[] campuses)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
            new ContactPointDto("Registrant", "Org", "+8491", "registrant@example.com"),
            null, campuses.ToList());

    private static CampusVisitEditV2Dto Slot(VisitRequestCampus instance, CampusVisitFormDto content, ulong? overrideId = null)
        => new(overrideId ?? instance.VisitInstanceId, instance.RowVersion,
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.MediaConsentNote, content.Notes);

    private static VisitRequestEditV2Dto Edit(VisitRequest request, params CampusVisitEditV2Dto[] campuses)
        => new(request.RowVersion,
            new RegistrantInputV2(request.RegistrantFullName, request.RegistrantNationality ?? "VN",
                request.RegistrantOrganization, request.RegistrantJobTitle ?? "Job",
                request.RegistrantPhone ?? "+8491", request.RegistrantEmail),
            new ContactPointDto(request.ContactPersonFullName, request.ContactPersonOrganization ?? "Org",
                request.ContactPersonPhone ?? "+8491", request.ContactPersonEmail),
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
            await body(db, new VisitRequestV2CreateService(db), new VisitRequestV2EditService(db));
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
                new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
                "EN", null, "DECLINED", null, null);
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

    [Fact]
    public async Task Schedule_within_24h_is_rejected()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            await RejectAllAsync(db, r);
            var hn = InstanceOf(r, "HN");

            var soon = Campus("HN") with { PlannedStartAt = Now.AddHours(23), PlannedEndAt = Now.AddHours(25) };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, Edit(r, Slot(hn, soon)), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
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
                PrimaryContact = new ContactPointDto("Registrant", "Org", "+8491", "swapped@example.com"),
            };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyResubmitAsync(r, bad, Registrant, Now, default));
            Assert.Equal("IMMUTABLE_CONTACT_IDENTITY", ex.ErrorCode);
        });
    }
}
