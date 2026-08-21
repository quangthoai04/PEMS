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
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Application.Profiles.Commands.UpdateProfile;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Self-service profile sync (plan v10 §6) and the account lifecycle behind a contact invitation
/// (plan v10 §7, ACCOUNT-01..06).
///
/// <para>
/// The two belong together because both turn on one distinction: the per-instance CONTACT SNAPSHOT is
/// how a campus described a person for one visit, and the ACCOUNT is who that person is everywhere.
/// They may legitimately disagree. Nothing may quietly copy one onto the other — the account holder
/// alone may reconcile them, and only for the two fields the account schema actually owns.
/// </para>
/// </summary>
public sealed class ProfileSyncAndAccountLifecycleTests
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

    private static GetOperationalContactStateQueryHandler StateHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(), WriteOn);

    private static UpdateProfileCommandHandler ProfileHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock());

    // ── Fixtures ──────────────────────────────────────────────────────────────────

    private static CampusVisitFormDto Campus(string code, string contactEmail, string contactName, string phone)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn " + code, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto(contactName, "OrgB", "Trưởng phòng", phone, contactEmail),
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
            "PS" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task<ulong> InstanceAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .OrderBy(c => c.VisitInstanceId).Select(c => c.VisitInstanceId).FirstAsync();
    }

    /// <summary>
    /// Confirms a contact the way accepting does: the relation AND the status move together. The
    /// database refuses to hold a contact on a campus that is still WAITING_CONTACT_CONFIRMATION, which
    /// is the point — "confirmed" and "still waiting for confirmation" is not a state that exists.
    /// </summary>
    private static async Task BindContactAsync(ulong instanceId, ulong userId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET operational_contact_user_id = {1}, " +
            "operational_contact_confirmed_at = {2}, operational_contact_confirmation_source = 'EMAIL_CONFIRMATION', " +
            "status = 'WAITING_REQUEST_APPROVAL' WHERE visit_instance_id = {0}", instanceId, userId, Now);
    }

    private static async Task<(ulong UserId, string Email, string? FullName, string? Phone)> OtherVisitorAsync()
    {
        using var db = NewContext();
        var u = await db.Users.AsNoTracking()
            .Where(x => x.Role.RoleCode == RoleCodes.Visitor && x.Status == UserStatuses.Active
                        && x.UserId != Registrant)
            .OrderBy(x => x.UserId)
            .Select(x => new { x.UserId, x.Email, x.FullName, x.Phone })
            .FirstAsync();
        return (u.UserId, u.Email, u.FullName, u.Phone);
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

    /// <summary>Restores an account's profile so these tests leave the shared seed as they found it.</summary>
    private static async Task RestoreProfileAsync(ulong userId, string? fullName, string? phone)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE users SET full_name = {1}, phone = {2} WHERE user_id = {0}", userId, fullName, phone);
    }

    // ── PROFILE-SYNC-01/02/03: detection ──────────────────────────────────────────

    /// <summary>
    /// PROFILE-SYNC-01. A snapshot that says the same thing as the account offers nothing. Formatting is
    /// not a difference: the same number written with spaces must not raise a prompt whose "update"
    /// would change nothing.
    /// </summary>
    [Fact]
    public async Task No_prompt_when_the_snapshot_matches_the_account()
    {
        RequireDb();
        ulong requestId = 0;
        var contact = await OtherVisitorAsync();
        try
        {
            await RestoreProfileAsync(contact.UserId, "Nguyen Van A", "+84912345678");
            requestId = await CreateAsync(Campus("HN", contact.Email, "Nguyen Van A", "+84 912 345 678"));
            var instanceId = await InstanceAsync(requestId);
            await BindContactAsync(instanceId, contact.UserId);

            using var db = NewContext();
            var state = await StateHandler(db, contact.UserId).Handle(
                new GetOperationalContactStateQuery(requestId, instanceId), CancellationToken.None);

            Assert.Null(state.ProfileDifference);
        }
        finally
        {
            await CleanupAsync(requestId);
            await RestoreProfileAsync(contact.UserId, contact.FullName, contact.Phone);
        }
    }

    /// <summary>PROFILE-SYNC-02 and 03. A different name, or a genuinely different number, is offered.</summary>
    [Fact]
    public async Task A_differing_name_or_phone_is_offered_to_the_holder()
    {
        RequireDb();
        ulong requestId = 0;
        var contact = await OtherVisitorAsync();
        try
        {
            await RestoreProfileAsync(contact.UserId, "Nguyen Van A", "+84912345678");
            requestId = await CreateAsync(Campus("HN", contact.Email, "Nguyễn Văn A (Trưởng đoàn)", "+84900000111"));
            var instanceId = await InstanceAsync(requestId);
            await BindContactAsync(instanceId, contact.UserId);

            using var db = NewContext();
            var state = await StateHandler(db, contact.UserId).Handle(
                new GetOperationalContactStateQuery(requestId, instanceId), CancellationToken.None);

            var diff = Assert.IsType<OperationalContactProfileDifference>(state.ProfileDifference);
            Assert.True(diff.FullNameDiffers);
            Assert.True(diff.PhoneDiffers);
            Assert.Equal("Nguyen Van A", diff.AccountFullName);
            Assert.Equal("Nguyễn Văn A (Trưởng đoàn)", diff.SnapshotFullName);
        }
        finally
        {
            await CleanupAsync(requestId);
            await RestoreProfileAsync(contact.UserId, contact.FullName, contact.Phone);
        }
    }

    /// <summary>
    /// PROFILE-SYNC-08 and 09. The offer belongs to the account holder and to nobody else. The registrant
    /// who typed those very details, and any unrelated VISITOR, both see nothing to act on.
    /// </summary>
    [Fact]
    public async Task Nobody_but_the_holder_is_offered_the_reconciliation()
    {
        RequireDb();
        ulong requestId = 0;
        var contact = await OtherVisitorAsync();
        try
        {
            await RestoreProfileAsync(contact.UserId, "Nguyen Van A", "+84912345678");
            requestId = await CreateAsync(Campus("HN", contact.Email, "Tên Khác Hẳn", "+84900000222"));
            var instanceId = await InstanceAsync(requestId);
            await BindContactAsync(instanceId, contact.UserId);

            // The registrant sees the card, but is offered no reconciliation of somebody else's profile.
            using (var db = NewContext())
            {
                var asRegistrant = await StateHandler(db, Registrant).Handle(
                    new GetOperationalContactStateQuery(requestId, instanceId), CancellationToken.None);
                Assert.Null(asRegistrant.ProfileDifference);
            }

            // The holder is.
            using (var db = NewContext())
            {
                var asHolder = await StateHandler(db, contact.UserId).Handle(
                    new GetOperationalContactStateQuery(requestId, instanceId), CancellationToken.None);
                Assert.NotNull(asHolder.ProfileDifference);
            }
        }
        finally
        {
            await CleanupAsync(requestId);
            await RestoreProfileAsync(contact.UserId, contact.FullName, contact.Phone);
        }
    }

    // ── PROFILE-SYNC-04/05/06/07/10: the two actions ──────────────────────────────

    /// <summary>PROFILE-SYNC-04. Declining changes nothing at all — no account write, no snapshot write.</summary>
    [Fact]
    public async Task Keeping_the_profile_changes_neither_account_nor_snapshot()
    {
        RequireDb();
        ulong requestId = 0;
        var contact = await OtherVisitorAsync();
        try
        {
            await RestoreProfileAsync(contact.UserId, "Nguyen Van A", "+84912345678");
            requestId = await CreateAsync(Campus("HN", contact.Email, "Tên Trong Đơn", "+84900000333"));
            var instanceId = await InstanceAsync(requestId);
            await BindContactAsync(instanceId, contact.UserId);

            // "Giữ nguyên hồ sơ" is the absence of a call. Reading the state must not have written.
            using (var db = NewContext())
                await StateHandler(db, contact.UserId).Handle(
                    new GetOperationalContactStateQuery(requestId, instanceId), CancellationToken.None);

            using (var db = NewContext())
            {
                var account = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == contact.UserId);
                Assert.Equal("Nguyen Van A", account.FullName);
                Assert.Equal("+84912345678", account.Phone);

                var detail = await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instanceId);
                Assert.Equal("Tên Trong Đơn", detail.OperationalContactFullName);
            }
        }
        finally
        {
            await CleanupAsync(requestId);
            await RestoreProfileAsync(contact.UserId, contact.FullName, contact.Phone);
        }
    }

    /// <summary>
    /// PROFILE-SYNC-05, 06, 07 and 10. Accepting copies the two approved fields onto the account through
    /// the canonical self-service profile command — and touches nothing else.
    ///
    /// <para>
    /// The direction matters: snapshot → account, once, for this person. The visit's own record of what
    /// it was told keeps saying exactly that, because a visit that happened is a historical fact and not
    /// a view of the current directory (§6.6).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Updating_the_profile_copies_only_name_and_phone_and_leaves_the_snapshot_alone()
    {
        RequireDb();
        ulong requestId = 0;
        var contact = await OtherVisitorAsync();
        try
        {
            await RestoreProfileAsync(contact.UserId, "Nguyen Van A", "+84912345678");
            requestId = await CreateAsync(Campus("HN", contact.Email, "Nguyen Van An", "+84900000444"));
            var instanceId = await InstanceAsync(requestId);
            await BindContactAsync(instanceId, contact.UserId);

            string? emailBefore, roleBefore, statusBefore;
            int identityChangesBefore;
            using (var db = NewContext())
            {
                var before = await db.Users.AsNoTracking().Include(u => u.Role)
                    .SingleAsync(u => u.UserId == contact.UserId);
                emailBefore = before.Email;
                roleBefore = before.Role.RoleCode;
                statusBefore = before.Status;

                // Creation already raised this campus's INITIAL_CONFIRMATION invitation. What matters is
                // that the profile update adds nothing to it — counted, not assumed absent.
                identityChangesBefore = await db.VisitRequestIdentityChanges.AsNoTracking()
                    .CountAsync(c => c.VisitRequestId == requestId);
            }

            // "Cập nhật hồ sơ cá nhân" — the canonical self-profile command, caller resolved from the
            // session. Deliberately NOT an operational-contact handler (§6.7).
            using (var db = NewContext())
                await ProfileHandler(db, contact.UserId).Handle(
                    new UpdateProfileCommand { FullName = "Nguyen Van An", Phone = "+84900000444" },
                    CancellationToken.None);

            using (var db = NewContext())
            {
                var after = await db.Users.AsNoTracking().Include(u => u.Role)
                    .SingleAsync(u => u.UserId == contact.UserId);
                Assert.Equal("Nguyen Van An", after.FullName);
                Assert.Equal("+84900000444", after.Phone);

                // Identity, role and status are untouched.
                Assert.Equal(emailBefore, after.Email);
                Assert.Equal(roleBefore, after.Role.RoleCode);
                Assert.Equal(statusBefore, after.Status);

                // The visit still records what it was told. Organization and job title live only here —
                // the users table has no column for either.
                var detail = await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instanceId);
                Assert.Equal("Nguyen Van An", detail.OperationalContactFullName);
                Assert.Equal("OrgB", detail.OperationalContactOrganization);
                Assert.Equal("Trưởng phòng", detail.OperationalContactJobTitle);

                // No confirmation was raised and no transfer started: this was never an identity change.
                Assert.Equal(identityChangesBefore, await db.VisitRequestIdentityChanges.AsNoTracking()
                    .CountAsync(c => c.VisitRequestId == requestId));
                Assert.False(await db.VisitRequestIdentityChanges.AsNoTracking()
                    .AnyAsync(c => c.VisitRequestId == requestId
                                   && c.ChangeKind == IdentityChangeKinds.Transfer));
            }

            // …and the prompt is gone, because there is nothing left to reconcile.
            using (var db = NewContext())
            {
                var state = await StateHandler(db, contact.UserId).Handle(
                    new GetOperationalContactStateQuery(requestId, instanceId), CancellationToken.None);
                Assert.Null(state.ProfileDifference);
            }
        }
        finally
        {
            await CleanupAsync(requestId);
            await RestoreProfileAsync(contact.UserId, contact.FullName, contact.Phone);
        }
    }

    /// <summary>
    /// PROFILE-SYNC-08. The self-service command has no target parameter at all — it resolves the caller
    /// from the session — so "update someone else's profile" is not a request that can be expressed.
    /// This pins that: the registrant calling it moves their OWN row and never the contact's.
    /// </summary>
    [Fact]
    public async Task The_registrant_calling_profile_update_can_only_move_their_own_row()
    {
        RequireDb();
        var contact = await OtherVisitorAsync();
        string? registrantName, registrantPhone;
        using (var db = NewContext())
        {
            var r = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == Registrant);
            registrantName = r.FullName;
            registrantPhone = r.Phone;
        }

        try
        {
            await RestoreProfileAsync(contact.UserId, "Contact Untouched", "+84911111111");

            using (var db = NewContext())
                await ProfileHandler(db, Registrant).Handle(
                    new UpdateProfileCommand { FullName = "Registrant Renamed Themself" },
                    CancellationToken.None);

            using var verify = NewContext();
            Assert.Equal("Registrant Renamed Themself",
                (await verify.Users.AsNoTracking().SingleAsync(u => u.UserId == Registrant)).FullName);
            Assert.Equal("Contact Untouched",
                (await verify.Users.AsNoTracking().SingleAsync(u => u.UserId == contact.UserId)).FullName);
        }
        finally
        {
            await RestoreProfileAsync(Registrant, registrantName, registrantPhone);
            await RestoreProfileAsync(contact.UserId, contact.FullName, contact.Phone);
        }
    }

    // ── ACCOUNT-01, 03, 04, 06 ────────────────────────────────────────────────────

    /// <summary>
    /// ACCOUNT-01 and ACCOUNT-06. Naming an address raises an invitation and nothing else. No account is
    /// created for it, and the campus is bound to nobody while the invitation is pending.
    /// </summary>
    [Fact]
    public async Task Typing_a_contact_email_creates_no_account_and_binds_nobody()
    {
        RequireDb();
        ulong requestId = 0;
        var strangerEmail = $"never.seen.{Guid.NewGuid():N}@example.com";
        try
        {
            int usersBefore;
            using (var db = NewContext())
                usersBefore = await db.Users.CountAsync();

            requestId = await CreateAsync(Campus("HN", strangerEmail, "Người Lạ", "+84900000555"));
            var instanceId = await InstanceAsync(requestId);

            using var verify = NewContext();

            // No row was created for the address that was merely typed.
            Assert.False(await verify.Users.AsNoTracking().AnyAsync(u => u.Email == strangerEmail));
            Assert.Equal(usersBefore, await verify.Users.CountAsync());

            // The campus has no contact, and the request sits behind the confirmation gate.
            var campus = await verify.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Null(campus.OperationalContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, campus.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// ACCOUNT-03 and ACCOUNT-04. An address that already has an ACTIVE account reuses it — one row,
    /// same id — and the snapshot's own wording stays contextual rather than being pushed onto it.
    /// </summary>
    [Fact]
    public async Task An_existing_account_is_reused_and_never_overwritten_by_the_snapshot()
    {
        RequireDb();
        ulong requestId = 0;
        var contact = await OtherVisitorAsync();
        try
        {
            await RestoreProfileAsync(contact.UserId, "Canonical Account Name", "+84912345678");

            // The registrant describes them differently for this visit. Both are legitimate.
            requestId = await CreateAsync(Campus("HN", contact.Email, "Tên Theo Đoàn", "+84900000666"));
            var instanceId = await InstanceAsync(requestId);

            using var verify = NewContext();

            // Exactly one account for that address — no duplicate was minted for the different wording.
            Assert.Equal(1, await verify.Users.AsNoTracking().CountAsync(u => u.Email == contact.Email));

            // The account keeps its own name and number.
            var account = await verify.Users.AsNoTracking().SingleAsync(u => u.UserId == contact.UserId);
            Assert.Equal("Canonical Account Name", account.FullName);
            Assert.Equal("+84912345678", account.Phone);

            // The visit keeps what it was told.
            var detail = await verify.VisitInstanceFormDetails.AsNoTracking()
                .SingleAsync(d => d.VisitInstanceId == instanceId);
            Assert.Equal("Tên Theo Đoàn", detail.OperationalContactFullName);
        }
        finally
        {
            await CleanupAsync(requestId);
            await RestoreProfileAsync(contact.UserId, contact.FullName, contact.Phone);
        }
    }

    /// <summary>
    /// ACCOUNT-02, the third part: an UNAUTHENTICATED caller cannot accept, so no binding can happen
    /// from possession of the link alone.
    ///
    /// <para>
    /// Two independent bars enforce it — <c>[Authorize]</c> on the accept route and this guard inside
    /// the handler — and this is the one that holds even for an internal caller that bypasses routing.
    /// The other two parts of ACCOUNT-02 (authenticated invitee binds; authenticated non-invitee is
    /// refused) are proven end-to-end through the accept handler in
    /// <c>OperationalContactConfirmationWorkflowTests</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void An_unauthenticated_caller_cannot_accept_a_contact_invitation()
    {
        Assert.Throws<ForbiddenException>(() =>
            OperationalContactGuards.RequireAuthenticated(WriteOn, new AnonymousUser()));
    }

    private sealed class AnonymousUser : ICurrentUserService
    {
        public ulong? UserId => null;
        public string? Email => null;
        public string? RoleCode => null;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? RoleId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
        public bool IsAuthenticated => false;
    }

    /// <summary>
    /// ACCOUNT-05. Eligibility is ACTIVE plus an address that matches the invitation. An inactive account
    /// is refused rather than quietly switched back on, and a session belonging to somebody else is
    /// refused rather than allowed to take the campus.
    /// </summary>
    [Fact]
    public async Task An_inactive_or_mismatched_account_cannot_take_the_contact_role()
    {
        RequireDb();
        var contact = await OtherVisitorAsync();

        var change = new Domain.Entities.Delegations.VisitRequestIdentityChange
        {
            NewEmailNormalized = VisitRequestFingerprintBuilder.NormalizeEmail(contact.Email),
        };

        using var db = NewContext();
        var actor = await db.Users.Include(u => u.Role).AsNoTracking()
            .SingleAsync(u => u.UserId == contact.UserId);

        // Matching address, ACTIVE — allowed.
        OperationalContactGuards.EnsureActorMayTakeContactRole(actor, change);

        // Same person, deactivated — refused, and nothing reactivates them.
        var inactive = await db.Users.Include(u => u.Role).AsNoTracking()
            .SingleAsync(u => u.UserId == contact.UserId);
        inactive.Status = UserStatuses.Inactive;
        var refusedInactive = Assert.Throws<BusinessRuleException>(
            () => OperationalContactGuards.EnsureActorMayTakeContactRole(inactive, change));
        Assert.Equal(OperationalContactErrorCodes.AccountInactive, refusedInactive.ErrorCode);

        // Signed in as somebody else — refused. Possession of the link is not authority.
        var someoneElse = await db.Users.Include(u => u.Role).AsNoTracking()
            .SingleAsync(u => u.UserId == Registrant);
        var refusedMismatch = Assert.Throws<ConflictException>(
            () => OperationalContactGuards.EnsureActorMayTakeContactRole(someoneElse, change));
        Assert.Equal(OperationalContactErrorCodes.EmailMismatch, refusedMismatch.ErrorCode);
    }
}
