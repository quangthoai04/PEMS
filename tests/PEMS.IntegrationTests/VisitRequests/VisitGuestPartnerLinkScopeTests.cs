using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.VisitLinks.Commands.CreateOrUpdateVisitGuestPartnerLink;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// PART-08 (P0) — a partner link may only ever name a guest of the visit instance it is filed
/// against, and PART-04 — the partner it names must be one this profile status allows linking to.
///
/// <para><b>What was wrong.</b> The target check read:</para>
/// <code>
/// belongs = link row for THIS instance exists
///        || member belongs to THIS request
///        || ANY guest_member_id with that id exists;   // ← this
/// </code>
/// <para>The last clause is satisfied by every row in the table, so the two real checks above it were
/// unreachable. Any caller with rights over one visit could attach a guest of a DIFFERENT visit —
/// another campus, another delegation — to a partner: an IDOR for anyone who could guess an id, and a
/// silent corruption of that partner's visit history for everyone else. It is a P0 because nothing in
/// the response distinguishes it from an ordinary link.</para>
///
/// <para>These tests run against a disposable copy of the canonical database, using its seeded
/// requests, so "instance A" and "instance B" are genuinely separate visits rather than a fixture
/// shape invented to make the assertion easy.</para>
/// </summary>
public sealed class VisitGuestPartnerLinkScopeTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong LeaderHn = 3;     // STAFF/LEADER, campus 1 — may manage both seeded instances
    private const ulong CampusHn = 1;

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => UserId is not null;
        public ulong? UserId { get; init; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => DateTime.Now;
    }

    private static FakeUser Leader() => new()
    {
        UserId = LeaderHn,
        RoleCode = RoleCodes.Staff,
        SubRole = UserSubRoles.Leader,
        PrimaryCampusId = CampusHn,
    };

    private static CreateOrUpdateVisitGuestPartnerLinkCommandHandler Handler(ApplicationDbContext db)
        => new(db, Leader(), new FixedClock());

    /// <summary>Two campus instances of DIFFERENT requests, each with a guest of its own.</summary>
    private static async Task<(ulong InstanceA, ulong GuestA, ulong InstanceB, ulong GuestB)> TwoVisitsAsync(
        ApplicationDbContext db)
    {
        var rows = await db.VisitInstanceGuestMembers.AsNoTracking()
            .Join(db.VisitRequestCampuses.AsNoTracking(),
                l => l.VisitInstanceId, c => c.VisitInstanceId,
                (l, c) => new { l.VisitInstanceId, l.GuestMemberId, l.VisitRequestId, c.CampusId })
            .Where(x => x.CampusId == CampusHn)
            .OrderBy(x => x.VisitInstanceId).ThenBy(x => x.GuestMemberId)
            .Take(400)
            .ToListAsync();

        var byInstance = rows.GroupBy(r => r.VisitInstanceId).ToList();
        Assert.True(byInstance.Count >= 2, "Seed must hold at least two campus-1 instances with guests.");

        var a = byInstance[0];
        // A different REQUEST, not merely a different instance — the strongest form of the leak.
        var b = byInstance.First(g => g.First().VisitRequestId != a.First().VisitRequestId);

        return (a.Key, a.First().GuestMemberId, b.Key, b.First().GuestMemberId);
    }

    private static async Task<ulong> PartnerWithStatusAsync(ApplicationDbContext db, string profileStatus)
    {
        var id = await db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == profileStatus && p.OwnerCampusId == CampusHn)
            .Select(p => p.PartnerId)
            .FirstOrDefaultAsync();
        Assert.True(id != 0, $"Seed must hold a campus-1 partner with profile_status={profileStatus}.");
        return id;
    }

    // ── PART-08: the target must belong to the instance being written ────────────

    [Fact]
    public async Task A_guest_of_another_visit_cannot_be_linked_through_this_instance()
    {
        RequireDb();
        using var db = NewContext();
        var (instanceA, _, _, guestB) = await TwoVisitsAsync(db);
        var partner = await PartnerWithStatusAsync(db, PartnerProfileStatuses.Approved);

        // The guest exists — that is precisely what the deleted fallback accepted as proof.
        Assert.True(await db.VisitGuestMembers.AnyAsync(g => g.GuestMemberId == guestB));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db).Handle(new CreateOrUpdateVisitGuestPartnerLinkCommand
            {
                VisitInstanceId = instanceA,
                GuestMemberId = guestB,
                PartnerId = partner,
            }, CancellationToken.None));

        // Not found, not forbidden: an id the caller may not touch must be indistinguishable from
        // one that does not exist, or the endpoint answers "does this id exist" for free.
        Assert.False(await db.VisitGuestPartnerLinks.AnyAsync(
            l => l.GuestMemberId == guestB && l.VisitInstanceId == instanceA));
    }

    [Fact]
    public async Task A_guest_id_that_exists_nowhere_is_refused()
    {
        RequireDb();
        using var db = NewContext();
        var (instanceA, _, _, _) = await TwoVisitsAsync(db);
        var partner = await PartnerWithStatusAsync(db, PartnerProfileStatuses.Approved);
        var ghost = await db.VisitGuestMembers.AsNoTracking().MaxAsync(g => g.GuestMemberId) + 10_000;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db).Handle(new CreateOrUpdateVisitGuestPartnerLinkCommand
            {
                VisitInstanceId = instanceA,
                GuestMemberId = ghost,
                PartnerId = partner,
            }, CancellationToken.None));
    }

    [Fact]
    public async Task A_guest_of_THIS_instance_links_normally()
    {
        // The counterweight: the guard above must not have been bought by breaking the ordinary path.
        RequireDb();
        using var db = NewContext();
        var (instanceA, guestA, _, _) = await TwoVisitsAsync(db);
        var partner = await PartnerWithStatusAsync(db, PartnerProfileStatuses.Approved);

        var dto = await Handler(db).Handle(new CreateOrUpdateVisitGuestPartnerLinkCommand
        {
            VisitInstanceId = instanceA,
            GuestMemberId = guestA,
            PartnerId = partner,
        }, CancellationToken.None);

        Assert.Equal(partner, dto.PartnerId);
        Assert.Equal(guestA, dto.GuestMemberId);
        Assert.Equal(PartnerLinkMatchStatuses.Confirmed, dto.MatchStatus);
    }

    [Fact]
    public async Task Relinking_the_same_guest_reuses_the_row_instead_of_stacking_duplicates()
    {
        RequireDb();
        using var db = NewContext();
        var (instanceA, guestA, _, _) = await TwoVisitsAsync(db);
        var first = await PartnerWithStatusAsync(db, PartnerProfileStatuses.Approved);

        await Handler(db).Handle(new CreateOrUpdateVisitGuestPartnerLinkCommand
        {
            VisitInstanceId = instanceA, GuestMemberId = guestA, PartnerId = first,
        }, CancellationToken.None);
        await Handler(db).Handle(new CreateOrUpdateVisitGuestPartnerLinkCommand
        {
            VisitInstanceId = instanceA, GuestMemberId = guestA, PartnerId = first,
        }, CancellationToken.None);

        using var check = NewContext();
        Assert.Equal(1, await check.VisitGuestPartnerLinks.CountAsync(l => l.GuestMemberId == guestA));
    }

    // ── PART-04: profile status decides linkability, not read permission ─────────

    [Fact]
    public async Task A_rejected_profile_cannot_be_linked_even_by_the_campus_that_owns_it()
    {
        RequireDb();
        using var db = NewContext();
        var (instanceA, guestA, _, _) = await TwoVisitsAsync(db);
        var rejected = await PartnerWithStatusAsync(db, PartnerProfileStatuses.Rejected);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() =>
            Handler(db).Handle(new CreateOrUpdateVisitGuestPartnerLinkCommand
            {
                VisitInstanceId = instanceA,
                GuestMemberId = guestA,
                PartnerId = rejected,
            }, CancellationToken.None));

        // A distinct code, because the UI has a distinct thing to offer: edit and resubmit.
        Assert.Equal(PartnerErrorCodes.RejectedCannotLink, ex.ErrorCode);
        Assert.False(await db.VisitGuestPartnerLinks.AnyAsync(l => l.PartnerId == rejected));
    }

    [Fact]
    public async Task A_draft_profile_cannot_be_linked()
    {
        RequireDb();
        using var db = NewContext();
        var (instanceA, guestA, _, _) = await TwoVisitsAsync(db);
        var draft = await PartnerWithStatusAsync(db, PartnerProfileStatuses.Draft);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() =>
            Handler(db).Handle(new CreateOrUpdateVisitGuestPartnerLinkCommand
            {
                VisitInstanceId = instanceA,
                GuestMemberId = guestA,
                PartnerId = draft,
            }, CancellationToken.None));

        Assert.Equal(PartnerErrorCodes.NotLinkable, ex.ErrorCode);
    }

    [Fact]
    public async Task A_pending_profile_of_the_callers_own_campus_still_links()
    {
        // Work must not stall because a Staff Leader has not got to the queue yet.
        RequireDb();
        using var db = NewContext();
        var (instanceA, guestA, _, _) = await TwoVisitsAsync(db);
        var pending = await PartnerWithStatusAsync(db, PartnerProfileStatuses.PendingApproval);

        var dto = await Handler(db).Handle(new CreateOrUpdateVisitGuestPartnerLinkCommand
        {
            VisitInstanceId = instanceA,
            GuestMemberId = guestA,
            PartnerId = pending,
        }, CancellationToken.None);

        Assert.Equal(pending, dto.PartnerId);
        // The RELATIONSHIP is confirmed while the PROFILE is still pending — two different states,
        // and the DTO has to carry both so the UI can stop conflating them (PART-05).
        Assert.Equal(PartnerLinkMatchStatuses.Confirmed, dto.MatchStatus);
        Assert.Equal(PartnerProfileStatuses.PendingApproval, dto.PartnerProfileStatus);
    }
}
