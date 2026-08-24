using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The before/after a reader actually sees when they open one history entry.
///
/// The drawer was reporting "(trống) → giá trị mới" for fields that had never been empty. Three causes,
/// all of them here: the write paths had drifted so a field present in one snapshot was missing from
/// the next; snapshots written in an older shape (nested contact, differently-cased member key) found
/// nothing on the left when compared property-by-property; and "there is no recorded previous value"
/// was rendered with the same words as "the previous value was blank".
///
/// Seed ids in pems_pr3_test: visitor owner = 8, Staff Leader campus1 = 3, IC Staff campus1 = 4.
/// </summary>
public sealed class VisitHistoryDetailDiffV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong VisitorOwner = 8, SlCampus1 = 3, IcStaffC1 = 4;
    /// <summary>ACTIVE Student — invited to SUPPORT the campus, which grants no history access.</summary>
    private const ulong SupportingStudent = 152;
    private const ulong Campus1 = 1;

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
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

    private static FakeUser Owner() => new() { UserId = VisitorOwner, RoleCode = RoleCodes.Visitor };

    private static GetVisitHistoryDetailQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new PerCampusFormV2Options { Enabled = true });

    /// <summary>One approved, hosted campus — enough to hang revisions off.</summary>
    private static async Task<(VisitRequest Request, VisitRequestCampus Instance)> SeedAsync(ApplicationDbContext db)
    {
        var now = DateTime.Now;
        var req = new VisitRequest
        {
            RequestCode = "HDIFF-" + Guid.NewGuid().ToString("N")[..11],
            RegistrantUserId = VisitorOwner,
            CreatedSource = "VISITOR_SUBMITTED",
            HasMixedCampusDetails = false,
            RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
            RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
            VisitScope = "SINGLE_CAMPUS",
            Status = "APPROVED", SubmittedAt = now, CreatedAt = now,
        };
        req.CampusInstances.Add(new VisitRequestCampus
        {
            CampusId = Campus1,
            PlannedStartAt = now.AddDays(20),
            PlannedEndAt = now.AddDays(20).AddHours(2),
            Status = "ASSIGNED",
            // Self-matched contact: the campus is past the confirmation gate, which the op-contact
            // guard trigger requires for any status beyond WAITING_CONTACT_CONFIRMATION.
            OperationalContactUserId = VisitorOwner,
            OperationalContactConfirmedAt = now,
            OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
            CurrentHostUserId = IcStaffC1,
            HostAssignedBy = SlCampus1, HostAssignedAt = now,
            DecidedBy = SlCampus1, DecidedAt = now,
            DecisionActorRole = "STAFF_LEADER", DecisionSource = "STANDARD_CAMPUS_REVIEW",
            CreatedAt = now,
            FormDetail = new VisitInstanceFormDetail
            {
                DelegationName = "DELEG", VisitType = "MEETING", Purpose = "P", WorkingContent = "C",
                OperationalContactFullName = "Op", OperationalContactOrganization = "OpOrg",
                OperationalContactJobTitle = "Trưởng phòng Hợp tác",
                OperationalContactPhone = "+8410", OperationalContactEmail = "op@example.com",
                WorkingLanguage = "VI", MediaConsentStatus = "AGREED",
                FormRevision = 2, ApprovalRevision = 1, CreatedAt = now,
            },
        });
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();
        return (req, req.CampusInstances.Single());
    }

    /// <summary>Writes revisions 1 and 2 with the given snapshots and returns revision 2's event id.</summary>
    private static async Task<string> TwoRevisionsAsync(
        ApplicationDbContext db, VisitRequest req, VisitRequestCampus inst, string? first, string second)
    {
        var now = DateTime.Now;
        var r1 = new VisitInstanceFormRevisionHistory
        {
            VisitRequestId = req.VisitRequestId, VisitInstanceId = inst.VisitInstanceId,
            FormRevision = 1, ApprovalRevision = 1, SourceType = "CREATE",
            SnapshotJson = first ?? "{}", AppliedBy = VisitorOwner, AppliedAt = now.AddMinutes(-10),
        };
        var r2 = new VisitInstanceFormRevisionHistory
        {
            VisitRequestId = req.VisitRequestId, VisitInstanceId = inst.VisitInstanceId,
            FormRevision = 2, ApprovalRevision = 1, SourceType = FormRevisionSourceTypes.SafeEdit,
            SnapshotJson = second, AppliedBy = VisitorOwner, AppliedAt = now,
        };
        db.VisitInstanceFormRevisionHistories.AddRange(r1, r2);
        await db.SaveChangesAsync();
        return VisitHistoryEventSources.Build(VisitHistoryEventSources.InstanceRevision, r2.RevisionHistoryId);
    }

    private static VisitHistoryFieldChangeDto? Field(VisitHistoryDetailDto d, string code)
        => d.FieldChanges.FirstOrDefault(f => f.FieldCode == code);

    // ── A real change reads as the real values ───────────────────────────────

    [Fact]
    public async Task A_changed_field_reports_the_value_it_actually_had_before()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        var eventId = await TwoRevisionsAsync(db, req, inst,
            """{"delegationName":"DELEG","visitType":"CAMPUS_TOUR","notes":null}""",
            """{"delegationName":"DELEG","visitType":"MEETING","notes":"Cần xe điện"}""");

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        var visitType = Field(detail, "visitType");
        Assert.NotNull(visitType);
        Assert.Equal("CAMPUS_TOUR", visitType!.BeforeValue);
        Assert.Equal("MEETING", visitType.AfterValue);
        Assert.False(visitType.BeforeUnknown);

        // A field that was genuinely null before IS reported as empty — the snapshot recorded it, and
        // what it recorded was nothing. That is a different statement from "no history", below.
        var notes = Field(detail, "notes");
        Assert.NotNull(notes);
        Assert.Null(notes!.BeforeValue);
        Assert.Equal("Cần xe điện", notes.AfterValue);
        Assert.False(notes.BeforeUnknown);

        // Unchanged fields stay out of the drawer entirely.
        Assert.Null(Field(detail, "delegationName"));

        await tx.RollbackAsync();
    }

    // ── Empty is not unknown ─────────────────────────────────────────────────

    [Fact]
    public async Task An_empty_previous_snapshot_reports_no_history_rather_than_an_empty_value()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        var eventId = await TwoRevisionsAsync(db, req, inst,
            "{}",
            """{"delegationName":"DELEG","visitType":"MEETING"}""");

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        var visitType = Field(detail, "visitType");
        Assert.NotNull(visitType);
        // The claim is "nobody recorded what this was", NOT "it was blank" — the drawer renders the
        // two differently, and asserting BeforeUnknown is what keeps them apart.
        Assert.True(visitType!.BeforeUnknown);
        Assert.Null(visitType.BeforeValue);
        Assert.Equal("MEETING", visitType.AfterValue);

        await tx.RollbackAsync();
    }

    // ── Older snapshot shapes still read ─────────────────────────────────────

    [Fact]
    public async Task A_legacy_nested_contact_snapshot_is_compared_against_todays_flat_one()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        var eventId = await TwoRevisionsAsync(db, req, inst,
            // The older shape: the contact nested, and the note under its old name.
            """
            {"delegationName":"DELEG","operationalContact":{"fullName":"Op","jobTitle":"Trưởng phòng Hợp tác","email":"op@example.com","phone":"+8410"},"noteToFptu":"Ghi chú cũ"}
            """,
            // Today's shape: flat contact fields, `notes`.
            """
            {"delegationName":"DELEG","operationalContactFullName":"Op","operationalContactJobTitle":"Trưởng phòng Hợp tác","operationalContactEmail":"op2@example.com","operationalContactPhone":"+8410","notes":"Ghi chú cũ"}
            """);

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        // The address moved, and the old one is READ from the nested shape rather than reported as
        // absent — which is exactly the "(trống) → op2@example.com" the drawer used to show.
        var email = Field(detail, "operationalContactEmail");
        Assert.NotNull(email);
        Assert.Equal("op@example.com", email!.BeforeValue);
        Assert.Equal("op2@example.com", email.AfterValue);
        Assert.False(email.BeforeUnknown);

        // The other contact fields did NOT move, so they produce no rows at all despite being spelled
        // differently on the two sides.
        Assert.Null(Field(detail, "operationalContactFullName"));
        Assert.Null(Field(detail, "operationalContactJobTitle"));
        Assert.Null(Field(detail, "operationalContactPhone"));
        // noteToFptu and notes are the same field under two names — same value, so no row.
        Assert.Null(Field(detail, "notes"));

        await tx.RollbackAsync();
    }

    // ── Member lists ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Member_changes_are_reported_whichever_way_the_member_key_was_spelled()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        // Written by the camelCase serializer ("members"); the differ used to look for "Members" with a
        // case-SENSITIVE lookup, found nothing, and silently reported no membership change at all.
        var eventId = await TwoRevisionsAsync(db, req, inst,
            """
            {"delegationName":"DELEG","members":[{"fullName":"Khách A","memberType":"GUEST","displayOrder":1}]}
            """,
            """
            {"delegationName":"DELEG","members":[{"fullName":"Khách A","memberType":"GUEST","displayOrder":1},{"fullName":"Khách B","memberType":"GUEST","displayOrder":2}]}
            """);

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        var added = detail.CollectionChanges
            .SingleOrDefault(c => c.ChangeType == VisitHistoryChangeTypes.Added);
        Assert.NotNull(added);
        Assert.Equal(VisitHistoryCollectionCodes.Visitors, added!.CollectionCode);
        Assert.Equal("Khách B", added.ItemKey);
        // The person who did not move is not reported as anything.
        Assert.DoesNotContain(detail.CollectionChanges, c => c.ItemKey == "Khách A");

        await tx.RollbackAsync();
    }

    // ── Contact-identity events ──────────────────────────────────────────────

    [Fact]
    public async Task A_contact_identity_event_is_openable_and_names_its_campus()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        var now = DateTime.Now;
        var change = new VisitRequestIdentityChange
        {
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = inst.VisitInstanceId,
            ChangeKind = IdentityChangeKinds.Transfer,
            ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
            // A handover names the owner it is taken FROM — trg_identity_change_transfer_bi refuses
            // a TRANSFER row without one, which is the schema saying the same thing the workflow does.
            OldUserId = VisitorOwner,
            OldEmailNormalized = "reg@example.com",
            NewEmailNormalized = "new@example.com",
            NewEmailMasked = "n***@example.com",
            Status = IdentityChangeStatuses.Pending,
            TokenVersion = 1, ExpectedRequestRowVersion = 0,
            RequestedBy = VisitorOwner, RequestedAt = now, ExpiresAt = now.AddHours(24),
            ResendCount = 0, CreatedAt = now,
        };
        db.VisitRequestIdentityChanges.Add(change);
        await db.SaveChangesAsync();

        var evt = new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = change.IdentityChangeId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = inst.VisitInstanceId,
            EventType = "OPERATIONAL_CONTACT_TRANSFER_REQUESTED",
            FromStatus = null, ToStatus = IdentityChangeStatuses.Pending,
            ActorUserId = VisitorOwner, EmailMasked = "n***@example.com",
            // Plumbing, deliberately: the detail must not surface it.
            Reason = "token_version=1;resend_count=0",
            CreatedAt = now,
        };
        db.VisitRequestIdentityChangeEvents.Add(evt);
        await db.SaveChangesAsync();

        // The timeline offers it with its own code and its campus, not one word for every transition.
        var timeline = await new GetVisitRequestHistoryQueryHandler(
                db, Owner(), new PerCampusFormV2Options { Enabled = true })
            .Handle(new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var entry = timeline.Entries.Single(e => e.EventCode == VisitHistoryEventCodes.ContactTransferRequested);
        Assert.NotNull(entry.EventId);
        Assert.Equal(inst.VisitInstanceId, entry.VisitInstanceId);
        Assert.Equal("n***@example.com", entry.MaskedEmail);

        // …and the drawer opens on it.
        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, entry.EventId!), CancellationToken.None);
        Assert.Equal(VisitHistoryEventCodes.ContactTransferRequested, detail.EventCode);
        Assert.Equal((long)Campus1, detail.CampusId);
        Assert.Equal("n***@example.com", Field(detail, "contactEmailMasked")!.AfterValue);
        Assert.Equal(IdentityChangeStatuses.Pending, Field(detail, "identityChangeStatus")!.AfterValue);
        // Never the unmasked address, and never the internal reason string.
        Assert.DoesNotContain(detail.FieldChanges, f => f.AfterValue == "new@example.com");
        Assert.Null(detail.Reason);

        await tx.RollbackAsync();
    }

    // ── The drawer is not a way around the timeline's scope ──────────────────

    [Fact]
    public async Task A_supporting_participant_cannot_open_a_history_entry_by_its_id()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        var eventId = await TwoRevisionsAsync(db, req, inst, "{\"purpose\":\"P\"}", "{\"purpose\":\"P2\"}");

        var now = DateTime.Now;
        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = inst.VisitInstanceId,
            UserId = SupportingStudent,
            ParticipantRole = ParticipantRoles.Student,
            IsHost = false,
            Status = ParticipantStatuses.Accepted,
            InvitedBy = SlCampus1, InvitedAt = now,
            AssignedBy = SlCampus1, AssignedAt = now,
            RespondedAt = now, CreatedAt = now, CreatedBy = SlCampus1,
        });
        await db.SaveChangesAsync();

        // They can see this campus's detail, and they hold a REAL event id belonging to it — the only
        // two things a caller needs to try the drawer endpoint directly. The refusal has to come from
        // the scope, not from the id being unguessable, or the timeline's scoping is decorative.
        var participant = new FakeUser
        {
            UserId = SupportingStudent, RoleCode = RoleCodes.Student, PrimaryCampusId = Campus1,
        };

        await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.ForbiddenException>(
            () => Handler(db, participant).Handle(
                new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None));

        await tx.RollbackAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // Operational-contact ↔ delegation-member RELATION (plan CanhIter3FixBug §11–§14) — the drawer
    // must never print a raw GuestMemberId, must resolve it to a name using THAT SNAPSHOT's own
    // member list (never the live roster), and must never fabricate a "before" state a legacy
    // snapshot never actually recorded.
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>A minimal member-array fragment carrying the fields the differ actually reads.</summary>
    private static string MemberJson(string name, ulong? guestMemberId) =>
        "{\"fullName\":\"" + name + "\",\"memberType\":\"GUEST\",\"displayOrder\":1"
        + (guestMemberId is { } id ? ",\"guestMemberId\":" + id : "") + "}";

    [Fact]
    public async Task H1_A_legacy_snapshot_with_no_relation_field_at_all_does_not_crash_the_reader()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        // Neither side ever recorded the relation — the field genuinely does not exist in either shape.
        var eventId = await TwoRevisionsAsync(db, req, inst,
            """{"delegationName":"DELEG","members":[""" + MemberJson("Khách A", null) + "]}",
            """{"delegationName":"DELEG2","members":[""" + MemberJson("Khách A", null) + "]}");

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        // Reached without throwing, and the relation genuinely produced no row — it never appeared on
        // either side, which is the honest "no history for this field" outcome (plan §13), not an
        // error.
        Assert.Null(Field(detail, "operationalContactGuestMemberId"));
        Assert.NotNull(Field(detail, "delegationName"));

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task H2_A_relation_appearing_for_the_first_time_is_BeforeUnknown_not_a_fabricated_null()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        // BEFORE: an old-shape snapshot that predates the relation field entirely (no key at all).
        // AFTER: today's shape, with the relation now pointing at a real member.
        var eventId = await TwoRevisionsAsync(db, req, inst,
            """{"delegationName":"DELEG","members":[""" + MemberJson("Khách A", 501) + "]}",
            """{"delegationName":"DELEG","operationalContactGuestMemberId":501,"members":["""
                + MemberJson("Khách A", 501) + "]}");

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        var relation = Field(detail, "operationalContactGuestMemberId");
        Assert.NotNull(relation);
        // The field genuinely has no recorded "before" — BeforeUnknown, and BeforeValue null. It must
        // NEVER read as "Không nằm trong danh sách đoàn → Khách A" (that would assert the OLD snapshot
        // positively recorded "outside the delegation", which it never did — plan §13's exact trap).
        Assert.True(relation!.BeforeUnknown);
        Assert.Null(relation.BeforeValue);
        Assert.Equal("Khách A", relation.AfterValue);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task H3_A_to_B_resolves_both_sides_to_the_names_that_snapshot_actually_recorded()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        var membersJson = "[" + MemberJson("Khách A", 501) + "," + MemberJson("Khách B", 502) + "]";
        var eventId = await TwoRevisionsAsync(db, req, inst,
            $$"""{"delegationName":"DELEG","operationalContactGuestMemberId":501,"members":{{membersJson}}}""",
            $$"""{"delegationName":"DELEG","operationalContactGuestMemberId":502,"members":{{membersJson}}}""");

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        var relation = Field(detail, "operationalContactGuestMemberId");
        Assert.NotNull(relation);
        Assert.False(relation!.BeforeUnknown);
        Assert.Equal("Khách A", relation.BeforeValue);
        Assert.Equal("Khách B", relation.AfterValue);
        // The raw ids must never leak into either cell.
        Assert.DoesNotContain("501", relation.BeforeValue);
        Assert.DoesNotContain("502", relation.AfterValue);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task H4_A_to_outside_resolves_to_the_name_then_the_stable_not_in_delegation_code()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        var membersJson = "[" + MemberJson("Khách A", 501) + "]";
        var eventId = await TwoRevisionsAsync(db, req, inst,
            $$"""{"delegationName":"DELEG","operationalContactGuestMemberId":501,"members":{{membersJson}}}""",
            $$"""{"delegationName":"DELEG","operationalContactGuestMemberId":null,"members":{{membersJson}}}""");

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        var relation = Field(detail, "operationalContactGuestMemberId");
        Assert.NotNull(relation);
        Assert.False(relation!.BeforeUnknown);
        Assert.Equal("Khách A", relation.BeforeValue);
        // A STABLE code, for the frontend to translate — never a raw null/blank that could be confused
        // with "no history", and never a guessed name.
        Assert.Equal("NOT_IN_DELEGATION", relation.AfterValue);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task H5_Outside_to_A_resolves_the_stable_code_then_the_name()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        var membersJson = "[" + MemberJson("Khách A", 501) + "]";
        var eventId = await TwoRevisionsAsync(db, req, inst,
            $$"""{"delegationName":"DELEG","operationalContactGuestMemberId":null,"members":{{membersJson}}}""",
            $$"""{"delegationName":"DELEG","operationalContactGuestMemberId":501,"members":{{membersJson}}}""");

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        var relation = Field(detail, "operationalContactGuestMemberId");
        Assert.NotNull(relation);
        Assert.False(relation!.BeforeUnknown);
        Assert.Equal("NOT_IN_DELEGATION", relation.BeforeValue);
        Assert.Equal("Khách A", relation.AfterValue);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task H6_A_relation_id_that_predates_per_member_ids_resolves_to_a_stable_code_never_a_guess()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await SeedAsync(db);
        // Transitional shape: the top-level relation field exists, but the member rows carry no
        // guestMemberId at all (a snapshot written between the two additions in this feature). There is
        // exactly one member, and the temptation is to assume the id must mean THAT one — the reader
        // must resist it: an id it cannot verify is unresolvable, not "the only candidate".
        var eventId = await TwoRevisionsAsync(db, req, inst,
            """{"delegationName":"DELEG","operationalContactGuestMemberId":null,"members":[""" + MemberJson("Khách A", null) + "]}",
            """{"delegationName":"DELEG","operationalContactGuestMemberId":999,"members":[""" + MemberJson("Khách A", null) + "]}");

        var detail = await Handler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);

        var relation = Field(detail, "operationalContactGuestMemberId");
        Assert.NotNull(relation);
        Assert.Equal("NOT_IN_DELEGATION", relation!.BeforeValue);
        // Never "Khách A" (a guess) and never the raw "999".
        Assert.Equal("UNRESOLVABLE", relation.AfterValue);
        Assert.DoesNotContain("999", relation.AfterValue);
        Assert.NotEqual("Khách A", relation.AfterValue);

        await tx.RollbackAsync();
    }
}
