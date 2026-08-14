using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Minutes;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Minutes;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.Minutes;

/// <summary>
/// The biên bản attendance list is filled from two independent sources — internal participants
/// (<c>visit_participants</c>, keyed by user_id) and the delegation's own members
/// (<c>visit_instance_guest_members</c>, keyed by guest_member_id).
///
/// <para>
/// One person can legitimately be on both: invited as support AND listed among the delegation's
/// members. Their two rows carry different kinds of id, so the id de-dup could not see them as the
/// same person and the biên bản listed them twice — once as "Cán bộ IC", once as a guest, same name,
/// same organisation. These tests pin the fingerprint that closes that gap, and pin just as firmly
/// where it must NOT merge: two people who only share a name are two people.
/// </para>
/// <para>
/// They also cover the other half of the sync contract — that a row the editor has removed but not
/// saved is excluded from "what already exists", which is what lets the deleted person be offered
/// back in the same editing session.
/// </para>
/// </summary>
public class MinuteAutoFillTests
{
    private const ulong MinutesId = 500;
    private const ulong SupporterUserId = 201;
    private const ulong GuestMemberId = 301;

    // The label MinuteAutoFill derives for an ACCEPTED IC_SUPPORT participant, and the organisation it
    // derives from their department — a guest has to match BOTH to be the same person.
    private const string SupporterRole = "Cán bộ IC";
    private const string SupporterOrg = "Phòng ban 900";

    // The campus's operational contact, as seeded by DelegationsTestData.CreateVisitInstance.
    private const string ContactName = "Đầu mối cơ sở";
    private const string ContactJobTitle = "Trưởng phòng Hợp tác";
    private const string ContactOrg = "Đối tác";
    private const string ContactEmail = "op@test.local";

    // ── Cross-source duplicates ──────────────────────────────────────────────

    [Fact]
    public async Task Guest_matching_an_internal_participant_is_filled_in_once()
    {
        using var db = DelegationsTestDbContext.Create();
        var instance = SeedInstanceWithSupporterAndGuest(db,
            guestName: "Nguyễn Văn A", guestRole: SupporterRole, guestOrg: SupporterOrg);

        var rows = await ComputeAsync(db, instance);

        var forThatPerson = rows.Where(r => r.FullNameSnapshot == "Nguyễn Văn A").ToList();
        Assert.Single(forThatPerson);
        // The row that survives is the internal one: a user_id is a stronger identity than a
        // guest_member_id, and it is the row the rest of the system can join back to an account.
        Assert.Equal(SupporterUserId, forThatPerson[0].UserId);
        Assert.Null(forThatPerson[0].GuestMemberId);
    }

    [Fact]
    public async Task Same_name_at_a_different_organisation_is_a_different_person()
    {
        using var db = DelegationsTestDbContext.Create();
        var instance = SeedInstanceWithSupporterAndGuest(db,
            guestName: "Nguyễn Văn A", guestRole: SupporterRole, guestOrg: "ABC University");

        var rows = await ComputeAsync(db, instance);

        Assert.Equal(2, rows.Count(r => r.FullNameSnapshot == "Nguyễn Văn A"));
    }

    [Fact]
    public async Task Same_name_and_organisation_in_a_different_role_is_a_different_person()
    {
        using var db = DelegationsTestDbContext.Create();
        var instance = SeedInstanceWithSupporterAndGuest(db,
            guestName: "Nguyễn Văn A", guestRole: "Trưởng đoàn", guestOrg: SupporterOrg);

        var rows = await ComputeAsync(db, instance);

        Assert.Equal(2, rows.Count(r => r.FullNameSnapshot == "Nguyễn Văn A"));
    }

    [Fact]
    public async Task Fingerprint_ignores_letter_case_and_stray_whitespace()
    {
        using var db = DelegationsTestDbContext.Create();
        var instance = SeedInstanceWithSupporterAndGuest(db,
            guestName: "  nguyễn   văn a ", guestRole: SupporterRole.ToUpperInvariant(),
            guestOrg: $" {SupporterOrg} ");

        var rows = await ComputeAsync(db, instance);

        Assert.DoesNotContain(rows, r => r.GuestMemberId == GuestMemberId);
    }

    [Fact]
    public async Task Guest_matching_an_ALREADY_SAVED_internal_row_is_not_added_again()
    {
        using var db = DelegationsTestDbContext.Create();
        var instance = SeedInstanceWithSupporterAndGuest(db,
            guestName: "Nguyễn Văn A", guestRole: SupporterRole, guestOrg: SupporterOrg);

        // The internal row is already in the biên bản; only the guest is left to consider.
        var existing = new[]
        {
            SavedRow(1, userId: SupporterUserId, name: "Nguyễn Văn A", role: SupporterRole, org: SupporterOrg),
        };

        var rows = await ComputeAsync(db, instance, existing);

        Assert.DoesNotContain(rows, r => r.GuestMemberId == GuestMemberId);
    }

    [Fact]
    public async Task Two_guests_sharing_a_fingerprint_both_stay()
    {
        using var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        AddGuest(db, GuestMemberId, "Nguyễn Văn A", "Trưởng đoàn", "ABC University");
        AddGuest(db, GuestMemberId + 1, "Nguyễn Văn A", "Trưởng đoàn", "ABC University");
        db.SaveChanges();

        var rows = await ComputeAsync(db, instance);

        // The fingerprint only resolves internal-vs-guest. Two rows of the delegation's own member
        // list are two members, and dropping one would quietly shrink the delegation.
        Assert.Equal(2, rows.Count(r => r.GuestMemberId != null));
    }

    // ── Removed-but-unsaved rows (the "xóa rồi Đồng bộ" flow) ────────────────

    [Fact]
    public async Task A_removed_row_is_offered_again_once_it_stops_counting_as_existing()
    {
        using var db = DelegationsTestDbContext.Create();
        var instance = SeedInstanceWithSupporterAndGuest(db,
            guestName: "Trần Thị B", guestRole: "Trưởng đoàn", guestOrg: "ABC University");

        var savedHostRow = SavedRow(6, userId: DelegationsTestData.HostUserId,
            name: $"User {DelegationsTestData.HostUserId}", role: "Host", org: SupporterOrg);
        var savedGuestRow = SavedRow(7, guestMemberId: GuestMemberId, name: "Trần Thị B",
            role: "Trưởng đoàn", org: "ABC University");
        var savedSupporterRow = SavedRow(8, userId: SupporterUserId, name: "Nguyễn Văn A",
            role: SupporterRole, org: SupporterOrg);
        var savedContactRow = SavedContactRow(9);

        // Everything saved → nothing new to sync.
        Assert.Empty(await ComputeAsync(db, instance,
            new[] { savedHostRow, savedGuestRow, savedSupporterRow, savedContactRow }));

        // The editor deleted the guest row; the query drops it from "existing" before computing, which
        // is exactly what the handler does with ignoredExistingParticipantIds.
        var rows = await ComputeAsync(db, instance,
            new[] { savedHostRow, savedSupporterRow, savedContactRow });

        var candidate = Assert.Single(rows);
        Assert.Equal(GuestMemberId, candidate.GuestMemberId);
        Assert.Equal("Trần Thị B", candidate.FullNameSnapshot);
    }

    [Fact]
    public async Task Sync_only_ever_returns_people_who_are_in_a_source_list()
    {
        using var db = DelegationsTestDbContext.Create();
        var (instance, host) = DelegationsTestData.SeedBase(db);

        // Nothing but the host and the campus's operational contact is on any source list. So a row
        // typed in by hand has nothing to be regenerated FROM: removing a manual participant is
        // final, and sync cannot bring them back.
        var hostRow = SavedRow(1, userId: host.UserId, name: host.FullName, role: "Host", org: SupporterOrg);
        var rows = await ComputeAsync(db, instance, new[] { hostRow, SavedContactRow(2) });

        Assert.Empty(rows);
    }

    // ── The operational contact (NP-03) ──────────────────────────────────────
    //
    // Whether the person who actually ran the visit appeared in the record of it used to depend on
    // how the form had been filled in: named only in the "Đầu mối" block → they appeared nowhere,
    // because no source list contained them. Also typed into the delegation → they appeared, but the
    // only thing preventing a second row was a string comparison.

    [Fact]
    public async Task A_contact_who_is_not_in_any_source_list_is_still_added()
    {
        using var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);

        var rows = await ComputeAsync(db, instance);

        var contact = Assert.Single(rows.Where(r => r.FullNameSnapshot == ContactName));
        // A snapshot row: the contact is a person the delegation named, not necessarily an account
        // and not necessarily a member of the delegation.
        Assert.Null(contact.UserId);
        Assert.Null(contact.GuestMemberId);
        Assert.Contains("Đầu mối đoàn khách", contact.RoleSnapshot);
        Assert.Equal(ContactEmail, contact.EmailSnapshot);
    }

    [Fact]
    public async Task A_contact_LINKED_to_a_delegation_member_is_not_added_a_second_time()
    {
        using var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        // Same human, reached two ways: listed among the delegation AND named as the contact.
        AddGuest(db, GuestMemberId, ContactName, ContactJobTitle, ContactOrg);
        instance.FormDetail!.OperationalContactGuestMemberId = GuestMemberId;
        db.SaveChanges();

        var rows = await ComputeAsync(db, instance);

        var forThatPerson = rows.Where(r => r.FullNameSnapshot == ContactName).ToList();
        Assert.Single(forThatPerson);
        // The GUEST row is what stays — it is the one carrying the stable id.
        Assert.Equal(GuestMemberId, forThatPerson[0].GuestMemberId);
    }

    [Fact]
    public async Task A_contact_who_is_one_of_the_SUPPORT_staff_is_filled_in_once_as_that_member()
    {
        // The role is open to anybody travelling with the delegation — an interpreter, an assistant,
        // a coordinator — and those are written down in the support list, not the guest list. The
        // biên bản must treat that exactly like a guest holding the role: one row, carrying the id.
        using var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        AddGuest(db, GuestMemberId, ContactName, ContactJobTitle, ContactOrg, memberType: "EXTERNAL_SUPPORT");
        instance.FormDetail!.OperationalContactGuestMemberId = GuestMemberId;
        db.SaveChanges();

        var rows = await ComputeAsync(db, instance);

        var forThatPerson = rows.Where(r => r.FullNameSnapshot == ContactName).ToList();
        Assert.Single(forThatPerson);
        Assert.Equal(GuestMemberId, forThatPerson[0].GuestMemberId);
    }

    [Fact]
    public async Task A_member_who_only_SHARES_A_NAME_with_the_contact_does_not_absorb_them()
    {
        // Two different people, one of them the campus's contact. Merging on the name alone would
        // leave the person who actually ran the visit out of the record of it; the fingerprint
        // carries role and organisation precisely so it cannot.
        using var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        AddGuest(db, GuestMemberId, ContactName, "Sinh viên", "ABC University");
        db.SaveChanges();

        var rows = await ComputeAsync(db, instance);

        Assert.Equal(2, rows.Count(r => r.FullNameSnapshot == ContactName));
    }

    [Fact]
    public async Task A_contact_matching_a_member_by_name_role_and_org_is_not_duplicated()
    {
        using var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        // No stable link (a request created before the column existed), so all that is left is the
        // fingerprint — it still must not produce two rows for one person.
        AddGuest(db, GuestMemberId, ContactName, ContactJobTitle, ContactOrg);
        db.SaveChanges();

        var rows = await ComputeAsync(db, instance);

        Assert.Single(rows.Where(r => r.FullNameSnapshot == ContactName));
    }

    [Fact]
    public async Task A_contact_who_is_the_host_is_not_added_a_second_time()
    {
        using var db = DelegationsTestDbContext.Create();
        var (instance, host) = DelegationsTestData.SeedBase(db);
        instance.OperationalContactUserId = host.UserId;
        db.SaveChanges();

        var rows = await ComputeAsync(db, instance);

        Assert.DoesNotContain(rows, r => r.FullNameSnapshot == ContactName);
        Assert.Single(rows.Where(r => r.UserId == host.UserId));
    }

    [Fact]
    public async Task Running_the_fill_twice_adds_the_contact_once()
    {
        using var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);

        var first = await ComputeAsync(db, instance);
        // Feed the first run's output back in as "already saved" — exactly what the sync does.
        var second = await ComputeAsync(db, instance, first);

        Assert.Contains(first, r => r.FullNameSnapshot == ContactName);
        Assert.Empty(second);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Task<List<MinuteParticipant>> ComputeAsync(
        DelegationsTestDbContext db, VisitRequestCampus instance,
        IReadOnlyCollection<MinuteParticipant>? existing = null)
        => MinuteAutoFill.ComputeNewRowsAsync(
            (IApplicationDbContext)db, instance, existing ?? Array.Empty<MinuteParticipant>(),
            MinutesId, new DateTime(2026, 8, 1, 8, 0, 0), CancellationToken.None);

    /// <summary>Host + one ACCEPTED IC_SUPPORT participant named "Nguyễn Văn A" + one guest.</summary>
    private static VisitRequestCampus SeedInstanceWithSupporterAndGuest(
        DelegationsTestDbContext db, string guestName, string guestRole, string guestOrg)
    {
        var (instance, _) = DelegationsTestData.SeedBase(db);

        var supporter = DelegationsTestData.CreateUser(
            SupporterUserId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, departmentId: 900);
        supporter.FullName = "Nguyễn Văn A";
        db.Users.Add(supporter);
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            participantId: 1, userId: SupporterUserId,
            participantRole: ParticipantRoles.IcSupport, status: ParticipantStatuses.Accepted));

        AddGuest(db, GuestMemberId, guestName, guestRole, guestOrg);
        db.SaveChanges();
        return instance;
    }

    private static void AddGuest(
        DelegationsTestDbContext db, ulong guestMemberId, string fullName, string jobTitle, string organization,
        string memberType = "GUEST")
    {
        db.GuestMembers.Add(new VisitGuestMember
        {
            GuestMemberId = guestMemberId,
            VisitRequestId = DelegationsTestData.VisitRequestId,
            MemberType = memberType,
            FullName = fullName,
            JobTitle = jobTitle,
            Organization = organization,
            Nationality = "VN",
            CreatedAt = new DateTime(2026, 6, 1),
        });
        db.InstanceGuestMembers.Add(new VisitInstanceGuestMember
        {
            VisitRequestId = DelegationsTestData.VisitRequestId,
            VisitInstanceId = DelegationsTestData.VisitInstanceId,
            GuestMemberId = guestMemberId,
            CreatedAt = new DateTime(2026, 6, 1),
        });
    }

    /// <summary>The row a previous auto-fill would have written for the campus's operational contact.</summary>
    private static MinuteParticipant SavedContactRow(ulong id)
    {
        var row = SavedRow(id, name: ContactName,
            role: $"{ContactJobTitle} (Đầu mối đoàn khách)", org: ContactOrg);
        row.EmailSnapshot = ContactEmail;
        return row;
    }

    private static MinuteParticipant SavedRow(
        ulong id, string name, string role, string org, ulong? userId = null, ulong? guestMemberId = null)
        => new()
        {
            MinuteParticipantId = id,
            MinutesId = MinutesId,
            UserId = userId,
            GuestMemberId = guestMemberId,
            FullNameSnapshot = name,
            RoleSnapshot = role,
            OrganizationSnapshot = org,
            AttendanceStatus = "ABSENT",
            DisplayOrder = (uint)id,
            CreatedAt = new DateTime(2026, 8, 1, 8, 0, 0),
        };
}
