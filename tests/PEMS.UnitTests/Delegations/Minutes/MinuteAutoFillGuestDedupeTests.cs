using PEMS.Application.Delegations.Minutes;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Minutes;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.Minutes;

/// <summary>
/// A biên bản lists each attendee once.
///
/// <para>
/// The delegation form does not guarantee that. member_type describes a ROLE IN THE FORM — a person
/// can be entered as a GUEST of the delegation and again as EXTERNAL_SUPPORT accompanying it — so the
/// same human being can own two <c>visit_guest_members</c> rows with two different ids. De-dup by id
/// cannot see this: the ids genuinely differ. The auto-fill therefore compares the four business
/// fields that identify a person, and keeps the GUEST row.
/// </para>
/// <para>
/// The line it does NOT cross is guessing. Only exact matches (after trimming, collapsing repeated
/// spaces and ignoring case) collapse; a different organization, a stripped diacritic, or an internal
/// user who happens to share a guest's name are all treated as different people, because merging two
/// real attendees into one is the worse failure — it deletes someone from the meeting record.
/// </para>
/// </summary>
public class MinuteAutoFillGuestDedupeTests
{
    private const ulong MinutesId = 7001;

    private static VisitGuestMember Guest(
        ulong id, string fullName, string organization = "Đại học ABC",
        string jobTitle = "Manager", string nationality = "Vietnam",
        string memberType = GuestMemberType.Guest) => new()
    {
        GuestMemberId = id,
        VisitRequestId = DelegationsTestData.VisitRequestId,
        MemberType = memberType,
        FullName = fullName,
        Organization = organization,
        JobTitle = jobTitle,
        Nationality = nationality,
        DisplayOrder = (uint)id,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    /// <summary>Seeds the base fixture plus the given guests, each linked to the campus instance.</summary>
    private static DelegationsTestDbContext WithGuests(params VisitGuestMember[] guests)
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        db.GuestMembers.AddRange(guests);
        uint order = 0;
        foreach (var g in guests)
        {
            db.InstanceGuestMembers.Add(new VisitInstanceGuestMember
            {
                VisitRequestId = DelegationsTestData.VisitRequestId,
                VisitInstanceId = DelegationsTestData.VisitInstanceId,
                GuestMemberId = g.GuestMemberId,
                DisplayOrder = ++order,
                CreatedAt = new DateTime(2026, 6, 1),
            });
        }
        db.SaveChanges();
        return db;
    }

    /// <summary>
    /// Runs the auto-fill and returns only the guest rows. The host row is real output but belongs to
    /// rule 1, which these tests are not about.
    /// </summary>
    private static async Task<List<MinuteParticipant>> GuestRowsAsync(
        DelegationsTestDbContext db, params MinuteParticipant[] existing)
    {
        var instance = db.VisitRequestCampuses.Single(c => c.VisitInstanceId == DelegationsTestData.VisitInstanceId);
        var rows = await MinuteAutoFill.ComputeNewRowsAsync(
            db, instance, existing, MinutesId, new DateTime(2026, 8, 1, 9, 0, 0), default);
        return rows.Where(r => r.GuestMemberId != null).ToList();
    }

    /// <summary>An already-persisted biên bản row standing for a guest.</summary>
    private static MinuteParticipant ExistingGuestRow(ulong minuteParticipantId, ulong guestMemberId) => new()
    {
        MinuteParticipantId = minuteParticipantId,
        MinutesId = MinutesId,
        UserId = null,
        GuestMemberId = guestMemberId,
        FullNameSnapshot = "đã có trong biên bản",
        AttendanceStatus = "PRESENT",
        DisplayOrder = 1,
        CreatedAt = new DateTime(2026, 8, 1),
    };

    // ── TC-MIN-01 ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SamePersonAsGuestAndExternalSupport_IsListedOnce_AsTheGuestRow()
    {
        using var db = WithGuests(
            Guest(101, "Nguyễn Văn A"),
            Guest(205, "Nguyễn Văn A", memberType: GuestMemberType.ExternalSupport));

        var rows = await GuestRowsAsync(db);

        var row = Assert.Single(rows);
        // The surviving row is the GUEST one — the delegation's own member, not the accompanying entry.
        Assert.Equal(101UL, row.GuestMemberId);
        Assert.Equal("Nguyễn Văn A", row.FullNameSnapshot);
    }

    /// <summary>
    /// Order in the form must not decide who survives: the same pair listed EXTERNAL_SUPPORT-first
    /// still resolves to the GUEST row, or the result would depend on data entry order.
    /// </summary>
    [Fact]
    public async Task GuestWins_EvenWhenTheExternalSupportRowComesFirst()
    {
        using var db = WithGuests(
            Guest(101, "Nguyễn Văn A", memberType: GuestMemberType.ExternalSupport),
            Guest(205, "Nguyễn Văn A"));

        var rows = await GuestRowsAsync(db);

        Assert.Equal(205UL, Assert.Single(rows).GuestMemberId);
    }

    // ── TC-MIN-02 ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SameName_DifferentOrganization_AreTwoPeople()
    {
        using var db = WithGuests(
            Guest(101, "Nguyễn Văn A", organization: "ABC"),
            Guest(205, "Nguyễn Văn A", organization: "XYZ", memberType: GuestMemberType.ExternalSupport));

        var rows = await GuestRowsAsync(db);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { 101UL, 205UL }, rows.Select(r => r.GuestMemberId!.Value).OrderBy(x => x));
    }

    /// <summary>
    /// Vietnamese diacritics are meaning, not formatting: "Vân" and "Van" are different names and stay
    /// two attendees. This is the boundary that keeps the rule from quietly merging real people.
    /// </summary>
    [Fact]
    public async Task NamesDifferingOnlyByDiacritics_AreTwoPeople()
    {
        using var db = WithGuests(
            Guest(101, "Nguyễn Thị Vân"),
            Guest(205, "Nguyen Thi Van", memberType: GuestMemberType.ExternalSupport));

        Assert.Equal(2, (await GuestRowsAsync(db)).Count);
    }

    // ── TC-MIN-03 ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CaseAndSpacingDifferences_AreTheSamePerson()
    {
        using var db = WithGuests(
            Guest(101, "  Nguyễn   Văn A ", organization: "Đại học ABC", jobTitle: "Manager"),
            Guest(205, "nguyễn văn a", organization: "đại  học abc", jobTitle: "MANAGER",
                memberType: GuestMemberType.ExternalSupport));

        Assert.Equal(101UL, Assert.Single(await GuestRowsAsync(db)).GuestMemberId);
    }

    // ── TC-MIN-04 ────────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// An internal user and a guest with the same name are two participants. They come from different
    /// source tables and are two different people in the system; comparing across them would let a
    /// staff member be dropped from a meeting record because a visitor shares their name.
    /// </summary>
    [Fact]
    public async Task InternalUserAndGuestWithTheSameName_AreBothListed()
    {
        using var db = WithGuests(Guest(101, "Nguyễn Văn A"));
        var staff = DelegationsTestData.CreateUser(
            301, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 900);
        staff.FullName = "Nguyễn Văn A";
        db.Users.Add(staff);
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            501, staff.UserId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted));
        db.SaveChanges();

        var instance = db.VisitRequestCampuses.Single(c => c.VisitInstanceId == DelegationsTestData.VisitInstanceId);
        var rows = await MinuteAutoFill.ComputeNewRowsAsync(
            db, instance, Array.Empty<MinuteParticipant>(), MinutesId, new DateTime(2026, 8, 1, 9, 0, 0), default);

        Assert.Single(rows, r => r.GuestMemberId == 101UL);
        Assert.Single(rows, r => r.UserId == staff.UserId);
    }

    // ── TC-MIN-05 ────────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// The case de-dup-at-source alone cannot cover: the EXTERNAL_SUPPORT row is ALREADY in the biên
    /// bản (so nothing about the source list is new), and the GUEST twin is offered as a candidate. By
    /// id it looks new. By identity the person is already there, and syncing must add nobody.
    /// </summary>
    [Fact]
    public async Task GuestTwinOfAnAlreadyRecordedExternalSupport_IsNotOfferedAgain()
    {
        using var db = WithGuests(
            Guest(101, "Nguyễn Văn A"),
            Guest(205, "Nguyễn Văn A", memberType: GuestMemberType.ExternalSupport));

        var rows = await GuestRowsAsync(db, ExistingGuestRow(900, guestMemberId: 205));

        Assert.Empty(rows);
    }

    /// <summary>A genuinely new guest still syncs — the identity check narrows, it does not block.</summary>
    [Fact]
    public async Task ADifferentGuest_IsStillOfferedAlongsideAnAlreadyRecordedOne()
    {
        using var db = WithGuests(
            Guest(101, "Nguyễn Văn A"),
            Guest(205, "Nguyễn Văn A", memberType: GuestMemberType.ExternalSupport),
            Guest(310, "Trần Thị B"));

        var rows = await GuestRowsAsync(db, ExistingGuestRow(900, guestMemberId: 205));

        Assert.Equal(310UL, Assert.Single(rows).GuestMemberId);
    }

    /// <summary>
    /// Sync is idempotent: feeding the first run's output back as existing rows produces nothing, so
    /// pressing "Đồng bộ người mới" repeatedly cannot accumulate duplicates.
    /// </summary>
    [Fact]
    public async Task RunningTheSyncAgainOverItsOwnOutput_AddsNobody()
    {
        using var db = WithGuests(
            Guest(101, "Nguyễn Văn A"),
            Guest(205, "Nguyễn Văn A", memberType: GuestMemberType.ExternalSupport),
            Guest(310, "Trần Thị B"));
        var instance = db.VisitRequestCampuses.Single(c => c.VisitInstanceId == DelegationsTestData.VisitInstanceId);
        var now = new DateTime(2026, 8, 1, 9, 0, 0);

        var first = await MinuteAutoFill.ComputeNewRowsAsync(
            db, instance, Array.Empty<MinuteParticipant>(), MinutesId, now, default);
        var second = await MinuteAutoFill.ComputeNewRowsAsync(db, instance, first, MinutesId, now, default);

        Assert.Equal(2, first.Count(r => r.GuestMemberId != null));
        Assert.Empty(second);
    }
}
