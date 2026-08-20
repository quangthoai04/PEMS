using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// PR-3 persistence + dual-read + authorization tests for per-campus visit form v2. Runs against a
/// DISPOSABLE MySQL database <c>pems_pr3_test</c> built from the PR-2 fresh-create master — never the
/// real pems_db / pems_test. Each test seeds inside a transaction it rolls back, so the DB stays clean
/// and tests are independent. The whole class self-skips (throws a clear Skip) when the DB is absent.
///
/// Seed ids in pems_pr3_test: visitor owner = 8, other visitor = 22, Staff Leader campus1 = 3,
/// Staff Leader campus2 = 9, IC Staff campus1 (host) = 4, HO = 2, Admin = 1, VISITOR role_id = 6.
/// </summary>
public sealed class PerCampusFormV2ReadTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong VisitorOwner = 8, VisitorOther = 22, SlCampus1 = 3, SlCampus2 = 9,
                        IcStaffC1 = 4, HoUser = 2, AdminUser = 1;
    private const ulong Campus1 = 1, Campus2 = 2;

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString))
            .Options;
        return new ApplicationDbContext(options);
    }

    // These are MySQL-backed integration tests (like the repo's other pems_test tests). They require
    // the disposable pems_pr3_test database (PR-2 master imported). A clear, non-silent failure if absent.
    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master into it to run these tests.");
    }

    /// <summary>
    /// Every contact action names a campus, so they are carried by the campus rows rather than by
    /// the request-level viewer block. Flattened here because these tests seed a single campus.
    /// </summary>
    private static List<string> CampusActions(ResolvedVisitFormDto resolved)
        => resolved.CampusVisits.SelectMany(c => c.AllowedActions).ToList();

    private static VisitFormReadService Resolver(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, NullLogger<VisitFormReadService>.Instance);

    // ── Fixture builders ────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed = false) => new()
    {
        RequestCode = "PR3-" + Guid.NewGuid().ToString("N")[..12],
        RegistrantUserId = VisitorOwner,
        CreatedSource = "VISITOR_SUBMITTED",
        HasMixedCampusDetails = mixed,
        RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
        RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
        VisitScope = scope,
        // Pure V2: form content is per campus (see the detail builder). The request row keeps only the
        // PRIMARY contact — a request-level relation, distinct from each campus's operational contact.
        Status = "PENDING_APPROVAL", SubmittedAt = DateTime.Now, CreatedAt = DateTime.Now,
    };

    private static VisitRequestCampus NewInstance(ulong campusId, ulong? hostUserId = null) => new()
    {
        CampusId = campusId,
        PlannedStartAt = DateTime.Now.AddDays(20),
        PlannedEndAt = DateTime.Now.AddDays(20).AddHours(2),
        Status = hostUserId is null ? "WAITING_REQUEST_APPROVAL" : "ASSIGNED",
        // Self-matched: the registrant is this campus's operational contact, so the campus sits past
        // the confirmation gate. A campus beyond WAITING_CONTACT_CONFIRMATION with no contact is
        // refused by trg_visit_campuses_op_contact_guard_bi. Tests that need the gate SHUT call
        // MakeContactUnconfirmedAsync, which puts the campus back.
        OperationalContactUserId = VisitorOwner,
        OperationalContactConfirmedAt = DateTime.Now,
        OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
        CurrentHostUserId = hostUserId,
        HostAssignedBy = hostUserId is null ? null : SlCampus1,
        HostAssignedAt = hostUserId is null ? null : DateTime.Now,
        DecidedBy = hostUserId is null ? null : SlCampus1,
        DecidedAt = hostUserId is null ? null : DateTime.Now,
        DecisionActorRole = hostUserId is null ? null : "STAFF_LEADER",
        DecisionSource = hostUserId is null ? null : "STANDARD_CAMPUS_REVIEW",
        CreatedAt = DateTime.Now,
    };

    private static VisitInstanceFormDetail NewDetail(string tag) => new()
    {
        DelegationName = $"DELEG-{tag}", VisitType = "MEETING", Purpose = $"PURPOSE-{tag}",
        WorkingContent = $"CONTENT-{tag}",
        OperationalContactFullName = $"Op-{tag}", OperationalContactOrganization = $"OpOrg-{tag}", OperationalContactJobTitle = "Trưởng phòng Hợp tác",
        OperationalContactPhone = "+8410", OperationalContactEmail = $"op-{tag}@example.com",
        WorkingLanguage = tag == "B" ? "VI" : "EN", MediaConsentStatus = "AGREED",
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitGuestMember NewMember(ulong requestId, string name, string type = "GUEST") => new()
    {
        VisitRequestId = requestId, MemberType = type, FullName = name,
        Organization = "GOrg", JobTitle = "GJob", Nationality = "VN", CreatedAt = DateTime.Now,
    };

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
    private static FakeUser Ho() => new() { UserId = HoUser, RoleCode = RoleCodes.Ho };
    private static FakeUser Admin() => new() { UserId = AdminUser, RoleCode = RoleCodes.Admin };
    private static FakeUser Unrelated() => new() { UserId = VisitorOther, RoleCode = RoleCodes.Visitor };
    private static FakeUser StaffLeader(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId };
    private static FakeUser Host(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = campusId };

    // ── 1. Persistence / model mapping against the real PR-2 MySQL schema ─────

    [Fact]
    public async Task Model_builds_and_new_dbsets_query_against_pr2_schema()
    {
        RequireDb();
        using var db = NewContext();
        // Touching each new DbSet forces EF to build the model against the real schema (composite
        // FKs, alternate keys, one-to-one shared PK). A model error would throw here.
        Assert.Equal(0, await db.VisitInstanceFormDetails.CountAsync(d => d.VisitInstanceId == 0));
        Assert.Equal(0, await db.VisitInstanceGuestMembers.CountAsync(l => l.VisitInstanceId == 0));
        Assert.Equal(0, await db.VisitRequestIdentityChanges.CountAsync(c => c.IdentityChangeId == 0));
        Assert.Equal(0, await db.VisitRequestIdentityChangeEvents.CountAsync(e => e.IdentityChangeEventId == 0));
        Assert.Equal(0, await db.VisitInstanceAmendments.CountAsync(a => a.AmendmentId == 0));
        Assert.Equal(0, await db.VisitInstanceAmendmentChanges.CountAsync(a => a.AmendmentChangeId == 0));
        Assert.Equal(0, await db.VisitInstanceFormRevisionHistories.CountAsync(h => h.RevisionHistoryId == 0));
        Assert.Equal(0, await db.VisitRequestRevisionHistories.CountAsync(h => h.RequestRevisionHistoryId == 0));
    }

    [Fact]
    public async Task Generated_guard_columns_are_not_written_on_insert()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        // Insert a PENDING_APPROVAL amendment + a PENDING identity change. If EF tried to write the
        // VIRTUAL guard columns (amendment_pending_guard / pending_guard) MySQL would reject the
        // INSERT (error 3105). A clean SaveChanges proves EF excludes the generated columns.
        db.VisitInstanceAmendments.Add(new VisitInstanceAmendment
        {
            VisitRequestId = req.VisitRequestId, VisitInstanceId = instances[0].VisitInstanceId,
            AmendmentNo = 1, Status = AmendmentStatuses.PendingApproval,
            BaseFormRevision = 1, BaseApprovalRevision = 1, RequestedBy = VisitorOwner,
            RequestedAt = DateTime.Now, ExpectedInstanceRowVersion = 0, CreatedAt = DateTime.Now,
        });
        db.VisitRequestIdentityChanges.Add(new VisitRequestIdentityChange
        {
            VisitRequestId = req.VisitRequestId, VisitInstanceId = instances[0].VisitInstanceId,
            ChangeKind = IdentityChangeKinds.InitialConfirmation, NewEmailMasked = "n***@e.com",
            Status = IdentityChangeStatuses.Pending, ExpectedRequestRowVersion = 0,
            RequestedBy = VisitorOwner, RequestedAt = DateTime.Now, ExpiresAt = DateTime.Now.AddHours(72),
            CreatedAt = DateTime.Now,
        });

        var affected = await db.SaveChangesAsync();
        Assert.True(affected >= 2);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Resolved_campus_rowversion_is_the_instance_token_not_the_form_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        // Simulate a campus-approve: the CAMPUS INSTANCE row_version is bumped, the form-detail's is not.
        // The read model must surface the instance token — that is what pending-edit / safe-edit / amendment
        // all check against — so a safe-edit/amendment on a freshly-loaded ASSIGNED detail never 409s. (Caught
        // by the real-stack member-amendment journey: exposing the form-detail version here 409'd the submit.)
        var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instances[0].VisitInstanceId);
        instance.RowVersion += 5;
        await db.SaveChangesAsync();
        var detail = await db.VisitInstanceFormDetails.AsNoTracking()
            .SingleAsync(d => d.VisitInstanceId == instances[0].VisitInstanceId);

        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var campus = Assert.Single(dto.CampusVisits);
        Assert.Equal(instance.RowVersion, campus.RowVersion);   // the instance token the write paths check
        Assert.NotEqual(detail.RowVersion, campus.RowVersion);  // the diverged form-detail version is NOT surfaced

        await tx.RollbackAsync();
    }

    // ── 2. Dual-read v1 ───────────────────────────────────────────────────────

    [Fact]
    /// <summary>
    /// Pure V2 replaces the old "V1 resolves from the global projection" case. There is no global
    /// projection left, so an instance WITHOUT its own detail row is a data error: the resolver must fail
    /// loudly instead of quietly rendering empty or borrowed content.
    /// </summary>
    public async Task Instance_without_detail_fails_loudly_instead_of_falling_back()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV1Async(db, new[] { Campus1 }); // seeds an instance with NO form detail
        var ex = await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.ConflictException>(
            () => Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));

        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    /// <summary>
    /// Pure V2 replaces the old "every campus shares one global snapshot" case: sharing content across
    /// campuses is exactly what the schema abolished. A multi-campus request missing its details must
    /// fail rather than hand every campus the same borrowed values.
    /// </summary>
    public async Task Multi_campus_without_details_fails_instead_of_sharing_one_snapshot()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV1Async(db, new[] { Campus1, Campus2 }); // instances with NO form details
        var ex = await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.ConflictException>(
            () => Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));

        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    // ── 3. Dual-read v2 ───────────────────────────────────────────────────────

    [Fact]
    public async Task V2_single_campus_reads_detail_and_links()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.False(dto.HasMixedCampusDetails);
        var c = Assert.Single(dto.CampusVisits);
        Assert.Equal("DELEG-A", c.DelegationName);
        Assert.Equal("PURPOSE-A", c.Purpose);
        Assert.Equal("op-A@example.com", c.OperationalContact.Email); // per-campus operational contact
        Assert.Contains(c.Visitors, m => m.FullName == "A-guest");     // per-campus link
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_campus_mixed_keeps_each_campus_independent()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.True(dto.HasMixedCampusDetails);
        var a = dto.CampusVisits.Single(c => c.CampusId == (long)Campus1);
        var b = dto.CampusVisits.Single(c => c.CampusId == (long)Campus2);
        Assert.Equal("DELEG-A", a.DelegationName);
        Assert.Equal("DELEG-B", b.DelegationName);            // campus B not equal to campus A
        Assert.Equal("PURPOSE-A", a.Purpose);
        Assert.Equal("PURPOSE-B", b.Purpose);
        Assert.Equal("EN", a.WorkingLanguage);
        Assert.Equal("VI", b.WorkingLanguage);
        Assert.Contains(a.Visitors, m => m.FullName == "A-guest");
        Assert.Contains(b.Visitors, m => m.FullName == "B-guest");
        Assert.DoesNotContain(a.Visitors, m => m.FullName == "B-guest"); // no cross-campus member leak
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_consistency_error_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        // Delete the only detail row → a v2 instance with no per-campus detail.
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instances[0].VisitInstanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    // ── 4. Authorization / scope ─────────────────────────────────────────────

    [Fact]
    public async Task StaffLeader_sees_only_own_campus_hidden_campus_not_in_payload()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true);

        var dtoA = await Resolver(db, StaffLeader(SlCampus1, Campus1)).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var a = Assert.Single(dtoA.CampusVisits);
        Assert.Equal((long)Campus1, a.CampusId);
        Assert.DoesNotContain(dtoA.CampusVisits, c => c.CampusId == (long)Campus2); // hidden campus absent
        Assert.False(dtoA.Viewer.CanViewAllCampuses);
        Assert.Equal(VisitInstanceAccessRelations.StaffLeader, dtoA.Viewer.Relation);

        var dtoB = await Resolver(db, StaffLeader(SlCampus2, Campus2)).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var b = Assert.Single(dtoB.CampusVisits);
        Assert.Equal((long)Campus2, b.CampusId);
        await tx.RollbackAsync();
    }

    // ── The pending-campus edit capability, and who is offered it ───────────────────────────────

    /// <summary>
    /// A capability that promises what the handler refuses is worse than no capability at all, so this
    /// is the read-model half of the rule the command enforces: the campus's Staff Leader does NOT get
    /// the pending edit on a request somebody else filed, and does not get the flag that draws the
    /// 72-hour override and the "Lưu và duyệt" button either.
    ///
    /// <para>
    /// What they keep is asserted in the same breath, because that is the regression that matters: they
    /// still see the campus, and the handover capability still comes back — approval and rejection live
    /// on separate commands with their own list actions, and this rule never reached them.
    /// </para>
    /// </summary>
    [Fact]
    public async Task EditPendingCampus_is_withheld_from_a_campus_leader_who_did_not_file_the_request()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Seeded registrant is VisitorOwner — the leader is a different person entirely.
        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        var leaderView = await Resolver(db, StaffLeader(SlCampus1, Campus1))
            .ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var campus = Assert.Single(leaderView.CampusVisits);
        Assert.DoesNotContain(VisitFormActions.EditPendingCampus, campus.AllowedActions);
        // Not merely disabled — absent. A relation refusal is not a near miss the reader can wait out,
        // and rendering it greyed out would suggest the door might open later.
        Assert.DoesNotContain(campus.Capabilities, c => c.Code == VisitFormActions.EditPendingCampus);
        Assert.False(campus.CanOverrideScheduleLeadTime);
        // No EditPendingCampus at all here, so CanSaveAndApprove (its own contract — see the DTO's
        // doc comment) must agree rather than accidentally offering "Lưu và duyệt" on a screen this
        // actor cannot even open.
        Assert.False(campus.CanSaveAndApprove);

        // The registrant is untouched by any of this.
        var ownerView = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.Contains(VisitFormActions.EditPendingCampus,
            Assert.Single(ownerView.CampusVisits).AllowedActions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// A Staff Leader of a DIFFERENT campus who filed the request. They are its registrant, so the edit
    /// is offered — being a leader somewhere else is a fact about that campus and takes nothing away
    /// here. What is withheld is the leader-only pair: no 72-hour override, no "Lưu và duyệt", because
    /// those belong to whoever has to prepare THIS campus, and that is somebody else.
    /// </summary>
    [Fact]
    public async Task EditPendingCampus_is_offered_to_a_registrant_who_leads_a_different_campus()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // One campus: Campus1. The registrant is the Staff Leader of Campus2.
        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        req.RegistrantUserId = SlCampus2;
        await db.SaveChangesAsync();

        var view = await Resolver(db, StaffLeader(SlCampus2, Campus2))
            .ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var campus = Assert.Single(view.CampusVisits);
        Assert.Equal((long)Campus1, campus.CampusId);
        Assert.Contains(VisitFormActions.EditPendingCampus, campus.AllowedActions);
        // The flag the client draws the 72-hour override dialog from.
        Assert.False(campus.CanOverrideScheduleLeadTime);
        // The SEPARATE flag the "Lưu và duyệt" button itself renders from — same actor, same false,
        // for its own reason: they edit Campus1 as its registrant but do not lead it.
        Assert.False(campus.CanSaveAndApprove);
        // And nothing about deciding Campus1 leaked in with it: the handover is the leader's action on
        // the campus they actually lead, and this is not it.
        Assert.DoesNotContain(VisitFormActions.TransferHost, campus.AllowedActions);
        // Nor does the ORDINARY decision — approve/reject are Campus1's own leader's business, and
        // being Campus1's registrant does not make this actor Campus1's leader. Being a Staff Leader
        // of a DIFFERENT campus must never be read as "Staff Leader, therefore only own-campus
        // actions" — the registrant-side rights above are real and independent of it.
        Assert.DoesNotContain(VisitListActions.ApproveAndAssignHost, campus.AllowedActions);
        Assert.DoesNotContain(VisitListActions.CampusReject, campus.AllowedActions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// The same leader, on a request they filed themselves. Both halves of the rule hold, so the edit is
    /// offered — and with it the flag that carries the 72-hour override and "Lưu và duyệt", which are
    /// the two things that only ever existed inside this screen.
    /// </summary>
    [Fact]
    public async Task EditPendingCampus_is_offered_to_a_campus_leader_who_filed_the_request()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        req.RegistrantUserId = SlCampus1;
        await db.SaveChangesAsync();

        var leaderView = await Resolver(db, StaffLeader(SlCampus1, Campus1))
            .ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var campus = Assert.Single(leaderView.CampusVisits);
        Assert.Contains(VisitFormActions.EditPendingCampus, campus.AllowedActions);
        // Exactly once. The requester side and the leader used to add it separately, which put the same
        // code in the list twice for the one person who is both.
        Assert.Single(campus.Capabilities, c => c.Code == VisitFormActions.EditPendingCampus);
        Assert.True(campus.CanOverrideScheduleLeadTime);
        // Same actor, own separate verdict: the leader-registrant pairing grants BOTH the 72-hour
        // override AND "Lưu và duyệt" — they happen to agree today (both ActsAsCampusLeader), but
        // CanSaveAndApprove must be readable on its own rather than inferred from the other field.
        Assert.True(campus.CanSaveAndApprove);
        await tx.RollbackAsync();
    }

    // ── Ordinary campus decision (APPROVE_AND_ASSIGN_HOST / CAMPUS_REJECT) — the V2 Detail gap ────
    //
    // Distinct from EditPendingCampus above: EDIT right and DECISION right are separate questions.
    // A Staff Leader decides their OWN campus ordinarily whether or not they filed the request —
    // exactly the actions ViewGuestDelegationListQueryHandler has offered the list screen all along
    // (VisitListActions.ApproveAndAssignHost / .CampusReject), now also offered here so a leader who
    // opens the V2 Detail screen instead of the list is not stuck with nothing to click.

    /// <summary>
    /// The campus's own Staff Leader, on a request somebody else filed (the ordinary case — approving
    /// and rejecting never required leading AND registering). WAITING_REQUEST_APPROVAL, request active,
    /// contact gate open: every condition ApproveCampusInstanceCommandHandler/RejectCampusInstanceCommandHandler
    /// re-check independently is already true, so the read model must offer both.
    /// </summary>
    [Fact]
    public async Task OrdinaryCampusDecision_is_offered_to_this_campus_own_Staff_Leader_while_waiting()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Seeded registrant is VisitorOwner — the leader below did not file this request.
        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        var leaderView = await Resolver(db, StaffLeader(SlCampus1, Campus1))
            .ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var campus = Assert.Single(leaderView.CampusVisits);
        Assert.Contains(VisitListActions.ApproveAndAssignHost, campus.AllowedActions);
        Assert.Contains(VisitListActions.CampusReject, campus.AllowedActions);
        // EDIT right is a different question and correctly absent — they never filed this request.
        Assert.DoesNotContain(VisitFormActions.EditPendingCampus, campus.AllowedActions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// The mirror image of the read-model rule above: a Staff Leader of a DIFFERENT campus gets
    /// neither ordinary action for a campus they do not lead, and neither does the registrant who is
    /// not that campus's leader — deciding stays with whoever actually leads the campus.
    /// </summary>
    [Fact]
    public async Task OrdinaryCampusDecision_is_withheld_from_a_different_campus_leader_and_from_a_non_leader_registrant()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        // Campus2's leader is not scoped to this request at all — this campus is the only one on it,
        // and it is not theirs — so the read model refuses the whole request rather than a bare empty
        // list, exactly as it does for any other stranger.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Resolver(db, StaffLeader(SlCampus2, Campus2)).ResolveAsync(req.VisitRequestId, CancellationToken.None));

        var ownerView = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var campus = Assert.Single(ownerView.CampusVisits);
        // The registrant edits Campus1 (they filed it) but does not lead it, so neither decision
        // action is offered — deciding is the campus leader's alone, filed-by-them or not.
        Assert.DoesNotContain(VisitListActions.ApproveAndAssignHost, campus.AllowedActions);
        Assert.DoesNotContain(VisitListActions.CampusReject, campus.AllowedActions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Once the campus is decided (ASSIGNED) the ordinary decision is no longer offered — the same
    /// leader keeps other actions (host handover) but not a second approve/reject on a campus that is
    /// no longer WAITING_REQUEST_APPROVAL.
    /// </summary>
    [Fact]
    public async Task OrdinaryCampusDecision_is_withheld_once_the_campus_is_already_decided()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: SlCampus1);

        var leaderView = await Resolver(db, StaffLeader(SlCampus1, Campus1))
            .ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var campus = Assert.Single(leaderView.CampusVisits);
        Assert.Equal(VisitInstanceStatuses.Assigned, campus.InstanceStatus);
        Assert.DoesNotContain(VisitListActions.ApproveAndAssignHost, campus.AllowedActions);
        Assert.DoesNotContain(VisitListActions.CampusReject, campus.AllowedActions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// The request-wide verdict goes ONLY to a caller who can see the whole request.
    ///
    /// This is the shape that produced the contradiction on screen: campus 1 REJECTED, campus 2 still
    /// WAITING. The Staff Leader of campus 1 receives exactly one campus, and every campus they can
    /// see is rejected — so anything counting their own payload concludes the request is dead. The
    /// backend must not hand them a request-level verdict at all, and must give the registrant one
    /// that counts BOTH campuses.
    /// </summary>
    [Fact]
    public async Task RequestOutcome_is_withheld_from_a_scoped_caller_and_counts_every_campus_for_the_registrant()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true);
        var first = instances.Single(i => i.CampusId == Campus1);
        first.Status = VisitInstanceStatuses.Rejected;
        first.DecidedBy = SlCampus1;
        first.DecidedAt = DateTime.Now;
        first.DecisionActorRole = "STAFF_LEADER";
        first.DecisionNote = "Trùng lịch sự kiện cấp campus.";
        instances.Single(i => i.CampusId == Campus2).Status = VisitInstanceStatuses.WaitingRequestApproval;
        await db.SaveChangesAsync();

        var scoped = await Resolver(db, StaffLeader(SlCampus1, Campus1)).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var onlyCampus = Assert.Single(scoped.CampusVisits);
        Assert.Equal(VisitInstanceStatuses.Rejected, onlyCampus.InstanceStatus);
        Assert.False(scoped.Viewer.CanViewAllCampuses);
        // Everything they can see is rejected — and they are still told nothing about the request.
        Assert.Null(scoped.RequestOutcome);

        var full = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.True(full.Viewer.CanViewAllCampuses);
        Assert.NotNull(full.RequestOutcome);
        var outcome = full.RequestOutcome!;
        Assert.Equal(2, outcome.Total);
        Assert.Equal(1, outcome.Rejected);
        Assert.Equal(1, outcome.Waiting);
        Assert.Equal("MIXED", outcome.Code);   // NOT ALL_REJECTED
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Ho_sees_all_read_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Resolver(db, Ho()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.Equal(2, dto.CampusVisits.Count);
        Assert.True(dto.Viewer.CanViewAllCampuses);
        Assert.True(dto.Viewer.IsReadOnly);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Host_sees_only_hosted_instance()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // campus1 hosted by IcStaffC1, campus2 not.
        var (req, instances) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true, host0: IcStaffC1);
        var dto = await Resolver(db, Host(IcStaffC1, Campus1)).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var c = Assert.Single(dto.CampusVisits);
        Assert.Equal(instances[0].VisitInstanceId, (ulong)c.VisitInstanceId);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Admin_and_unrelated_are_forbidden()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => Resolver(db, Admin()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => Resolver(db, Unrelated()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        await tx.RollbackAsync();
    }

    // ── 4b. Cancellation outcome metadata (UC-136) ───────────────────────────

    [Fact]
    public async Task Request_cancellation_metadata_is_mapped_with_the_actor_name()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        var cancelledAt = DateTime.Now.AddDays(-1);
        req.Status = "CANCELLED";
        req.CancelledBy = VisitorOwner;
        req.CancelledAt = cancelledAt;
        req.CancellationReason = "Thay đổi lịch công tác của đoàn.";
        await db.SaveChangesAsync();

        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.Equal(VisitorOwner, dto.CancelledByUserId);
        Assert.Equal(cancelledAt, dto.CancelledAt!.Value, TimeSpan.FromSeconds(1));
        Assert.Equal("Thay đổi lịch công tác của đoàn.", dto.CancellationReason);
        // The name is resolved server-side: the screen must not have to look a user id up itself.
        Assert.False(string.IsNullOrWhiteSpace(dto.CancelledByName));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Campus_cancellation_metadata_is_mapped_per_instance()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // A campus must be DECIDED before it can be cancelled on its own: a DB trigger enforces that a
        // still-pending instance is only cancelled as part of cancelling the whole request (UC-136).
        var (req, instances) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true, host0: IcStaffC1);
        var cancelledAt = DateTime.Now.AddDays(-2);
        instances[0].Status = "CANCELLED";
        // A HOST cancellation must be recorded against the instance's own official host — the schema
        // refuses to attribute it to anybody else.
        instances[0].CancelledBy = IcStaffC1;
        instances[0].CancelledAt = cancelledAt;
        // The schema allows only VISITOR|HOST and SELF_SERVICE|EXTERNAL_CONFIRMATION here.
        instances[0].CancellationActorType = "HOST";
        instances[0].CancellationSource = "EXTERNAL_CONFIRMATION";
        instances[0].CancellationReason = "Cơ sở bận sự kiện khác.";
        await db.SaveChangesAsync();

        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var cancelled = dto.CampusVisits.Single(c => c.VisitInstanceId == (long)instances[0].VisitInstanceId);
        Assert.Equal(IcStaffC1, cancelled.CancelledByUserId);
        Assert.Equal("HOST", cancelled.CancellationActorType);
        Assert.Equal("EXTERNAL_CONFIRMATION", cancelled.CancellationSource);
        Assert.Equal("Cơ sở bận sự kiện khác.", cancelled.CancellationReason);
        Assert.False(string.IsNullOrWhiteSpace(cancelled.CancelledByName));

        // The sibling was never cancelled and must not inherit any of it.
        var other = dto.CampusVisits.Single(c => c.VisitInstanceId == (long)instances[1].VisitInstanceId);
        Assert.Null(other.CancelledAt);
        Assert.Null(other.CancellationReason);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Hidden_campus_cancellation_never_reaches_a_scoped_leader()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Campus 1 is decided (so it CAN be cancelled alone) and then cancelled; campus 2 is untouched.
        var (req, instances) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true, host0: IcStaffC1);
        instances[0].Status = "CANCELLED";
        instances[0].CancelledBy = IcStaffC1;   // the instance's own host, per the schema guard
        instances[0].CancelledAt = DateTime.Now.AddDays(-1);
        instances[0].CancellationActorType = "HOST";
        instances[0].CancellationSource = "EXTERNAL_CONFIRMATION";
        instances[0].CancellationReason = "LÝ DO CỦA CƠ SỞ KHÁC";
        await db.SaveChangesAsync();

        // A Staff Leader scoped to campus 2 must not learn that campus 1 was cancelled, nor why.
        var dto = await Resolver(db, StaffLeader(SlCampus2, Campus2)).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var only = Assert.Single(dto.CampusVisits);
        Assert.Equal((long)Campus2, only.CampusId);
        Assert.Null(only.CancellationReason);
        Assert.DoesNotContain(dto.CampusVisits, c => c.CancellationReason == "LÝ DO CỦA CƠ SỞ KHÁC");
        await tx.RollbackAsync();
    }

    // ── 4b. Primary-contact identity actions ─────────────────────────────────
    // The frontend used to decide these from `viewer.relation` alone, so it offered buttons the backend
    // would refuse: a resend past its cap, a transfer inside the 24h window, a second transfer while one
    // was already pending. Each test below is one of those refusals.

    private static readonly string[] ContactActionCodes =
    {
        VisitFormActions.ResendOperationalContactConfirmation, VisitFormActions.ReplaceOperationalContact,
        VisitFormActions.InitiateOperationalContactTransfer, VisitFormActions.ResendOperationalContactConfirmation,
        VisitFormActions.CancelOperationalContactChange,
    };

    /// <summary>Takes the campus back to "nobody has confirmed it", which is what re-shuts the gate.</summary>
    private static async Task MakeContactUnconfirmedAsync(ApplicationDbContext db, ulong visitInstanceId)
    {
        var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == visitInstanceId);
        instance.OperationalContactUserId = null;
        instance.OperationalContactConfirmedAt = null;
        instance.OperationalContactConfirmationSource = null;
        instance.Status = VisitInstanceStatuses.WaitingContactConfirmation;
        await db.SaveChangesAsync();
    }

    private static async Task SeedPendingIdentityChangeAsync(
        ApplicationDbContext db, ulong visitRequestId, ulong visitInstanceId,
        string kind, uint resendCount, DateTime expiresAt)
    {
        db.VisitRequestIdentityChanges.Add(new VisitRequestIdentityChange
        {
            VisitRequestId = visitRequestId,
            // An invitation belongs to ONE campus; the composite FK refuses it otherwise.
            VisitInstanceId = visitInstanceId,
            ChangeKind = kind,
            NewEmailMasked = "n***@e.com",
            // A TRANSFER always captures the owner it is taking the role from — the DB enforces it,
            // exactly as InitiateOperationalContactTransferCommandHandler does.
            OldUserId = kind == IdentityChangeKinds.Transfer ? VisitorOwner : null,
            Status = IdentityChangeStatuses.Pending,
            ExpectedRequestRowVersion = 0,
            RequestedBy = VisitorOwner,
            RequestedAt = DateTime.Now,
            ExpiresAt = expiresAt,
            ResendCount = resendCount,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The two ways a campus's contact can change are windowed by the campus DECISION, not by whether
    /// somebody holds the role: before a decision the registrant simply REPLACEs the contact, and once
    /// the campus has been decided the seat is handed over by TRANSFER, which the new holder has to
    /// accept. Both sides of that boundary are asserted here so it cannot drift.
    /// </summary>
    [Fact]
    public async Task Replace_is_offered_before_the_decision_and_transfer_after_it()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Seed default: contact confirmed, campus NOT yet decided, earliest start +20d, nothing pending.
        var (undecided, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        var beforeDecision = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(undecided.VisitRequestId, CancellationToken.None));

        Assert.Contains(VisitFormActions.ReplaceOperationalContact, beforeDecision);
        Assert.DoesNotContain(VisitFormActions.InitiateOperationalContactTransfer, beforeDecision);
        Assert.DoesNotContain(VisitFormActions.ResendOperationalContactConfirmation, beforeDecision);
        Assert.DoesNotContain(VisitFormActions.CancelOperationalContactChange, beforeDecision);

        // host0 drives the campus to ASSIGNED — decided, Host has not started preparation.
        var (decided, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        var afterDecision = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(decided.VisitRequestId, CancellationToken.None));

        Assert.Contains(VisitFormActions.InitiateOperationalContactTransfer, afterDecision);
        // Replace is over: the campus has a decision, so the seat can only be handed over.
        Assert.DoesNotContain(VisitFormActions.ReplaceOperationalContact, afterDecision);
        Assert.DoesNotContain(VisitFormActions.ResendOperationalContactConfirmation, afterDecision);
        Assert.DoesNotContain(VisitFormActions.CancelOperationalContactChange, afterDecision);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_unclaimed_contact_offers_replace_always_and_resend_only_below_the_cap()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        await MakeContactUnconfirmedAsync(db, instances[0].VisitInstanceId);
        await SeedPendingIdentityChangeAsync(
            db, req.VisitRequestId, instances[0].VisitInstanceId,
            IdentityChangeKinds.InitialConfirmation, resendCount: 4, DateTime.Now.AddHours(72));

        var actions = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        Assert.Contains(VisitFormActions.ReplaceOperationalContact, actions);
        Assert.Contains(VisitFormActions.ResendOperationalContactConfirmation, actions);
        // An unclaimed contact cannot be transferred — that is the claim workflow's job.
        Assert.DoesNotContain(VisitFormActions.InitiateOperationalContactTransfer, actions);

        // One more resend and the handler answers CLAIM_RESEND_LIMIT; the button must go before that.
        var claim = await db.VisitRequestIdentityChanges
            .SingleAsync(c => c.VisitRequestId == req.VisitRequestId);
        claim.ResendCount = 5;
        await db.SaveChangesAsync();

        var atCap = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        Assert.DoesNotContain(VisitFormActions.ResendOperationalContactConfirmation, atCap);
        Assert.Contains(VisitFormActions.ReplaceOperationalContact, atCap); // replace has no cap
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Seeded on a DECIDED campus, because that is the only place a transfer can exist: an undecided
    /// campus is replace territory, and its contact is changed outright rather than handed over. The
    /// seed used to leave the campus at WAITING_REQUEST_APPROVAL, which made the resend it asserted a
    /// call the handler would have refused — the read model now asks the same lifecycle question the
    /// resend handler does, so the fixture has to describe a state that can actually occur.
    /// </summary>
    [Fact]
    public async Task A_pending_transfer_replaces_initiation_with_resend_and_cancel()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        await SeedPendingIdentityChangeAsync(
            db, req.VisitRequestId, instances[0].VisitInstanceId,
            IdentityChangeKinds.Transfer, resendCount: 0, DateTime.Now.AddHours(24));

        var actions = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));

        Assert.Contains(VisitFormActions.ResendOperationalContactConfirmation, actions);
        Assert.Contains(VisitFormActions.CancelOperationalContactChange, actions);
        // A second transfer is refused by the one-pending-change guard, so it is never offered.
        Assert.DoesNotContain(VisitFormActions.InitiateOperationalContactTransfer, actions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// A handover stays on offer minutes before the visit begins, because the CAMPUS has not begun.
    ///
    /// <para>
    /// This used to be the opposite assertion: a 24-hour lead time before <c>PlannedStartAt</c> took
    /// the button away, and the registrant whose contact fell ill the night before was told to
    /// telephone FPTU. The rule is now the persisted status alone, in the read model exactly as in
    /// <c>OperationalContactGuards.EnsureTransferWindowOpen</c> — which is the point: a countdown here
    /// and a status test there is how a screen comes to offer what the API refuses.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_handover_is_still_offered_minutes_before_a_start_the_campus_has_not_reached()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // host0 → the campus is ASSIGNED: decided, and its Host has not opened preparation.
        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instances[0].VisitInstanceId);
        instance.PlannedStartAt = DateTime.Now.AddMinutes(1);
        instance.PlannedEndAt = DateTime.Now.AddHours(2);
        await db.SaveChangesAsync();

        var imminent = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        Assert.Contains(VisitFormActions.InitiateOperationalContactTransfer, imminent);
        Assert.Contains(VisitFormActions.UpdateOperationalContactProfile, imminent);

        await SeedPendingIdentityChangeAsync(
            db, req.VisitRequestId, instances[0].VisitInstanceId,
            IdentityChangeKinds.Transfer, resendCount: 0, DateTime.Now.AddHours(24));
        var withTransfer = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));

        // A second transfer is refused by the one-pending-change guard, so it is never offered; the
        // invitation in flight can still be chased or closed.
        Assert.DoesNotContain(VisitFormActions.InitiateOperationalContactTransfer, withTransfer);
        Assert.Contains(VisitFormActions.ResendOperationalContactConfirmation, withTransfer);
        Assert.Contains(VisitFormActions.CancelOperationalContactChange, withTransfer);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// From the moment the campus starts, the screen offers no way to change its contact — the same
    /// four statuses the guards whitelist, and no others. Read-only is not enough here: every one of
    /// these codes would come back 409 from its handler, and a button that fails is worse than an
    /// absent one because the user cannot tell whether the system or their request is at fault.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    public async Task A_started_campus_offers_no_contact_mutation(string status)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        await StartCampusAsync(db, instances[0].VisitInstanceId, status);

        var actions = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));

        Assert.DoesNotContain(VisitFormActions.UpdateOperationalContactProfile, actions);
        Assert.DoesNotContain(VisitFormActions.ReplaceOperationalContact, actions);
        Assert.DoesNotContain(VisitFormActions.InitiateOperationalContactTransfer, actions);
        Assert.DoesNotContain(VisitFormActions.ReinviteOperationalContactConfirmation, actions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// The asymmetry, from the read model's side. A handover left in flight when the campus started
    /// can no longer be RESENT — resending renews its expiry and mints a fresh link, which is how a
    /// transfer that can never be applied would be kept alive — but it can still be CANCELLED, because
    /// closing it changes nothing about who runs the campus. Withholding cancel would leave the campus
    /// permanently occupied by a PENDING change, and only one is allowed at a time.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    public async Task A_stale_handover_on_a_started_campus_offers_cancel_but_not_resend(string status)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        // Proposed while the campus was still ASSIGNED, and still well inside its 24-hour validity.
        await SeedPendingIdentityChangeAsync(
            db, req.VisitRequestId, instances[0].VisitInstanceId,
            IdentityChangeKinds.Transfer, resendCount: 0, DateTime.Now.AddHours(24));
        await StartCampusAsync(db, instances[0].VisitInstanceId, status);

        var actions = CampusActions(
            await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));

        Assert.Contains(VisitFormActions.CancelOperationalContactChange, actions);
        Assert.DoesNotContain(VisitFormActions.ResendOperationalContactConfirmation, actions);
        Assert.DoesNotContain(VisitFormActions.InitiateOperationalContactTransfer, actions);
        Assert.DoesNotContain(VisitFormActions.UpdateOperationalContactProfile, actions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Drives an ASSIGNED campus into <paramref name="status"/> the way the database insists on:
    /// BEFORE_VISIT is the only door into DURING_VISIT, and a campus at DURING_VISIT or beyond must
    /// have at least one agenda item. Both are enforced by <c>trg_visit_campuses_*_bu</c>, so they are
    /// satisfied rather than worked around.
    /// </summary>
    private static async Task StartCampusAsync(
        ApplicationDbContext db, ulong visitInstanceId, string status)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_agendas (visit_instance_id, sequence_order, title, start_time, created_at) " +
            "VALUES ({0}, 1, 'Phiên làm việc', {1}, {1})",
            visitInstanceId, DateTime.Now);

        foreach (var step in new[] { VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, status }
                     .Distinct())
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_request_campuses SET status = {1} WHERE visit_instance_id = {0}",
                visitInstanceId, step);
    }

    [Fact]
    public async Task No_contact_action_reaches_ho_a_campus_leader_or_a_host()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);

        foreach (var viewer in new ICurrentUserService[]
                 { Ho(), StaffLeader(SlCampus1, Campus1), Host(IcStaffC1, Campus1) })
        {
            var actions = (await Resolver(db, viewer).ResolveAsync(req.VisitRequestId, CancellationToken.None))
                .Viewer.AllowedActions;
            Assert.All(ContactActionCodes, code => Assert.DoesNotContain(code, actions));
        }
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_cancelled_request_offers_no_contact_action()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        var tracked = await db.VisitRequests.SingleAsync(v => v.VisitRequestId == req.VisitRequestId);
        tracked.Status = VisitRequestStatuses.Cancelled;
        tracked.CancelledBy = VisitorOwner;
        tracked.CancelledAt = DateTime.Now;
        tracked.CancellationReason = "Đoàn hủy chuyến"; // the DB requires a reason with a cancellation
        await db.SaveChangesAsync();

        var actions = (await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None))
            .Viewer.AllowedActions;
        Assert.All(ContactActionCodes, code => Assert.DoesNotContain(code, actions));
        await tx.RollbackAsync();
    }

    // ── 5. allowedActions (mirror the command-handler authorization) ─────────

    [Fact]
    public async Task Owner_pending_request_offers_edit_and_safe_edit_not_amendment()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false); // instance WAITING_REQUEST_APPROVAL
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.Contains(VisitFormActions.EditPendingRequest, dto.Viewer.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.ResubmitRejectedRequest, dto.Viewer.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.SubmitAmendment, dto.CampusVisits.Single().AllowedActions);

        // Safe edit is NOT offered on a still-pending request. A pending request can be edited in
        // full, so offering the narrow tool alongside only made it unclear which one to reach for —
        // and the safe-edit handler now refuses a WAITING campus outright, so offering it here would
        // be a button that fails.
        Assert.DoesNotContain(VisitFormActions.SubmitSafeEdit, dto.Viewer.AllowedActions);

        // The refused capability is still REPORTED, with the reason — that is what lets the screen
        // explain itself rather than silently dropping the action.
        var safeEdit = dto.Viewer.Capabilities.Single(c => c.Code == VisitFormActions.SubmitSafeEdit);
        Assert.False(safeEdit.Enabled);
        Assert.Equal(VisitMutationErrorCodes.LifecycleNotAllowed, safeEdit.DisabledReasonCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Owner_assigned_instance_offers_submit_amendment()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1); // ASSIGNED, +20d
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var campus = dto.CampusVisits.Single();
        Assert.Contains(VisitFormActions.SubmitAmendment, campus.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.WithdrawAmendment, campus.AllowedActions); // no pending amendment
        Assert.Contains(VisitFormActions.SubmitSafeEdit, dto.Viewer.AllowedActions);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// A pending proposal splits three ways: the owner may withdraw it, the campus's current HOST decides
    /// it, and the campus's Staff Leader does not.
    ///
    /// <para>
    /// This test previously asserted the opposite of its last two lines — the leader decided and the host
    /// was never asked. Authority moved to the Host with the post-approval amendment rules (§9/§10/§15):
    /// after approval the Host holds the campus, and routing every adjustment back through a leader who
    /// handed it over days earlier is what the change removed. The name says "host to decide" now because
    /// that is the rule; the owner half is unchanged.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Pending_amendment_moves_owner_to_withdraw_and_host_to_decide()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        await SeedPendingAmendmentAsync(db, req.VisitRequestId, instances[0].VisitInstanceId);

        var ownerCampus = (await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None))
            .CampusVisits.Single();
        Assert.Contains(VisitFormActions.WithdrawAmendment, ownerCampus.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.SubmitAmendment, ownerCampus.AllowedActions);  // one pending only
        Assert.DoesNotContain(VisitFormActions.ApproveAmendment, ownerCampus.AllowedActions); // owner never decides

        // The campus's current Host decides it — approve and reject travel together.
        var hostCampus = (await Resolver(db, Host(IcStaffC1, Campus1)).ResolveAsync(req.VisitRequestId, CancellationToken.None))
            .CampusVisits.Single();
        Assert.Contains(VisitFormActions.ApproveAmendment, hostCampus.AllowedActions);
        Assert.Contains(VisitFormActions.RejectAmendment, hostCampus.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.WithdrawAmendment, hostCampus.AllowedActions);

        // And the campus's Staff Leader does NOT, even though they approved the campus in the first
        // place. There is no fallback: if the Host is the wrong person the leader transfers the role.
        var leaderCampus = (await Resolver(db, StaffLeader(SlCampus1, Campus1)).ResolveAsync(req.VisitRequestId, CancellationToken.None))
            .CampusVisits.Single();
        Assert.DoesNotContain(VisitFormActions.ApproveAmendment, leaderCampus.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.RejectAmendment, leaderCampus.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.WithdrawAmendment, leaderCampus.AllowedActions);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Leader_of_other_campus_cannot_decide_sibling_amendment()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true, host0: IcStaffC1);
        await SeedPendingAmendmentAsync(db, req.VisitRequestId, instances[0].VisitInstanceId); // campus1

        // Leader of campus2 sees ONLY campus2 (campus1 hidden) → can never approve campus1's amendment.
        var dtoB = await Resolver(db, StaffLeader(SlCampus2, Campus2)).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var only = Assert.Single(dtoB.CampusVisits);
        Assert.Equal((long)Campus2, only.CampusId);
        Assert.DoesNotContain(VisitFormActions.ApproveAmendment, only.AllowedActions);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Ho_gets_only_view_and_no_instance_actions()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        await SeedPendingAmendmentAsync(db, req.VisitRequestId, instances[0].VisitInstanceId);

        var dto = await Resolver(db, Ho()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.True(dto.Viewer.IsReadOnly);
        // READ codes only, and exactly these two — HO monitors the whole request, change history
        // included, and may act on none of it. Still an exact comparison, so a mutation action
        // leaking into HO's list fails here as loudly as it did before VIEW_CHANGE_HISTORY existed.
        Assert.Equal(
            new[] { VisitFormActions.View, VisitFormActions.ViewChangeHistory },
            dto.Viewer.AllowedActions);
        Assert.Empty(dto.CampusVisits.Single().AllowedActions);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Rejected_request_offers_resubmit_not_edit()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        var entity = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == req.VisitRequestId);
        entity.Status = "REJECTED";
        // The CAMPUS has to be rejected too, with the decision metadata the DB trigger requires. A
        // request whose status says REJECTED while its only campus still says WAITING cannot exist —
        // the aggregate trigger derives one from the other — and resubmit is now correctly refused
        // for such a row, so seeding it that way was testing an impossible state.
        var instance = await db.VisitRequestCampuses.FirstAsync(c => c.VisitRequestId == req.VisitRequestId);
        instance.Status = VisitInstanceStatuses.Rejected;
        instance.DecidedBy = SlCampus1;
        instance.DecidedAt = DateTime.Now;
        instance.DecisionActorRole = "STAFF_LEADER";
        instance.DecisionNote = "Không đủ điều kiện tiếp đón.";
        await db.SaveChangesAsync();

        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.Contains(VisitFormActions.ResubmitRejectedRequest, dto.Viewer.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.EditPendingRequest, dto.Viewer.AllowedActions);
        // Nor the narrow tool: a rejected request is re-sent, not patched.
        Assert.DoesNotContain(VisitFormActions.SubmitSafeEdit, dto.Viewer.AllowedActions);
        await tx.RollbackAsync();
    }

    private static async Task SeedPendingAmendmentAsync(ApplicationDbContext db, ulong requestId, ulong instanceId)
    {
        db.VisitInstanceAmendments.Add(new VisitInstanceAmendment
        {
            VisitRequestId = requestId,
            VisitInstanceId = instanceId,
            AmendmentNo = 1,
            Status = AmendmentStatuses.PendingApproval,
            BaseFormRevision = 1,
            BaseApprovalRevision = 1,
            RequestedBy = VisitorOwner,
            RequestedAt = DateTime.Now,
            ExpectedInstanceRowVersion = 0,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static async Task<(VisitRequest, List<VisitRequestCampus>)> SeedV1Async(
        ApplicationDbContext db, ulong[] campusIds)
    {
        var req = NewRequest(FormSchemaVersions.Legacy, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS");
        foreach (var cid in campusIds) req.CampusInstances.Add(NewInstance(cid));
        req.GuestMembers.Add(NewMember(0, "G1"));
        req.GuestMembers.Add(NewMember(0, "S1", "EXTERNAL_SUPPORT"));
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();
        return (req, req.CampusInstances.OrderBy(c => c.CampusId).ToList());
    }

    private static async Task<(VisitRequest, List<VisitRequestCampus>)> SeedV2Async(
        ApplicationDbContext db, ulong[] campusIds, bool mixed, ulong? host0 = null)
    {
        var req = NewRequest(FormSchemaVersions.PerCampus, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS", mixed);
        var tags = new[] { "A", "B", "C", "D", "E" };
        for (var i = 0; i < campusIds.Length; i++)
        {
            var inst = NewInstance(campusIds[i], i == 0 ? host0 : null);
            inst.FormDetail = NewDetail(tags[i]);
            req.CampusInstances.Add(inst);
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();

        // Per-campus members + links (one distinct member per campus so cross-leak is detectable).
        for (var i = 0; i < ordered.Count; i++)
        {
            var member = NewMember(req.VisitRequestId, $"{tags[i]}-guest");
            db.VisitGuestMembers.Add(member);
            await db.SaveChangesAsync();
            db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
            {
                VisitRequestId = req.VisitRequestId,
                VisitInstanceId = ordered[i].VisitInstanceId,
                GuestMemberId = member.GuestMemberId,
                DisplayOrder = 0, CreatedAt = DateTime.Now,
            });
        }
        await db.SaveChangesAsync();
        return (req, ordered);
    }
}

/// <summary>Mirror of the string relation constants used by the resolver, so tests need no
/// dependency on the internal VisitInstanceAccess type.</summary>
internal static class VisitInstanceAccessRelations
{
    public const string StaffLeader = "STAFF_LEADER";
    public const string Ho = "HO";
    public const string VisitorOwner = "VISITOR_OWNER";
    public const string Host = "HOST";
}
