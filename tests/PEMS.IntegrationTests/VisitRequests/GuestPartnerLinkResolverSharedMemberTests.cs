using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.VisitLinks.Common;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Partners;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Remediation plan 2026-08-20, Phase 1 (Issue A / Patch 1) — GP-1..GP-8.
///
/// <para><b>What was wrong.</b> <see cref="GuestPartnerLinkResolver.ResolveForRequestAsync"/> joined
/// <c>visit_instance_guest_members</c> to <c>visit_guest_members</c> and called
/// <c>ToDictionary(m => m.GuestMemberId)</c> on the result. A legacy member shared by more than one
/// campus instance of the SAME request (the shape copy-on-write exists to unwind, not one it
/// prevents up front) makes that join yield more than one row per <c>GuestMemberId</c>, and
/// <c>ToDictionary</c> throws <c>ArgumentException: An item with the same key has already been
/// added</c> — the exact 500 reported against request 47003 on <c>PUT
/// /api/v2/visit-requests/{id}/pending-edit</c>.</para>
///
/// <para><b>Data model proven before fixing (plan §6.1).</b> The relationship this resolver writes is
/// member-global, not per-instance: <c>Organization</c>/<c>OrganizationPartnerId</c> are columns of
/// <c>VisitGuestMember</c> (one row per person), never of the instance link, and the resolver's own
/// docstring plus <c>CreateOrUpdateVisitGuestPartnerLinkCommandHandler</c> ("One target keeps at most
/// one active link") both already assume Model A. The fix collapses the join by
/// <c>GuestMemberId</c> — lossless, since every duplicate row of one member carries the identical
/// Organization/OrganizationPartnerId by construction — and scopes the resulting link to the single
/// instance when unambiguous, or to the whole request (<c>VisitInstanceId = null</c>) when the member
/// is still shared, so the link stays visible from every instance that shares the member instead of
/// being pinned to an arbitrary one of them.</para>
/// </summary>
public sealed class GuestPartnerLinkResolverSharedMemberTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong OwnerCampusId = 1;
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

    // ── Builders (mirrors UpdatePendingVisitRequestV2ServiceTests' local convention) ──────────────

    private static CampusVisitFormDto Campus(string code, IList<VisitorDto>? visitors = null,
        string delegation = "Đoàn Base", string purpose = "Thăm", int startOffsetDays = 20, int durationMinutes = 120)
    {
        var start = Now.AddDays(startOffsetDays);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(durationMinutes), delegation, "MEETING", null, purpose, "Nội dung",
            visitors ?? new List<VisitorDto> { new("Guest A", "Việt Nam", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null);
    }

    private static VisitRequestFormDataV2 CreateForm(params CampusVisitFormDto[] campuses)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());

    private static CampusVisitEditV2Dto Keep(VisitRequestCampus instance, CampusVisitFormDto content)
        => new(instance.VisitInstanceId, instance.RowVersion,
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
        finally
        {
            await tx.RollbackAsync();
        }
    }

    private static async Task<VisitRequestCampus> InstanceByCodeAsync(ApplicationDbContext db, VisitRequest r, string code)
    {
        var campusId = await db.Campuses.AsNoTracking()
            .Where(c => c.CampusCode == code).Select(c => c.CampusId).SingleAsync();
        return r.CampusInstances.Single(c => c.CampusId == campusId);
    }

    /// <summary>Creates a linkable (APPROVED) partner owned by <see cref="OwnerCampusId"/>.</summary>
    private static async Task<ulong> CreatePartnerAsync(ApplicationDbContext db, string name)
    {
        var partner = new Partner
        {
            OwnerCampusId = OwnerCampusId,
            Name = name,
            PartnerType = PartnerTypes.Company,
            Country = "VN",
            ProfileStatus = PartnerProfileStatuses.Approved,
            Visibility = PartnerVisibilities.Public,
            CooperationStatus = "ACTIVE",
            CreatedAt = Now,
        };
        db.Partners.Add(partner);
        await db.SaveChangesAsync();
        return partner.PartnerId;
    }

    /// <summary>
    /// Forces every <paramref name="followers"/> instance to drop its OWN member rows and share
    /// <paramref name="keeper"/>'s first member instead — the legacy shape request 47003 was in when
    /// the resolver crashed. Mirrors the fixture in
    /// <c>UpdatePendingVisitRequestV2ServiceTests.Legacy_shared_member_survives_via_copy_on_write_when_sibling_edits</c>,
    /// generalised to N followers.
    /// </summary>
    private static async Task<ulong> ShareOneMemberAsync(
        ApplicationDbContext db, VisitRequest r, VisitRequestCampus keeper, params VisitRequestCampus[] followers)
    {
        var sharedId = keeper.GuestMemberLinks.Select(l => l.GuestMemberId).First();
        foreach (var follower in followers)
        {
            var ownMemberIds = follower.GuestMemberLinks.Select(l => l.GuestMemberId).ToList();
            foreach (var link in follower.GuestMemberLinks.ToList())
            {
                db.VisitInstanceGuestMembers.Remove(link);
                follower.GuestMemberLinks.Remove(link);
            }
            foreach (var id in ownMemberIds)
            {
                var m = r.GuestMembers.First(x => x.GuestMemberId == id);
                r.GuestMembers.Remove(m);
                db.VisitGuestMembers.Remove(m);
            }
            follower.GuestMemberLinks.Add(new VisitInstanceGuestMember
            {
                VisitRequestId = r.VisitRequestId,
                VisitInstanceId = follower.VisitInstanceId,
                GuestMemberId = sharedId,
                DisplayOrder = 0,
                CreatedAt = Now,
            });
        }
        await db.SaveChangesAsync();
        return sharedId;
    }

    // ── GP-1 — exact reproduction: no 500, transaction commits ─────────────────────────────────

    [Fact]
    public async Task GP1_Shared_legacy_member_across_two_campuses_does_not_crash_pending_edit()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = await InstanceByCodeAsync(db, r, "HN");
            var hcm = await InstanceByCodeAsync(db, r, "HCM");
            await ShareOneMemberAsync(db, r, hn, hcm);

            // No content change on either side: both stay "untouched siblings" (no copy-on-write),
            // so the shared-member duplication survives unchanged into ResolvePartnerLinksAsync —
            // exactly where the pre-fix resolver threw.
            var result = await edit.ApplyPendingEditAsync(r,
                Edit(r, Keep(hn, Campus("HN")), Keep(hcm, Campus("HCM"))), Registrant, Now, default);

            Assert.NotNull(result);
        });
    }

    // ── GP-2 — no cross-campus link: a still-shared member's link is request-wide, not pinned ──

    [Fact]
    public async Task GP2_Shared_member_link_is_request_wide_not_pinned_to_one_campus()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var partnerId = await CreatePartnerAsync(db, "Shared Org Co");
            // No OrganizationPartnerId at create time: an explicit pick there would let CREATE's own
            // resolver call confirm a (correctly) single-instance link BEFORE the member is shared,
            // and this fix intentionally never rescopes an EXISTING confirmed link afterward (plan
            // §6.3 "not overwrite existing confirmed decisions"). A genuinely legacy-shared member was
            // never single-instance to begin with, so the realistic fixture — and the one that
            // exercises "first ever resolution while already shared" — sets the pick AFTER sharing.
            var r = await create.CreateV2Async(CreateForm(
                Campus("HN", new List<VisitorDto> { new("Guest HN", "Việt Nam", "Guest", "Shared Org Co") }),
                Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = await InstanceByCodeAsync(db, r, "HN");
            var hcm = await InstanceByCodeAsync(db, r, "HCM");
            var sharedId = await ShareOneMemberAsync(db, r, hn, hcm);

            var sharedMember = r.GuestMembers.Single(m => m.GuestMemberId == sharedId);
            sharedMember.OrganizationPartnerId = partnerId;
            await db.SaveChangesAsync();
            var sharedVisitor = new List<VisitorDto> { new("Guest HN", "Việt Nam", "Guest", "Shared Org Co", partnerId) };

            await edit.ApplyPendingEditAsync(r,
                Edit(r, Keep(hn, Campus("HN", sharedVisitor)), Keep(hcm, Campus("HCM", sharedVisitor))),
                Registrant, Now, default);

            var links = await db.VisitGuestPartnerLinks
                .Where(l => l.GuestMemberId == sharedId).ToListAsync();
            var link = Assert.Single(links);
            Assert.Equal(partnerId, link.PartnerId);
            // Shared by both instances → request-wide, so GetVisitGuestPartnerLinksQueryHandler's
            // filter (VisitInstanceId == null || VisitInstanceId == instance) matches from EITHER side.
            Assert.Null(link.VisitInstanceId);
        });
    }

    // ── GP-3 — idempotency: running the resolver twice adds nothing, duplicates nothing ────────

    [Fact]
    public async Task GP3_Resolver_is_idempotent_across_two_runs()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var partnerId = await CreatePartnerAsync(db, "Idempotent Co");
            var r = await create.CreateV2Async(CreateForm(
                Campus("HN", new List<VisitorDto> { new("Guest HN", "Việt Nam", "Guest", "Idempotent Co") }),
                Campus("HCM")), Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = await InstanceByCodeAsync(db, r, "HN");
            var hcm = await InstanceByCodeAsync(db, r, "HCM");
            var sharedId = await ShareOneMemberAsync(db, r, hn, hcm);

            var sharedMember = r.GuestMembers.Single(m => m.GuestMemberId == sharedId);
            sharedMember.OrganizationPartnerId = partnerId;
            await db.SaveChangesAsync();
            var sharedVisitor = new List<VisitorDto> { new("Guest HN", "Việt Nam", "Guest", "Idempotent Co", partnerId) };

            await edit.ApplyPendingEditAsync(r,
                Edit(r, Keep(hn, Campus("HN", sharedVisitor)), Keep(hcm, Campus("HCM", sharedVisitor))),
                Registrant, Now, default);

            var secondRunChanges = await GuestPartnerLinkResolver.ResolveForRequestAsync(
                db, r.VisitRequestId, Now, Registrant, CancellationToken.None);
            await db.SaveChangesAsync();

            Assert.Equal(0, secondRunChanges);
            Assert.Equal(1, await db.VisitGuestPartnerLinks.CountAsync(l => l.GuestMemberId == sharedId));
        });
    }

    // ── GP-4 — conflicting confirmed decisions for the same org must not propagate ─────────────

    [Fact]
    public async Task GP4_Conflicting_confirmed_partners_for_same_org_do_not_propagate()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var partnerA = await CreatePartnerAsync(db, "Ambiguous Co A");
            var partnerB = await CreatePartnerAsync(db, "Ambiguous Co B");
            const string org = "Ambiguous Org";

            var r = await create.CreateV2Async(CreateForm(Campus("HN", new List<VisitorDto>
            {
                new("Member One", "Việt Nam", "Guest", org),
                new("Member Two", "Việt Nam", "Guest", org),
                new("Member Three", "Việt Nam", "Guest", org), // no explicit pick — would receive AUTO_NAME if unambiguous
            })), Registrant, "VISITOR_SUBMITTED", Now, default);

            var hn = await InstanceByCodeAsync(db, r, "HN");
            var memberIds = hn.GuestMemberLinks.OrderBy(l => l.DisplayOrder).Select(l => l.GuestMemberId).ToList();

            // Confirm member 1 → partner A and member 2 → partner B directly (as if done from the
            // minutes screen) — two different decisions recorded for the exact same normalized org.
            db.VisitGuestPartnerLinks.Add(new VisitGuestPartnerLink
            {
                VisitRequestId = r.VisitRequestId, VisitInstanceId = hn.VisitInstanceId,
                GuestMemberId = memberIds[0], PartnerId = partnerA,
                MatchSource = PartnerLinkMatchSources.Manual, MatchStatus = PartnerLinkMatchStatuses.Confirmed,
                CreatedAt = Now,
            });
            db.VisitGuestPartnerLinks.Add(new VisitGuestPartnerLink
            {
                VisitRequestId = r.VisitRequestId, VisitInstanceId = hn.VisitInstanceId,
                GuestMemberId = memberIds[1], PartnerId = partnerB,
                MatchSource = PartnerLinkMatchSources.Manual, MatchStatus = PartnerLinkMatchStatuses.Confirmed,
                CreatedAt = Now,
            });
            await db.SaveChangesAsync();

            var changes = await GuestPartnerLinkResolver.ResolveForRequestAsync(
                db, r.VisitRequestId, Now, Registrant, CancellationToken.None);
            await db.SaveChangesAsync();

            Assert.Equal(0, changes);
            Assert.False(await db.VisitGuestPartnerLinks.AnyAsync(l => l.GuestMemberId == memberIds[2]));
        });
    }

    // ── GP-7 — create flow regression: ordinary (non-shared) auto-propagation still works ──────

    [Fact]
    public async Task GP7_Create_flow_still_auto_propagates_distinct_members_of_the_same_org()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var partnerId = await CreatePartnerAsync(db, "Create Flow Co");
            var r = await create.CreateV2Async(CreateForm(Campus("HN", new List<VisitorDto>
            {
                new("Picker", "Việt Nam", "Guest", "Create Flow Co", partnerId),
                new("Follower", "Việt Nam", "Guest", "Create Flow Co"), // same org, no explicit pick
            })), Registrant, "VISITOR_SUBMITTED", Now, default);

            var hn = await InstanceByCodeAsync(db, r, "HN");
            var memberIds = hn.GuestMemberLinks.OrderBy(l => l.DisplayOrder).Select(l => l.GuestMemberId).ToList();

            var links = await db.VisitGuestPartnerLinks
                .Where(l => l.VisitRequestId == r.VisitRequestId).ToListAsync();
            Assert.Equal(2, links.Count);
            Assert.Contains(links, l => l.GuestMemberId == memberIds[0] && l.PartnerId == partnerId
                                         && l.MatchSource == PartnerLinkMatchSources.RegistrationSelected);
            Assert.Contains(links, l => l.GuestMemberId == memberIds[1] && l.PartnerId == partnerId
                                         && l.MatchSource == PartnerLinkMatchSources.AutoName);
        });
    }

    // ── GP-8 — 3-campus shared member with an explicit partner pick: no crash, no mis-pin ──────

    [Fact]
    public async Task GP8_Three_campus_shared_member_with_explicit_pick_resolves_once_without_crash()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var partnerId = await CreatePartnerAsync(db, "Tri-Campus Co");
            // No pick at create time — see GP2's comment: an explicit pick here would let CREATE's
            // resolver confirm a single-instance link before the member is shared, and the fix never
            // rescopes an existing confirmed link afterward. The pick is set once the member is
            // already shared, matching how a genuinely legacy-shared member is first ever resolved.
            var r = await create.CreateV2Async(CreateForm(
                Campus("HN", new List<VisitorDto> { new("Guest HN", "Việt Nam", "Guest", "Tri-Campus Co") }),
                Campus("HCM"), Campus("DN")),
                Registrant, "VISITOR_SUBMITTED", Now, default);

            var hn = await InstanceByCodeAsync(db, r, "HN");
            var hcm = await InstanceByCodeAsync(db, r, "HCM");
            var dn = await InstanceByCodeAsync(db, r, "DN");
            var sharedId = await ShareOneMemberAsync(db, r, hn, hcm, dn);

            var sharedMember = r.GuestMembers.Single(m => m.GuestMemberId == sharedId);
            sharedMember.OrganizationPartnerId = partnerId;
            await db.SaveChangesAsync();
            var sharedVisitor = new List<VisitorDto> { new("Guest HN", "Việt Nam", "Guest", "Tri-Campus Co", partnerId) };

            // Only HN's content actually changes — HCM and DN echo back the shared member's ACTUAL
            // current content (sharedVisitor, not their own pre-share default), so they stay genuinely
            // untouched siblings and the member is still referenced by TWO instances after the edit,
            // not zero or one.
            var result = await edit.ApplyPendingEditAsync(r, Edit(r,
                Keep(hn, Campus("HN", new List<VisitorDto> { new("Guest HN New", "Việt Nam", "Guest", "Different Co") })),
                Keep(hcm, Campus("HCM", sharedVisitor)),
                Keep(dn, Campus("DN", sharedVisitor))), Registrant, Now, default);

            Assert.NotNull(result);

            var links = await db.VisitGuestPartnerLinks
                .Where(l => l.GuestMemberId == sharedId).ToListAsync();
            var link = Assert.Single(links);
            Assert.Equal(partnerId, link.PartnerId);
            Assert.Null(link.VisitInstanceId); // still shared by 2 instances → request-wide, not pinned
        });
    }

    // ── Related discovery (same defect shape, unrelated to legacy sharing) ─────────────────────
    //
    // Investigating GP-8 above surfaced a SECOND, independent duplicate-key crash: whenever a
    // pending edit changes the CONTENT of two or more campuses of a 3+-campus request in the SAME
    // call, VisitRequestV2EditService.CurrentContentOf's `request.GuestMembers.ToDictionary(m =>
    // m.GuestMemberId)` (line ~1358) can throw the identical
    // "An item with the same key has already been added. Key: 0" — because copy-on-write stages
    // each changed campus's replacement members with GuestMemberId still 0 (unflushed; flush #1
    // happens only after the whole per-instance loop), so a THIRD instance's CurrentContentOf call
    // finds TWO placeholder members both keyed 0. No legacy-shared member is required — three
    // ordinary, never-shared campuses trigger it. Reported alongside Patch 1 since it is the exact
    // ToDictionary(GuestMemberId) hazard plan §6/Phase 0 asks to audit system-wide, found by testing
    // the fix for Issue A.

    [Fact]
    public async Task Pending_edit_of_three_campuses_where_two_change_content_does_not_crash()
    {
        await RunAsync(async (db, create, edit) =>
        {
            var r = await create.CreateV2Async(CreateForm(Campus("HN"), Campus("HCM"), Campus("DN")),
                Registrant, "VISITOR_SUBMITTED", Now, default);
            var hn = await InstanceByCodeAsync(db, r, "HN");
            var hcm = await InstanceByCodeAsync(db, r, "HCM");
            var dn = await InstanceByCodeAsync(db, r, "DN");

            // HN and HCM (the first two in payload order) both change content and go through
            // copy-on-write; DN is untouched but its OWN CurrentContentOf call — needed regardless,
            // to detect that it did NOT change — is what used to crash.
            var result = await edit.ApplyPendingEditAsync(r, Edit(r,
                Keep(hn, Campus("HN", new List<VisitorDto> { new("Guest HN New", "Việt Nam", "Guest", "Org HN") })),
                Keep(hcm, Campus("HCM", new List<VisitorDto> { new("Guest HCM New", "Việt Nam", "Guest", "Org HCM") })),
                Keep(dn, Campus("DN"))), Registrant, Now, default);

            Assert.NotNull(result);
        });
    }
}
