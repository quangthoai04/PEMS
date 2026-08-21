using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.Commands.AssignDepartmentStaff;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Delegations;

/// <summary>
/// The two properties of <see cref="AssignDepartmentStaffCommandHandler"/> that a green unit suite is
/// structurally unable to see: that the whole assignment is ONE transaction, and that it takes a real
/// row lock on the account it is about to make responsible for a visit.
///
/// <para>
/// These exist because both were lost. Merging Dev into Cảnh-Iter1 dropped the handler's
/// <c>BeginTransactionAsync</c> and its <see cref="IUserMutationLockService"/> call while the class
/// documentation went on describing them; 1693 unit tests, 945 integration tests, 56 real-stack
/// journeys and a full green CI run all passed over it, and a human reviewer found it by reading the
/// diff (finding C-1). Nothing in the suite asked "is this atomic?" or "does this serialise?", so
/// nothing answered.
/// </para>
/// <para>
/// EF InMemory cannot host either test. It has no rollback that observes partial writes and no row
/// locks at all, so it reports success for code that races and loses data in production. Both tests
/// below therefore run against the disposable MySQL database and, in the concurrency case, drive two
/// genuinely separate connections.
/// </para>
/// <para>
/// Both fail on the pre-fix handler: without a transaction the participant row survives a later
/// failure, and without the lock the assignment reads a role that a concurrent change has already
/// invalidated.
/// </para>
/// </summary>
public sealed class AssignDepartmentStaffAtomicityTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-ASSIGN-DEPT-STAFF] ";
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);

    private readonly PemsWebApplicationFactory _factory;

    private ulong _campusId;
    private ulong _departmentId;
    private ulong _leaderUserId;
    private ulong _staffUserId;
    private ulong _registrantUserId;
    private ulong _visitRequestId;
    private ulong _visitInstanceId;
    private ulong _leaderParticipantId;

    public AssignDepartmentStaffAtomicityTests(PemsWebApplicationFactory factory) => _factory = factory;

    // ── fixture ───────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var departmentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department).Select(r => r.RoleId).FirstAsync();

        _campusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active)
            .OrderBy(c => c.CampusId).Select(c => c.CampusId).FirstAsync();

        // GENERAL, not merely active: trg_users_* refuses a DEPARTMENT-role account whose department is
        // of any other type, so seeding into an IC department fails on save rather than in the assertion.
        _departmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == _campusId && d.DepartmentType == "GENERAL" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).Select(d => d.DepartmentId).FirstAsync();

        var leader = new User
        {
            FullName = $"{TestPrefix}Leader",
            Email = "it-assign-dept-staff-leader@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var staff = new User
        {
            FullName = $"{TestPrefix}Staff",
            Email = "it-assign-dept-staff-target@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Staff,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.AddRange(leader, staff);
        await db.SaveChangesAsync();
        _leaderUserId = leader.UserId;
        _staffUserId = staff.UserId;

        // The guest who submitted, and — self-matched — the campus's operational contact. A campus past
        // WAITING_CONTACT_CONFIRMATION may not have a NULL contact, and this suite is about the
        // atomicity of assigning department staff, so the shortest valid contact model is the right one.
        // Deliberately its own account: making the leader or the staff member a contact would hand them
        // guest-side rights and change what the assignment test is measuring.
        var registrant = new User
        {
            FullName = $"{TestPrefix}Registrant",
            Email = "it-assign-dept-staff-registrant@pems.test",
            RoleId = await db.Roles.Where(r => r.RoleCode == RoleCodes.Visitor)
                .Select(r => r.RoleId).FirstAsync(),
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(registrant);
        await db.SaveChangesAsync();
        _registrantUserId = registrant.UserId;

        var request = new VisitRequest
        {
            RequestCode = $"IT-ADS-{Guid.NewGuid().ToString("N")[..8]}",
            RegistrantUserId = registrant.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = "it-assign-dept-staff-registrant@pems.test",
            VisitScope = VisitScopes.SingleCampus,
            Status = VisitRequestStatuses.PendingApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(request);
        await db.SaveChangesAsync();
        _visitRequestId = request.VisitRequestId;

        var instance = new VisitRequestCampus
        {
            VisitRequestId = _visitRequestId,
            CampusId = _campusId,
            PlannedStartAt = DateTime.Now.AddDays(20),
            PlannedEndAt = DateTime.Now.AddDays(20).AddHours(2),
            Status = VisitInstanceStatuses.WaitingRequestApproval,
            OperationalContactUserId = registrant.UserId,
            OperationalContactConfirmedAt = DateTime.Now,
            OperationalContactConfirmationSource = OperationalContactSources.RegistrantSelfMatch,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();
        _visitInstanceId = instance.VisitInstanceId;

        // The per-campus form detail. The handler names the delegation in the assignment email and reads
        // it through IVisitFormReadService, which refuses an instance that has none — so without this the
        // test would fail on missing fixture data rather than on the property under test.
        db.VisitInstanceFormDetails.Add(new VisitInstanceFormDetail
        {
            VisitInstanceId = _visitInstanceId,
            DelegationName = "Đoàn kiểm thử phân công",
            VisitType = "MEETING",
            Purpose = "Kiểm thử tính nguyên tử của phân công",
            WorkingContent = "Nội dung làm việc",
            OperationalContactFullName = "Đầu mối cơ sở",
            OperationalContactJobTitle = "Trưởng phòng Hợp tác",
            OperationalContactPhone = "0900000002",
            OperationalContactEmail = "it-assign-dept-staff-op@pems.test",
            WorkingLanguage = "VI",
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        // The department's own invitation — the row the Leader is acting on.
        var leaderParticipant = new VisitParticipant
        {
            VisitInstanceId = _visitInstanceId,
            UserId = _leaderUserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            IsHost = false,
            Status = ParticipantStatuses.Accepted,
            CreatedAt = DateTime.Now,
        };
        db.VisitParticipants.Add(leaderParticipant);
        await db.SaveChangesAsync();
        _leaderParticipantId = leaderParticipant.ParticipantId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM email_action_tokens WHERE recipient_user_id IN ({0}, {1})", _leaderUserId, _staffUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM notifications WHERE recipient_user_id IN ({0}, {1})", _leaderUserId, _staffUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM sent_email_recipients WHERE sent_email_id IN "
            + "(SELECT sent_email_id FROM sent_emails WHERE sent_by IN ({0}, {1}))", _leaderUserId, _staffUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM sent_emails WHERE sent_by IN ({0}, {1})", _leaderUserId, _staffUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_participants WHERE visit_instance_id = {0}", _visitInstanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_instance_form_details WHERE visit_instance_id = {0}", _visitInstanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_request_campuses WHERE visit_request_id = {0}", _visitRequestId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_requests WHERE visit_request_id = {0}", _visitRequestId);
        // After the request and its campuses, so neither registrant_user_id nor
        // operational_contact_user_id is still pointing at the registrant.
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM users WHERE user_id IN ({0}, {1}, {2})",
            _leaderUserId, _staffUserId, _registrantUserId);
    }

    // ── C-1a: one transaction, or none of it ─────────────────────────────────

    /// <summary>
    /// A failure after the participant is written must leave NOTHING behind.
    ///
    /// <para>
    /// <c>PrepareAsync</c> is the realistic failure point: it is the first thing to run after the
    /// participant row is saved, and it genuinely throws in production when a template is missing or
    /// inactive — a configuration fault, not a delivery outcome. Without the transaction the participant
    /// row and the leader's status change are already committed by then, so the department sees a staff
    /// member assigned to a visit who never received a message and holds no accept/decline token. That
    /// is the partial state this asserts is impossible.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_failure_after_the_participant_is_written_leaves_no_trace()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var handler = CreateHandler(scope, new ThrowingDispatcher());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.Handle(NewCommand(), CancellationToken.None));
        }

        // A FRESH scope, i.e. a different connection: anything visible here was committed.
        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Empty(await db.VisitParticipants.AsNoTracking()
            .Where(p => p.VisitInstanceId == _visitInstanceId && p.UserId == _staffUserId)
            .ToListAsync());

        // The Leader's own row must not have been advanced either — it moves in the same transaction.
        var leaderRow = await db.VisitParticipants.AsNoTracking()
            .FirstAsync(p => p.ParticipantId == _leaderParticipantId);
        Assert.Equal(ParticipantStatuses.Accepted, leaderRow.Status);

        Assert.Empty(await db.EmailActionTokens.AsNoTracking()
            .Where(t => t.RecipientUserId == _staffUserId).ToListAsync());
        Assert.Empty(await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == _staffUserId).ToListAsync());
        Assert.Empty(await db.SentEmails.AsNoTracking()
            .Where(e => e.SentBy == _leaderUserId).ToListAsync());
    }

    // ── C-1b: the account is locked, so a role change cannot slip underneath ──

    /// <summary>
    /// A role change holding the user lock forces the assignment to wait, and the assignment then reads
    /// the committed role and refuses.
    ///
    /// <para>
    /// This is the race the lock exists for. Transaction A takes the lock and re-roles the account to
    /// STUDENT — no longer a department member, so no longer assignable. Transaction B is the real
    /// handler. If it takes the same lock it blocks until A commits, re-reads, and refuses with a
    /// <see cref="ConflictException"/>. If it does NOT take the lock — the state the merge left it in —
    /// it reads the stale DEPARTMENT role, sails past every eligibility check and commits an assignment
    /// against an account that is now a student.
    /// </para>
    /// <para>
    /// The final database state is asserted, not merely the exception: a test that only checks timing
    /// would pass against a handler that blocks and then assigns anyway.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_concurrent_role_change_cannot_slip_under_the_eligibility_checks()
    {
        // A holds the lock this long after B has been released to call the handler; B cannot finish
        // sooner than that unless it skipped the lock entirely.
        var hold = TimeSpan.FromMilliseconds(750);
        var blockedFloor = TimeSpan.FromMilliseconds(400);

        var roleChangeHoldsLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Transaction A: lock the staff account and re-role it out of the department.
        var roleChange = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var locks = new MySqlUserMutationLockService(db);

            await using var tx = await db.Database.BeginTransactionAsync();
            await locks.LockUsersAsync(new[] { _staffUserId }, CancellationToken.None);

            roleChangeHoldsLock.SetResult();
            // Hold it long enough that the assignment is definitely queued behind the lock.
            await Task.Delay(hold);

            var studentRoleId = await db.Roles.AsNoTracking()
                .Where(r => r.RoleCode == RoleCodes.Student).Select(r => r.RoleId).FirstAsync();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET role_id = {0}, sub_role = NULL, department_id = NULL, "
                + "student_code = 'ITADS0001' WHERE user_id = {1}",
                studentRoleId, _staffUserId);

            await tx.CommitAsync();
        });

        // Transaction B: the real assignment handler, wanting the same account.
        var assignment = Task.Run(async () =>
        {
            await roleChangeHoldsLock.Task;

            using var scope = _factory.Services.CreateScope();
            var handler = CreateHandler(scope, scope.ServiceProvider.GetRequiredService<ISystemEmailDispatcher>());

            var blocked = Stopwatch.StartNew();
            var error = await Record.ExceptionAsync(() => handler.Handle(NewCommand(), CancellationToken.None));
            blocked.Stop();
            return (Error: error, Waited: blocked.Elapsed);
        });

        await roleChange.WaitAsync(LockWait);
        var (thrown, waited) = await assignment.WaitAsync(LockWait);

        // It waited for the lock rather than racing past it. Measured, not inferred from a signal: the
        // handler cannot return while another transaction holds the row it must lock first.
        Assert.True(waited >= blockedFloor,
            $"The assignment returned after only {waited.TotalMilliseconds:F0} ms while the role change held "
            + $"the user lock for {hold.TotalMilliseconds:F0} ms — it is not contending on the user lock.");

        // Having waited, it saw the committed STUDENT role and refused.
        Assert.NotNull(thrown);
        Assert.IsType<ConflictException>(thrown);

        // And the refusal is real: no participant, no token, no notification for a student account.
        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Empty(await db.VisitParticipants.AsNoTracking()
            .Where(p => p.VisitInstanceId == _visitInstanceId && p.UserId == _staffUserId)
            .ToListAsync());
        Assert.Empty(await db.EmailActionTokens.AsNoTracking()
            .Where(t => t.RecipientUserId == _staffUserId).ToListAsync());
        Assert.Empty(await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == _staffUserId).ToListAsync());
    }

    // ── C-1c: committed before SMTP, so a delivery failure cannot undo it ────

    /// <summary>
    /// A delivery failure AFTER the commit must leave the assignment fully intact.
    ///
    /// <para>
    /// The mirror image of the rollback test, and the reason the commit sits before
    /// <c>DeliverAsync</c> rather than after it. A transient SMTP or file-store fault is not a reason to
    /// un-assign somebody; the message is marked failed and stays resendable, while the participant row,
    /// both response tokens and the notification remain.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_delivery_failure_after_the_commit_keeps_the_assignment()
    {
        ulong participantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var real = scope.ServiceProvider.GetRequiredService<ISystemEmailDispatcher>();
            var handler = CreateHandler(scope, new FailingDeliveryDispatcher(real));

            participantId = await handler.Handle(NewCommand(), CancellationToken.None);
        }

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var participant = await db.VisitParticipants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ParticipantId == participantId);
        Assert.NotNull(participant);
        Assert.Equal(ParticipantStatuses.Assigned, participant!.Status);
        Assert.Equal(_staffUserId, participant.UserId);

        Assert.Equal(2, await db.EmailActionTokens.AsNoTracking()
            .CountAsync(t => t.TargetId == participantId));
        Assert.NotEmpty(await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == _staffUserId).ToListAsync());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private AssignDepartmentStaffCommand NewCommand()
        => new(_leaderParticipantId, _staffUserId, "Nhờ em hỗ trợ đón đoàn.");

    /// <summary>
    /// Builds the handler from the container, substituting only the dispatcher. Everything else — the
    /// DbContext, the real <see cref="MySqlUserMutationLockService"/>, tokens, sanitiser, storage and
    /// form reader — is the production registration, so the transaction and lock under test are the real
    /// ones rather than a test double that would prove nothing.
    /// </summary>
    private AssignDepartmentStaffCommandHandler CreateHandler(IServiceScope scope, ISystemEmailDispatcher dispatcher)
    {
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();

        return new AssignDepartmentStaffCommandHandler(
            db,
            new StaticDepartmentLeader(_leaderUserId, _campusId, _departmentId),
            sp.GetRequiredService<IDateTimeService>(),
            dispatcher,
            sp.GetRequiredService<IEmailActionTokenService>(),
            sp.GetRequiredService<IHtmlSanitizerService>(),
            sp.GetRequiredService<IFileStorageService>(),
            sp.GetRequiredService<IVisitFormReadService>(),
            new MySqlUserMutationLockService(db),
            sp.GetRequiredService<PEMS.Application.Notifications.Common.INotificationService>(),
            sp.GetRequiredService<PEMS.Application.Emails.Preview.IApprovedEmailContentResolver>());
    }

    /// <summary>Fails where a missing or inactive template fails: while recording the message.</summary>
    private sealed class ThrowingDispatcher : ISystemEmailDispatcher
    {
        public Task<SystemEmailDispatchResult> SendAsync(SystemEmailRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("Template không khả dụng (kiểm thử).");

        public Task<PreparedSystemEmail> PrepareAsync(SystemEmailRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("Template không khả dụng (kiểm thử).");

        public Task<EmailDeliveryResult> DeliverAsync(PreparedSystemEmail prepared, CancellationToken ct = default)
            => throw new InvalidOperationException("DeliverAsync should not be reached in this test.");
    }

    /// <summary>Records the message for real, then fails at the point SMTP would.</summary>
    private sealed class FailingDeliveryDispatcher : ISystemEmailDispatcher
    {
        private readonly ISystemEmailDispatcher _inner;
        public FailingDeliveryDispatcher(ISystemEmailDispatcher inner) => _inner = inner;

        public Task<SystemEmailDispatchResult> SendAsync(SystemEmailRequest request, CancellationToken ct = default)
            => _inner.SendAsync(request, ct);

        public Task<PreparedSystemEmail> PrepareAsync(SystemEmailRequest request, CancellationToken ct = default)
            => _inner.PrepareAsync(request, ct);

        public Task<EmailDeliveryResult> DeliverAsync(PreparedSystemEmail prepared, CancellationToken ct = default)
            => throw new InvalidOperationException("SMTP không khả dụng (kiểm thử).");
    }

    /// <summary>The acting Department Leader.</summary>
    private sealed class StaticDepartmentLeader : ICurrentUserService
    {
        public StaticDepartmentLeader(ulong userId, ulong campusId, ulong departmentId)
        {
            UserId = userId;
            PrimaryCampusId = campusId;
            DepartmentId = departmentId;
        }

        public ulong? UserId { get; }
        public string? Email => "it-assign-dept-staff-leader@pems.test";
        public string? RoleCode => RoleCodes.Department;
        public string? SubRole => UserSubRoles.Leader;
        public ulong? RoleId => null;
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId { get; }
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }
}
