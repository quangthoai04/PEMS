using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Feedbacks.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The instance authorization matrix (plan v11 §8–§12), proven rather than audited.
///
/// <para>
/// One request, two campuses, four people:
/// </para>
/// <code>
/// Request R
///   HN → operational contact A
///   DN → operational contact B
///   registrant R-owner
///   random VISITOR C
/// </code>
/// <para>
/// Every right confirmed for an operational contact routes through one of three shared guards —
/// <see cref="VisitRequestOwnership"/>, <see cref="AmendmentGuards"/> and
/// <see cref="VisitInstanceAccess"/> — and every one of them takes the CAMPUS, not the request. These
/// tests drive those guards against real rows, because that is where the decision is actually made:
/// a handler that called the wrong one would pass a UI test and still hand campus B to campus A's
/// contact.
/// </para>
/// <para>
/// The property under test throughout is the same: authority comes from
/// <c>visit_request_campuses.operational_contact_user_id</c>. Holding one campus grants nothing on its
/// sibling, and holding a VISITOR account grants nothing at all.
/// </para>
/// </summary>
public sealed class InstanceAuthorizationMatrixTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

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
        public FakeUser(ulong id) => UserId = id;
        public ulong? UserId { get; }
        public string? Email => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? RoleId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
        public bool IsAuthenticated => true;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong r, string t, string? m, string nt, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    // ── Fixture ───────────────────────────────────────────────────────────────────

    private static CampusVisitFormDto Campus(string code)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn " + code, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + code, "OrgB", "Trưởng phòng", "+84912345678",
                V2SeedActor.Email(Registrant)),
            "EN", null, "DECLINED", null, null);
    }

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new PEMS.Application.Delegations.Services.VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "AM" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    /// <summary>Two ACTIVE visitors who are neither the registrant nor each other.</summary>
    private static async Task<(ulong A, ulong B, ulong C)> ThreeVisitorsAsync()
    {
        using var db = NewContext();
        var ids = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant)
            .OrderBy(u => u.UserId).Select(u => u.UserId).Take(3).ToListAsync();
        Assert.True(ids.Count >= 3, "seed needs at least three non-registrant ACTIVE visitors");
        return (ids[0], ids[1], ids[2]);
    }

    private static async Task BindContactAsync(ulong instanceId, ulong userId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET operational_contact_user_id = {1}, " +
            "operational_contact_confirmed_at = {2}, operational_contact_confirmation_source = 'EMAIL_CONFIRMATION', " +
            "status = 'WAITING_REQUEST_APPROVAL' WHERE visit_instance_id = {0}", instanceId, userId, Now);
    }

    /// <summary>Loads the request with its campuses, tracked the way a handler would.</summary>
    private static async Task<(VisitRequest Visit, VisitRequestCampus Hn, VisitRequestCampus Dn)>
        LoadAsync(ApplicationDbContext db, ulong requestId)
    {
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .SingleAsync(v => v.VisitRequestId == requestId);

        var codes = await db.Campuses.AsNoTracking()
            .ToDictionaryAsync(c => c.CampusId, c => c.CampusCode);

        var hn = visit.CampusInstances.Single(c => codes[c.CampusId] == "HN");
        var dn = visit.CampusInstances.Single(c => codes[c.CampusId] == "DN");
        return (visit, hn, dn);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM email_action_tokens WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── The matrix ────────────────────────────────────────────────────────────────

    /// <summary>
    /// §8. VIEW and the guest-side relation. A holds HN, B holds DN, C holds nothing.
    ///
    /// <para>
    /// The registrant is guest-side of BOTH campuses — they own the request — while each contact is
    /// guest-side of exactly one. That asymmetry is the whole model: request-level ownership and
    /// campus-level operation are different things.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Guest_side_is_per_campus_for_contacts_and_request_wide_for_the_registrant()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var (a, b, c) = await ThreeVisitorsAsync();
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));

            using var db = NewContext();
            var (visit0, hn0, dn0) = await LoadAsync(db, requestId);
            await BindContactAsync(hn0.VisitInstanceId, a);
            await BindContactAsync(dn0.VisitInstanceId, b);

            using var fresh = NewContext();
            var (visit, hn, dn) = await LoadAsync(fresh, requestId);

            // A: HN yes, DN no.
            Assert.True(VisitRequestOwnership.IsGuestSide(visit, hn, a));
            Assert.False(VisitRequestOwnership.IsGuestSide(visit, dn, a));
            Assert.True(VisitRequestOwnership.IsOperationalContact(hn, a));
            Assert.False(VisitRequestOwnership.IsOperationalContact(dn, a));

            // B: the mirror image.
            Assert.True(VisitRequestOwnership.IsGuestSide(visit, dn, b));
            Assert.False(VisitRequestOwnership.IsGuestSide(visit, hn, b));

            // C: an ACTIVE VISITOR account and nothing else. The role grants nothing.
            Assert.False(VisitRequestOwnership.IsGuestSide(visit, hn, c));
            Assert.False(VisitRequestOwnership.IsGuestSide(visit, dn, c));
            Assert.False(VisitRequestOwnership.IsRequesterSide(visit, c));

            // The registrant owns the request, so both campuses.
            Assert.True(VisitRequestOwnership.IsGuestSide(visit, hn, Registrant));
            Assert.True(VisitRequestOwnership.IsGuestSide(visit, dn, Registrant));
            // …but owns neither campus floor: OperatedCampuses is for contacts only.
            Assert.Empty(VisitRequestOwnership.OperatedCampuses(visit, Registrant));
            Assert.Equal(new[] { hn.VisitInstanceId },
                VisitRequestOwnership.OperatedCampuses(visit, a).Select(x => x.VisitInstanceId).ToArray());
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §9 AMEND-CONTACT-01/02/03/06. The amendment guard takes the CAMPUS, so A may propose on HN and
    /// is refused on DN — the exact leak the request-level contact model used to allow.
    /// </summary>
    [Fact]
    public async Task Amendment_authorization_is_scoped_to_the_campus_the_contact_holds()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var (a, b, c) = await ThreeVisitorsAsync();
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));

            using (var seed = NewContext())
            {
                var (_, hn0, dn0) = await LoadAsync(seed, requestId);
                await BindContactAsync(hn0.VisitInstanceId, a);
                await BindContactAsync(dn0.VisitInstanceId, b);
            }

            using var db = NewContext();
            var (visit, hn, dn) = await LoadAsync(db, requestId);

            // A on HN: allowed (no throw).
            AmendmentGuards.EnsureRequesterSide(visit, hn, a);

            // A on DN: refused. Holding HN is not authority over its sibling.
            Assert.Throws<ForbiddenException>(() => AmendmentGuards.EnsureRequesterSide(visit, dn, a));

            // B is the mirror image.
            AmendmentGuards.EnsureRequesterSide(visit, dn, b);
            Assert.Throws<ForbiddenException>(() => AmendmentGuards.EnsureRequesterSide(visit, hn, b));

            // A random VISITOR is refused on both.
            Assert.Throws<ForbiddenException>(() => AmendmentGuards.EnsureRequesterSide(visit, hn, c));
            Assert.Throws<ForbiddenException>(() => AmendmentGuards.EnsureRequesterSide(visit, dn, c));

            // The registrant may propose on either.
            AmendmentGuards.EnsureRequesterSide(visit, hn, Registrant);
            AmendmentGuards.EnsureRequesterSide(visit, dn, Registrant);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §10 FEEDBACK-CONTACT-01/02/03/04. Feedback resolves the submitter's role from the CAMPUS, so a
    /// contact is a VISITOR-side submitter on their own campus and nothing at all on its sibling.
    /// </summary>
    [Fact]
    public async Task Feedback_eligibility_is_scoped_to_the_campus_the_contact_holds()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var (a, b, c) = await ThreeVisitorsAsync();
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));

            using (var seed = NewContext())
            {
                var (_, hn0, dn0) = await LoadAsync(seed, requestId);
                await BindContactAsync(hn0.VisitInstanceId, a);
                await BindContactAsync(dn0.VisitInstanceId, b);
            }

            using var db = NewContext();
            var (visit, hn, dn) = await LoadAsync(db, requestId);

            Assert.Equal(FeedbackSubmitterRoles.Visitor, FeedbackEligibility.ActorTypeOf(a, visit, hn));
            Assert.Null(FeedbackEligibility.ActorTypeOf(a, visit, dn));

            Assert.Equal(FeedbackSubmitterRoles.Visitor, FeedbackEligibility.ActorTypeOf(b, visit, dn));
            Assert.Null(FeedbackEligibility.ActorTypeOf(b, visit, hn));

            // No relation, no submitter role — on either campus.
            Assert.Null(FeedbackEligibility.ActorTypeOf(c, visit, hn));
            Assert.Null(FeedbackEligibility.ActorTypeOf(c, visit, dn));

            // The registrant may give feedback on both campuses of their own request.
            Assert.Equal(FeedbackSubmitterRoles.Visitor, FeedbackEligibility.ActorTypeOf(Registrant, visit, hn));
            Assert.Equal(FeedbackSubmitterRoles.Visitor, FeedbackEligibility.ActorTypeOf(Registrant, visit, dn));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §11 FILE-CONTACT-01..06. File access resolves through the OWNING campus, so knowing an id is
    /// never enough: the relation is computed for the instance the file belongs to, and a contact who
    /// holds a different campus resolves to NONE there.
    /// </summary>
    [Fact]
    public async Task Instance_relation_governs_file_access_and_does_not_travel_between_campuses()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var (a, b, c) = await ThreeVisitorsAsync();
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));

            using (var seed = NewContext())
            {
                var (_, hn0, dn0) = await LoadAsync(seed, requestId);
                await BindContactAsync(hn0.VisitInstanceId, a);
                await BindContactAsync(dn0.VisitInstanceId, b);
            }

            using var db = NewContext();
            var (visit, hn, dn) = await LoadAsync(db, requestId);

            // A, asked about HN's own instance → OPERATIONAL_CONTACT. Asked about DN → NONE.
            Assert.Equal(VisitInstanceAccess.OperationalContact,
                await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(a), hn, visit, CancellationToken.None));
            Assert.Equal(VisitInstanceAccess.None,
                await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(a), dn, visit, CancellationToken.None));

            Assert.Equal(VisitInstanceAccess.OperationalContact,
                await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(b), dn, visit, CancellationToken.None));
            Assert.Equal(VisitInstanceAccess.None,
                await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(b), hn, visit, CancellationToken.None));

            // Holding a VISITOR account resolves to NONE on both. This is the "guessed fileId" case:
            // whatever id C names, the relation computed for its owning campus is NONE.
            Assert.Equal(VisitInstanceAccess.None,
                await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(c), hn, visit, CancellationToken.None));
            Assert.Equal(VisitInstanceAccess.None,
                await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(c), dn, visit, CancellationToken.None));

            // The registrant is REGISTRANT on both — a request-level relation, not a campus one.
            Assert.Equal(VisitInstanceAccess.Registrant,
                await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(Registrant), hn, visit, CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §12 TRANSFER-AUTH-01/02/03 and the resend/cancel authorization that shares the guard. The
    /// current holder of a campus may hand it on; the holder of its sibling may not, and neither may
    /// an unrelated account.
    /// </summary>
    [Fact]
    public async Task Only_the_registrant_or_the_campus_holder_may_manage_that_campus_contact()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var (a, b, c) = await ThreeVisitorsAsync();
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));

            using (var seed = NewContext())
            {
                var (_, hn0, dn0) = await LoadAsync(seed, requestId);
                await BindContactAsync(hn0.VisitInstanceId, a);
                await BindContactAsync(dn0.VisitInstanceId, b);
            }

            using var db = NewContext();
            var (visit, hn, dn) = await LoadAsync(db, requestId);

            // Transfer / resend / cancel all pass allowCurrentContact: true.
            OperationalContactGuards.EnsureMayManageContact(visit, hn, a, allowCurrentContact: true);
            OperationalContactGuards.EnsureMayManageContact(visit, dn, b, allowCurrentContact: true);
            OperationalContactGuards.EnsureMayManageContact(visit, hn, Registrant, allowCurrentContact: true);

            // The sibling's holder is refused on this campus.
            Assert.Throws<ForbiddenException>(() =>
                OperationalContactGuards.EnsureMayManageContact(visit, hn, b, allowCurrentContact: true));
            Assert.Throws<ForbiddenException>(() =>
                OperationalContactGuards.EnsureMayManageContact(visit, dn, a, allowCurrentContact: true));

            // An unrelated VISITOR is refused everywhere.
            Assert.Throws<ForbiddenException>(() =>
                OperationalContactGuards.EnsureMayManageContact(visit, hn, c, allowCurrentContact: true));

            // REPLACE is registrant-only: even the current holder cannot swap themselves out that way,
            // because replacing a contact is not the same act as handing the role over.
            Assert.Throws<ForbiddenException>(() =>
                OperationalContactGuards.EnsureMayManageContact(visit, hn, a, allowCurrentContact: false));
            OperationalContactGuards.EnsureMayManageContact(visit, hn, Registrant, allowCurrentContact: false);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §12.5 TRANSFER-AUTH-10, and the handover behind AMEND-CONTACT-07 / FEEDBACK-CONTACT-05 /
    /// FILE-CONTACT-07. Rights follow the relation, so moving it moves ALL of them at once — which is
    /// the reason every one of these guards reads the same column instead of keeping its own list.
    /// </summary>
    [Fact]
    public async Task Rights_move_together_when_the_contact_relation_moves()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var (a, b, _) = await ThreeVisitorsAsync();
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));

            using (var seed = NewContext())
            {
                var (_, hn0, _) = await LoadAsync(seed, requestId);
                await BindContactAsync(hn0.VisitInstanceId, a);
            }

            // Before: A holds HN, B holds nothing there.
            using (var db = NewContext())
            {
                var (visit, hn, _) = await LoadAsync(db, requestId);
                AmendmentGuards.EnsureRequesterSide(visit, hn, a);
                Assert.Equal(FeedbackSubmitterRoles.Visitor, FeedbackEligibility.ActorTypeOf(a, visit, hn));
                Assert.Throws<ForbiddenException>(() => AmendmentGuards.EnsureRequesterSide(visit, hn, b));
                Assert.Null(FeedbackEligibility.ActorTypeOf(b, visit, hn));
            }

            // The handover: accepting a transfer writes exactly this column.
            using (var seed = NewContext())
            {
                var (_, hn0, _) = await LoadAsync(seed, requestId);
                await BindContactAsync(hn0.VisitInstanceId, b);
            }

            // After: every right has moved, and none was left behind with A.
            using (var db = NewContext())
            {
                var (visit, hn, _) = await LoadAsync(db, requestId);

                AmendmentGuards.EnsureRequesterSide(visit, hn, b);
                Assert.Equal(FeedbackSubmitterRoles.Visitor, FeedbackEligibility.ActorTypeOf(b, visit, hn));
                Assert.Equal(VisitInstanceAccess.OperationalContact,
                    await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(b), hn, visit, CancellationToken.None));
                OperationalContactGuards.EnsureMayManageContact(visit, hn, b, allowCurrentContact: true);

                Assert.Throws<ForbiddenException>(() => AmendmentGuards.EnsureRequesterSide(visit, hn, a));
                Assert.Null(FeedbackEligibility.ActorTypeOf(a, visit, hn));
                Assert.Equal(VisitInstanceAccess.None,
                    await VisitInstanceAccess.ResolveRelationAsync(db, new FakeUser(a), hn, visit, CancellationToken.None));
                Assert.Throws<ForbiddenException>(() =>
                    OperationalContactGuards.EnsureMayManageContact(visit, hn, a, allowCurrentContact: true));

                // The registrant's own rights are untouched by a handover between contacts.
                AmendmentGuards.EnsureRequesterSide(visit, hn, Registrant);
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
