using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Delegations.SetupProgressEmail;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.UnitTests.Delegations.ExportScheduleReport;
using Xunit;

namespace PEMS.UnitTests.Delegations.SetupProgressEmail;

/// <summary>
/// The default TO/CC of the Host's setup-progress email.
///
/// <para>
/// Every case here is one the frontend could have got wrong if it had built the list from what the
/// compose screen happened to have loaded: an address that appears on both sides of the delegation, a
/// participant who was invited but never answered, a request whose guest side PEMS holds no address for
/// at all.
/// </para>
/// </summary>
public class VisitSetupProgressRecipientResolverTests
{
    private const ulong Instance = ScheduleReportTestData.VisitInstanceId;

    private static (ScheduleReportTestDbContext Db, VisitSetupProgressRecipientResolver Resolver) CreateSut(
        string? contactEmail = "contact@test.local",
        string? registrantEmail = "guest@test.local")
    {
        var db = ScheduleReportTestDbContext.Create();
        ScheduleReportTestData.SeedBase(db);

        var visit = db.VisitRequests.Single();
        visit.RegistrantEmail = registrantEmail!;
        // The contact is the CAMPUS’s, so it is set on that campus’s form detail.
        db.VisitInstanceFormDetails.Single(d => d.VisitInstanceId == Instance)
            .OperationalContactEmail = contactEmail!;
        db.SaveChanges();

        return (db, new VisitSetupProgressRecipientResolver(db));
    }

    private static Task<SetupProgressRecipients> ResolveAsync(
        ScheduleReportTestDbContext db, VisitSetupProgressRecipientResolver resolver)
    {
        var instance = db.VisitRequestCampuses.Include(c => c.VisitRequest).Single(c => c.VisitInstanceId == Instance);
        return resolver.ResolveAsync(instance, default);
    }

    /// <summary>Adds a user and an accepted participant row for them, and returns their address.</summary>
    private static string AddAcceptedParticipant(
        ScheduleReportTestDbContext db, ulong userId, ulong participantId, string? email = null)
    {
        var user = ScheduleReportTestData.CreateUser(userId, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null);
        if (email is not null) user.Email = email;
        db.Users.Add(user);
        db.VisitParticipants.Add(ScheduleReportTestData.CreateParticipant(
            participantId, userId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted));
        db.SaveChanges();
        return user.Email;
    }

    // ── The guest side ──────────────────────────────────────────────────────

    [Fact]
    public async Task Contact_and_registrant_are_both_primary_recipients_when_they_differ()
    {
        var (db, resolver) = CreateSut();

        var result = await ResolveAsync(db, resolver);

        Assert.Equal(
            new[] { "contact@test.local", "guest@test.local" },
            result.To.Select(r => r.Email).OrderBy(e => e).ToArray());
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task The_same_person_as_contact_and_registrant_appears_once()
    {
        var (db, resolver) = CreateSut(contactEmail: "same@test.local", registrantEmail: "SAME@test.local");

        var result = await ResolveAsync(db, resolver);

        // Case is not identity for an address: two spellings of one mailbox would send that person the
        // message twice and show the guest a duplicated To header.
        Assert.Single(result.To);
    }

    [Fact]
    public async Task A_malformed_stored_address_is_dropped_rather_than_passed_on()
    {
        var (db, resolver) = CreateSut(contactEmail: "not-an-address");

        var result = await ResolveAsync(db, resolver);

        Assert.Equal(new[] { "guest@test.local" }, result.To.Select(r => r.Email).ToArray());
    }

    // ── The FPT side ────────────────────────────────────────────────────────

    [Fact]
    public async Task Accepted_participants_are_copied_and_the_host_is_not()
    {
        var (db, resolver) = CreateSut();
        var accepted = AddAcceptedParticipant(db, 401, 1);
        // The host has a participant row of their own in some flows; they are the sender either way.
        db.VisitParticipants.Add(ScheduleReportTestData.CreateParticipant(
            2, ScheduleReportTestData.HostUserId, ParticipantRoles.IcHost, ParticipantStatuses.Accepted, isHost: true));
        db.SaveChanges();

        var result = await ResolveAsync(db, resolver);

        Assert.Equal(new[] { accepted }, result.Cc.Select(r => r.Email).ToArray());
        Assert.DoesNotContain(result.Cc, r => r.Email == $"user{ScheduleReportTestData.HostUserId}@test.local");
    }

    [Theory]
    [InlineData(ParticipantStatuses.Invited)]
    [InlineData(ParticipantStatuses.Declined)]
    [InlineData(ParticipantStatuses.Removed)]
    [InlineData(ParticipantStatuses.Assigned)]
    public async Task Only_accepted_participants_are_copied(string status)
    {
        var (db, resolver) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(402, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.VisitParticipants.Add(ScheduleReportTestData.CreateParticipant(
            3, 402, ParticipantRoles.IcSupport, status));
        db.SaveChanges();

        var result = await ResolveAsync(db, resolver);

        // Copying somebody who has not said yes presents them to the GUEST as part of a reception they
        // have not agreed to join.
        Assert.Empty(result.Cc);
    }

    [Fact]
    public async Task A_participant_whose_address_is_also_the_guest_contact_stays_a_primary_recipient()
    {
        var (db, resolver) = CreateSut();
        AddAcceptedParticipant(db, 403, 4, email: "contact@test.local");

        var result = await ResolveAsync(db, resolver);

        Assert.Contains(result.To, r => r.Email == "contact@test.local");
        Assert.Empty(result.Cc);
    }

    // ── Fallbacks ───────────────────────────────────────────────────────────

    [Fact]
    public async Task With_no_guest_address_the_first_accepted_participant_becomes_the_primary_recipient()
    {
        var (db, resolver) = CreateSut(contactEmail: "", registrantEmail: "");
        var first = AddAcceptedParticipant(db, 404, 5);
        var second = AddAcceptedParticipant(db, 405, 6);

        var result = await ResolveAsync(db, resolver);

        Assert.Equal(new[] { first }, result.To.Select(r => r.Email).ToArray());
        Assert.Equal(new[] { second }, result.Cc.Select(r => r.Email).ToArray());
        // The Host must be told: a message addressed to a colleague is not the one they set out to send.
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task With_no_candidate_at_all_the_draft_is_left_unsendable_and_says_why()
    {
        var (db, resolver) = CreateSut(contactEmail: "", registrantEmail: "");

        var result = await ResolveAsync(db, resolver);

        Assert.Empty(result.To);
        Assert.Empty(result.Cc);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task Blind_copies_are_never_pre_filled()
    {
        var (db, resolver) = CreateSut();
        AddAcceptedParticipant(db, 406, 7);

        var result = await ResolveAsync(db, resolver);

        // A BCC nobody chose is a BCC nobody can account for.
        Assert.Empty(result.Bcc);
    }
}
