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
/// Pending-edit v2 SERVICE matrix (Phase C). Each test creates a v2 aggregate through the real
/// <see cref="VisitRequestV2CreateService"/> and edits it through <see cref="VisitRequestV2EditService"/>
/// inside ONE transaction that is rolled back — <c>pems_pr3_test</c> keeps v2_requests = 0.
/// Covers: per-campus isolation (sửa A không đổi B), mixed/scope/fingerprint/projection recompute,
/// add/remove campus, member independence + legacy copy-on-write, unchanged-campus no-op, optimistic
/// concurrency (request + instance), immutable account-binding fields, duration edge (29m/30m),
/// approved-instance blocks (edit + remove), downstream-data removal block, and revision/audit rows.
/// </summary>
public sealed class UpdatePendingVisitRequestV2ServiceTests
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

    // ── Builders ─────────────────────────────────────────────────────────────

    private static CampusVisitFormDto Campus(string code, string delegation = "Đoàn Base", string purpose = "Thăm",
        string visitorName = "Guest A", int startOffsetDays = 20, int durationMinutes = 120,
        string contactName = "Op Contact", string contactPhone = "+8410", string contactEmail = "op@example.com")
    {
        var start = Now.AddDays(startOffsetDays);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(durationMinutes), delegation, "MEETING", null, purpose, "Nội dung",
            new List<VisitorDto> { new(visitorName, "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto(contactName, "OpOrg", "Trưởng phòng Hợp tác", contactPhone, contactEmail),
            "EN", null, "DECLINED", null, null);
    }

    private static VisitRequestFormDataV2 CreateForm(params CampusVisitFormDto[] campuses)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());

    /// <summary>Edit slot for an EXISTING instance, carrying its stable id + current row version.</summary>
    private static CampusVisitEditV2Dto Keep(VisitRequestCampus instance, CampusVisitFormDto content)
        => new(instance.VisitInstanceId, instance.RowVersion,
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.Notes);

    /// <summary>Edit slot for a NEW campus (no instance id).</summary>
    private static CampusVisitEditV2Dto Add(CampusVisitFormDto content)
        => new(null, null,
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.Notes);

    private static VisitRequestEditV2Dto Edit(VisitRequest request, params CampusVisitEditV2Dto[] campuses)
        => new(request.RowVersion,
            new RegistrantInputV2(request.RegistrantFullName, request.RegistrantNationality ?? "VN",
                request.RegistrantOrganization, request.RegistrantJobTitle ?? "Job",
                request.RegistrantPhone ?? "+8491", request.RegistrantEmail),
            request.PartnerId, campuses.ToList());

    /// <summary>Creates the aggregate + applies the edit inside one rolled-back transaction.</summary>
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

    private static VisitRequestCampus InstanceOf(VisitRequest r, string code)
        => r.CampusInstances.Single(c => c.CampusId == (code == "HN" ? 1UL : code == "HCM" ? 2UL : 3UL));

    // ── Matrix ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_campus_A_updates_only_A_and_recomputes_mixed_fingerprint_projection()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            Assert.False(r.HasMixedCampusDetails);
            var fingerprintBefore = r.BusinessFingerprint;
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            var hcmRevisionBefore = hcm.FormDetail!.FormRevision;
            var hcmRowVersionBefore = hcm.RowVersion;
            var hcmMemberIdsBefore = hcm.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList();

            // Change ONLY HN's content (delegation + a different visitor).
            var result = await edit.ApplyPendingEditAsync(r, Edit(r,
                Keep(hn, Campus("HN", delegation: "Đoàn HN mới", visitorName: "Guest HN")),
                Keep(hcm, Campus("HCM"))), Registrant, Now, default);

            // A updated: revision bumped, new member rows, revision history row written.
            Assert.Equal(2u, hn.FormDetail!.FormRevision);
            Assert.Equal("Đoàn HN mới", hn.FormDetail.DelegationName);
            var hnRevisions = await db.VisitInstanceFormRevisionHistories
                .Where(h => h.VisitInstanceId == hn.VisitInstanceId).ToListAsync();
            Assert.Contains(hnRevisions, h => h.SourceType == FormRevisionSourceTypes.PendingEdit && h.FormRevision == 2);

            // B untouched: same revision, same row version, same member ids (sửa A không đổi B).
            Assert.Equal(hcmRevisionBefore, hcm.FormDetail!.FormRevision);
            Assert.Equal(hcmRowVersionBefore, hcm.RowVersion);
            Assert.Equal(hcmMemberIdsBefore, hcm.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList());

            // Recompute: mixed now true, fingerprint changed, request row version bumped.
            Assert.True(result.HasMixed);
            Assert.True(r.HasMixedCampusDetails);
            Assert.NotEqual(fingerprintBefore, r.BusinessFingerprint);
            Assert.Equal(1, r.RowVersion);
            // Pure V2: the edited content lands on HN's OWN detail. The request row carries no form
            // content at all, so there is no smallest-campus projection to follow any more.
            Assert.Equal("Đoàn HN mới", hn.FormDetail!.DelegationName);

            // Audit row with correlation id exists.
            Assert.True(await db.AuditLogs.AnyAsync(a =>
                a.VisitRequestId == r.VisitRequestId && a.Action == "UPDATE_PENDING_VISIT_REQUEST_V2"));
        });
    }

    [Fact]
    public async Task Edit_making_all_campuses_identical_clears_mixed()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN", delegation: "Đoàn X"), Campus("HCM", delegation: "Đoàn Y")),
                Registrant, "VISITOR_SUBMITTED", Now, default);
            Assert.True(r.HasMixedCampusDetails);

            var result = await edit.ApplyPendingEditAsync(r, Edit(r,
                Keep(InstanceOf(r, "HN"), Campus("HN", delegation: "Đoàn X")),
                Keep(InstanceOf(r, "HCM"), Campus("HCM", delegation: "Đoàn X"))), Registrant, Now, default);

            Assert.False(result.HasMixed);
            Assert.False(r.HasMixedCampusDetails);
        });
    }

    [Fact]
    public async Task Unchanged_campus_is_a_true_noop_no_member_churn_no_revision()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var memberIdsBefore = hn.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList();
            var rowVersionBefore = hn.RowVersion;

            await edit.ApplyPendingEditAsync(r, Edit(r, Keep(hn, Campus("HN"))), Registrant, Now, default);

            Assert.Equal(1u, hn.FormDetail!.FormRevision); // not bumped
            Assert.Equal(rowVersionBefore, hn.RowVersion); // untouched instance
            Assert.Equal(memberIdsBefore, hn.GuestMemberLinks.Select(l => l.GuestMemberId).OrderBy(x => x).ToList());
            Assert.False(await db.VisitInstanceFormRevisionHistories.AnyAsync(h =>
                h.VisitInstanceId == hn.VisitInstanceId && h.SourceType == FormRevisionSourceTypes.PendingEdit));
            // Request row version still bumps (the edit intent was applied).
            Assert.Equal(1, r.RowVersion);
        });
    }

    /// <summary>
    /// The campus set is chosen at create and is fixed from the moment the request exists — including
    /// while every campus is still waiting, which is the case that used to be the exception.
    ///
    /// <para>
    /// Adding used to work, and that is what makes this worth asserting rather than assuming: a request
    /// whose scope, fingerprint and set of invited contacts could change under an edit is a request
    /// whose identity is not stable for anyone already holding a link to it. Wanting another campus is
    /// a new request.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Adding_a_campus_is_refused_even_while_every_campus_is_still_pending()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            Assert.Equal(VisitScopes.SingleCampus, r.VisitScope);
            var instanceCountBefore = r.CampusInstances.Count;

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r,
                    Keep(InstanceOf(r, "HN"), Campus("HN")),
                    Add(Campus("HCM"))), Registrant, Now, default));

            Assert.Equal(VisitRequestErrorCodes.CampusSetImmutable, ex.ErrorCode);
            // Refused during validation, so nothing was half-applied.
            Assert.Equal(instanceCountBefore, r.CampusInstances.Count);
            Assert.Equal(VisitScopes.SingleCampus, r.VisitScope);
            Assert.Equal(0, r.RowVersion);
        });
    }

    /// <summary>
    /// Dropping a campus by leaving it out of the payload is the other half of the same rule. It is not
    /// an edit at all — a campus that should not happen is a cancellation, which has its own workflow,
    /// its own audit and its own notifications to the people already invited to it.
    /// </summary>
    [Fact]
    public async Task Omitting_a_campus_from_the_payload_is_refused_rather_than_deleting_it()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hcmInstanceId = InstanceOf(r, "HCM").VisitInstanceId;
            var hcmMemberIds = InstanceOf(r, "HCM").GuestMemberLinks.Select(l => l.GuestMemberId).ToList();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r,
                    Keep(InstanceOf(r, "HN"), Campus("HN"))), Registrant, Now, default));

            Assert.Equal(VisitRequestErrorCodes.CampusSetImmutable, ex.ErrorCode);
            // The campus and its people are still there.
            Assert.Contains(r.CampusInstances, c => c.VisitInstanceId == hcmInstanceId);
            foreach (var memberId in hcmMemberIds)
                Assert.True(await db.VisitGuestMembers.AnyAsync(m => m.GuestMemberId == memberId));
            Assert.Equal(VisitScopes.MultiCampus, r.VisitScope);
        });
    }

    [Fact]
    public async Task Legacy_shared_member_survives_via_copy_on_write_when_sibling_edits()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");

            // Simulate a LEGACY migrated share: drop HCM's own member rows and point its links at HN's rows.
            var hnMemberIds = hn.GuestMemberLinks.Select(l => l.GuestMemberId).ToList();
            var hcmOwnMemberIds = hcm.GuestMemberLinks.Select(l => l.GuestMemberId).ToList();
            foreach (var link in hcm.GuestMemberLinks.ToList())
            {
                db.VisitInstanceGuestMembers.Remove(link);
                hcm.GuestMemberLinks.Remove(link);
            }
            foreach (var id in hcmOwnMemberIds)
            {
                var m = r.GuestMembers.First(x => x.GuestMemberId == id);
                r.GuestMembers.Remove(m);
                db.VisitGuestMembers.Remove(m);
            }
            uint order = 0;
            foreach (var sharedId in hnMemberIds)
                hcm.GuestMemberLinks.Add(new VisitInstanceGuestMember
                {
                    VisitRequestId = r.VisitRequestId,
                    VisitInstanceId = hcm.VisitInstanceId,
                    GuestMemberId = sharedId,
                    DisplayOrder = order++,
                    CreatedAt = Now,
                });
            await db.SaveChangesAsync();

            // Edit HN (full member replace). The shared rows must SURVIVE because HCM still links them.
            await edit.ApplyPendingEditAsync(r, Edit(r,
                Keep(hn, Campus("HN", visitorName: "Guest HN Replaced")),
                Keep(hcm, Campus("HCM"))), Registrant, Now, default);

            foreach (var sharedId in hnMemberIds)
            {
                Assert.True(await db.VisitGuestMembers.AnyAsync(m => m.GuestMemberId == sharedId),
                    "legacy shared member row must survive a sibling's edit (copy-on-write)");
                Assert.Contains(hcm.GuestMemberLinks, l => l.GuestMemberId == sharedId);
            }
            // HN now links NEW rows only.
            Assert.DoesNotContain(hn.GuestMemberLinks, l => hnMemberIds.Contains(l.GuestMemberId));
        });
    }

    [Fact]
    public async Task Stale_request_row_version_is_stable_409()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var payload = Edit(r, Keep(InstanceOf(r, "HN"), Campus("HN", delegation: "Đoàn mới")));
            payload = payload with { ExpectedRequestRowVersion = r.RowVersion + 7 }; // stale/foreign version

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                edit.ApplyPendingEditAsync(r, payload, Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.RequestVersionConflict, ex.ErrorCode);
            Assert.Equal("Đoàn Base", InstanceOf(r, "HN").FormDetail!.DelegationName); // nothing applied
        });
    }

    [Fact]
    public async Task Stale_instance_row_version_is_stable_409()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var stale = Keep(hn, Campus("HN", delegation: "Đoàn mới")) with { ExpectedRowVersion = hn.RowVersion + 3 };

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, stale), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InstanceVersionConflict, ex.ErrorCode);
        });
    }

    [Fact]
    public async Task Duration_29m_fails_30m_passes()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");

            var tooShort = Campus("HN", durationMinutes: 29);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, Keep(hn, tooShort)), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);

            await edit.ApplyPendingEditAsync(r, Edit(r, Keep(hn, Campus("HN", durationMinutes: 30))), Registrant, Now, default);
            Assert.Equal(30, (InstanceOf(r, "HN").PlannedEndAt - InstanceOf(r, "HN").PlannedStartAt).TotalMinutes);
        });
    }

    /// <summary>
    /// A pending edit answers to the SCHEDULING floor, and to the same one create uses
    /// (<see cref="VisitMutationPolicy.MinScheduleLeadHours"/>). It used to answer to
    /// <c>RequiredLeadHours</c> — how late the action stays open — which let a request be edited into a
    /// slot it could never have been created for. Exactly on the boundary is inside the window
    /// (TC-TIME-01/02/04).
    /// </summary>
    [Fact]
    public async Task Edited_schedule_answers_to_the_scheduling_lead_time()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");

            var tooSoon = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-1);
            var soon = Campus("HN") with { PlannedStartAt = tooSoon, PlannedEndAt = tooSoon.AddHours(2) };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, Keep(hn, soon)), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
            // The refusal names the campus and the earliest date that WOULD work — a bare "at least 72
            // hours" leaves the user doing arithmetic against a clock they cannot see.
            Assert.Contains("HN", ex.Message);
            Assert.Contains(VisitMutationPolicy.MinScheduleLeadHours.ToString(), ex.Message);

            var exactly = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours);
            var onTheLine = Campus("HN") with { PlannedStartAt = exactly, PlannedEndAt = exactly.AddHours(2) };
            await edit.ApplyPendingEditAsync(r, Edit(r, Keep(hn, onTheLine)), Registrant, Now, default);
            Assert.Equal(exactly, InstanceOf(r, "HN").PlannedStartAt);
        });
    }

    /// <summary>
    /// Multi-campus is ATOMIC: one campus inside the lead time refuses the whole edit, and the message
    /// names which one rather than leaving the user to compare cards (TC-TIME-06).
    /// </summary>
    [Fact]
    public async Task One_campus_inside_the_lead_time_refuses_the_whole_edit()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(
                CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            var originalHnStart = hn.PlannedStartAt;

            var tooSoon = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-1);
            var badHcm = Campus("HCM") with { PlannedStartAt = tooSoon, PlannedEndAt = tooSoon.AddHours(2) };

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(
                    r, Edit(r, Keep(hn, Campus("HN")), Keep(hcm, badHcm)), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
            Assert.Contains("HCM", ex.Message);
            // Nothing was applied — the good campus is untouched, not half-saved.
            Assert.Equal(originalHnStart, InstanceOf(r, "HN").PlannedStartAt);
        });
    }

    [Fact]
    public async Task Immutable_registrant_and_contact_email_are_rejected()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var keep = Keep(InstanceOf(r, "HN"), Campus("HN"));

            var badRegistrant = Edit(r, keep) with
            {
                Registrant = new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "other@example.com"),
            };
            var ex1 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, badRegistrant, Registrant, Now, default));
            Assert.Equal("IMMUTABLE_REGISTRANT_INFO", ex1.ErrorCode);

            // The contact ADDRESS of a campus is immutable in a form edit: it is what that campus’s
            // confirmation is bound to, so changing it has to go through replace/transfer.
            var swappedCampus = Keep(InstanceOf(r, "HN"), Campus("HN", contactEmail: "swapped@example.com"));
            var ex2 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, swappedCampus), Registrant, Now, default));
            Assert.Equal("IMMUTABLE_CONTACT_IDENTITY", ex2.ErrorCode);
        });
    }

    [Fact]
    /// <summary>
    /// The contact's NAME and PHONE are refused here too (repair v3 §2.2). They used to be editable, on
    /// the reasoning that correcting a typo in a name is not a change of who runs the campus — true, and
    /// it made the request-edit form a second, silent writer of contact data.
    ///
    /// <para>
    /// Editing a visit request and managing its operational contact are two workflows now, with two
    /// screens and two endpoints. The contact snapshot still travels in this payload so an UNCHANGED one
    /// round-trips; anything else is refused before a single row is written, under its own code so the
    /// UI can say which workflow the user wants.
    /// </para>
    /// </summary>
    public async Task Contact_name_and_phone_cannot_be_edited_through_the_request_form()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var instanceId = InstanceOf(r, "HN").VisitInstanceId;
            var renamed = Keep(InstanceOf(r, "HN"),
                Campus("HN", contactName: "Tên Mới", contactPhone: "+84999"));

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, renamed), Registrant, Now, default));
            Assert.Equal("IMMUTABLE_CONTACT_PROFILE", ex.ErrorCode);

            // Refused during validation, so nothing was written: not the contact, and not the campus
            // revision an applied edit would have recorded.
            var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceId);
            Assert.Equal("Op Contact", detail.OperationalContactFullName);
            var revisions = await db.VisitInstanceFormRevisionHistories
                .Where(h => h.VisitInstanceId == instanceId).ToListAsync();
            Assert.DoesNotContain(revisions, h => h.SourceType == FormRevisionSourceTypes.PendingEdit);
        });
    }

    [Fact]
    /// <summary>
    /// The other half of the same rule: an edit that leaves the contact exactly as it is must still go
    /// through. Every save from the edit screen carries the snapshot, so a guard that could not tell
    /// "unchanged" from "changed" would block ordinary edits entirely.
    /// </summary>
    public async Task An_edit_that_leaves_the_contact_alone_still_applies()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var instanceId = InstanceOf(r, "HN").VisitInstanceId;
            // The stored phone is normalised to E.164 on create, so the payload echoes the national form
            // a client would have been served — same number, different spelling, and not a mutation.
            var edited = Keep(InstanceOf(r, "HN"), Campus("HN", purpose: "Mục đích mới"));

            await edit.ApplyPendingEditAsync(r, Edit(r, edited), Registrant, Now, default);

            var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceId);
            Assert.Equal("Mục đích mới", detail.Purpose);
            Assert.Equal("Op Contact", detail.OperationalContactFullName);
            var revisions = await db.VisitInstanceFormRevisionHistories
                .Where(h => h.VisitInstanceId == instanceId).ToListAsync();
            Assert.Contains(revisions, h => h.SourceType == FormRevisionSourceTypes.PendingEdit);
        });
    }

    /// <summary>
    /// A decided campus closes the WHOLE-request edit, because that edit rewrites content across every
    /// campus at once. The campus still waiting is not stranded by it — that is what the per-campus
    /// pending edit is for (see <c>UpdatePendingVisitInstanceV2ServiceTests</c>).
    /// </summary>
    [Fact]
    public async Task Approved_instance_blocks_the_whole_request_edit()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            // In-memory only (no flush → no DB trigger involved): the service must gate on the tracked status.
            hcm.Status = VisitInstanceStatuses.BeforeVisit; // approved = host assigned

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r,
                    Keep(hn, Campus("HN")),
                    Keep(hcm, Campus("HCM", delegation: "Đoàn sửa"))), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.VisitRequestNotEditable, ex.ErrorCode);
        });
    }

    [Fact]
    public async Task Changing_campus_of_existing_instance_is_rejected()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            // Same instance id, different campus code — an add and a remove wearing one payload.
            var moved = Keep(hn, Campus("HCM"));

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, moved), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InstanceEditInvalid, ex.ErrorCode);
        });
    }

    /// <summary>
    /// Time passing must not freeze a request. The 72-hour registration floor governs a schedule being
    /// FILED, so an edit that leaves the dates exactly as they were is not held to it — otherwise a
    /// guest correcting a typo two days before their visit would be told their own unchanged date is
    /// invalid, and the campus would receive the delegation with the mistake still in it.
    /// </summary>
    [Fact]
    public async Task Content_only_edit_inside_the_registration_floor_is_still_allowed()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");

            // The date arrives: the campus now starts in 24 hours, well inside the 72-hour floor.
            var near = Now.AddHours(24);
            hn.PlannedStartAt = near;
            hn.PlannedEndAt = near.AddHours(2);

            var sameSchedule = Campus("HN", delegation: "Đoàn HN (sửa chính tả)")
                with { PlannedStartAt = near, PlannedEndAt = near.AddHours(2) };
            await edit.ApplyPendingEditAsync(r, Edit(r, Keep(hn, sameSchedule)), Registrant, Now, default);

            Assert.Equal("Đoàn HN (sửa chính tả)", InstanceOf(r, "HN").FormDetail!.DelegationName);
            Assert.Equal(near, InstanceOf(r, "HN").PlannedStartAt);
        });
    }
}
