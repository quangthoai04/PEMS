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
            content.MediaConsentNote);

    /// <summary>Edit slot for a NEW campus (no instance id).</summary>
    private static CampusVisitEditV2Dto Add(CampusVisitFormDto content)
        => new(null, null,
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.MediaConsentNote);

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
            await body(db, new VisitRequestV2CreateService(db), new VisitRequestV2EditService(db));
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

    [Fact]
    public async Task Add_campus_creates_routed_instance_with_baseline_revision_and_scope_multi()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            Assert.Equal(VisitScopes.SingleCampus, r.VisitScope);

            var result = await edit.ApplyPendingEditAsync(r, Edit(r,
                Keep(InstanceOf(r, "HN"), Campus("HN")),
                Add(Campus("HCM"))), Registrant, Now, default);

            Assert.Equal(VisitScopes.MultiCampus, result.VisitScope);
            var hcm = InstanceOf(r, "HCM");
            // A campus added by a pending edit enters the same way one added at submit does: its contact
            // is not the registrant, so it starts by waiting for that person to confirm, holds the whole
            // request behind the gate, and carries no contact account yet.
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, hcm.Status);
            Assert.Null(hcm.OperationalContactUserId);
            Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, r.Status);
            Assert.NotNull(hcm.CoordinatorUserId); // routed to the campus Staff Leader
            Assert.NotNull(hcm.FormDetail);
            Assert.NotEmpty(hcm.GuestMemberLinks); // independent members created + linked
            Assert.True(await db.VisitInstanceFormRevisionHistories.AnyAsync(h =>
                h.VisitInstanceId == hcm.VisitInstanceId && h.SourceType == "CREATE"));

            // Member independence: the added campus's member ids differ from HN's.
            var hnIds = InstanceOf(r, "HN").GuestMemberLinks.Select(l => l.GuestMemberId).ToHashSet();
            Assert.DoesNotContain(hcm.GuestMemberLinks, l => hnIds.Contains(l.GuestMemberId));
        });
    }

    [Fact]
    public async Task Remove_campus_deletes_instance_and_orphan_members_and_recomputes_scope()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hcm = InstanceOf(r, "HCM");
            var hcmInstanceId = hcm.VisitInstanceId;
            var hcmMemberIds = hcm.GuestMemberLinks.Select(l => l.GuestMemberId).ToList();

            var result = await edit.ApplyPendingEditAsync(r, Edit(r,
                Keep(InstanceOf(r, "HN"), Campus("HN"))), Registrant, Now, default);

            Assert.Equal(VisitScopes.SingleCampus, result.VisitScope);
            Assert.False(await db.VisitRequestCampuses.AnyAsync(c => c.VisitInstanceId == hcmInstanceId));
            foreach (var memberId in hcmMemberIds) // orphan member rows cleaned up
                Assert.False(await db.VisitGuestMembers.AnyAsync(m => m.GuestMemberId == memberId));
            // Sibling untouched.
            Assert.NotEmpty(InstanceOf(r, "HN").GuestMemberLinks);
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
    /// Correcting the contact NAME or PHONE is a change to that campus, not to the request, so it is
    /// recorded as an instance revision. Only the address is locked.
    /// </summary>
    public async Task Contact_name_phone_change_writes_an_instance_revision()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var instanceId = InstanceOf(r, "HN").VisitInstanceId;
            var renamed = Keep(InstanceOf(r, "HN"),
                Campus("HN", contactName: "Tên Mới", contactPhone: "+84999"));

            await edit.ApplyPendingEditAsync(r, Edit(r, renamed), Registrant, Now, default);

            var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceId);
            Assert.Equal("Tên Mới", detail.OperationalContactFullName);
            var revisions = await db.VisitInstanceFormRevisionHistories
                .Where(h => h.VisitInstanceId == instanceId).ToListAsync();
            Assert.Contains(revisions, h => h.SourceType == FormRevisionSourceTypes.PendingEdit);
        });
    }

    [Fact]
    public async Task Approved_instance_blocks_edit_and_removal()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            var hcm = InstanceOf(r, "HCM");
            // In-memory only (no flush → no DB trigger involved): the service must gate on the tracked status.
            hcm.Status = VisitInstanceStatuses.BeforeVisit; // approved = host assigned

            // Editing the approved instance is blocked.
            var ex1 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r,
                    Keep(hn, Campus("HN")),
                    Keep(hcm, Campus("HCM", delegation: "Đoàn sửa"))), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.VisitRequestNotEditable, ex1.ErrorCode);

            // Removing the approved instance is blocked too.
            var ex2 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, Keep(hn, Campus("HN"))), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InstanceNotRemovable, ex2.ErrorCode);
        });
    }

    [Fact]
    public async Task Removal_with_downstream_data_is_blocked()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hcm = InstanceOf(r, "HCM");
            db.VisitAgendas.Add(new VisitAgenda
            {
                VisitInstanceId = hcm.VisitInstanceId,
                Title = "Chuẩn bị sớm",
                StartTime = hcm.PlannedStartAt,
                SequenceOrder = 1,
                CreatedAt = Now,
            });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, Keep(InstanceOf(r, "HN"), Campus("HN"))), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InstanceNotRemovable, ex.ErrorCode);
        });
    }

    [Fact]
    public async Task Changing_campus_of_existing_instance_is_rejected()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = InstanceOf(r, "HN");
            // Same instance id, different campus code → must be remove + add, never in-place.
            var moved = Keep(hn, Campus("HCM"));

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                edit.ApplyPendingEditAsync(r, Edit(r, moved), Registrant, Now, default));
            Assert.Equal(VisitRequestErrorCodes.InstanceEditInvalid, ex.ErrorCode);
        });
    }
}
