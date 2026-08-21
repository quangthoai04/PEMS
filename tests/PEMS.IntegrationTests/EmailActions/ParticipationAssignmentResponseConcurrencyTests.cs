using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.Commands.AssignDepartmentStaff;
using PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.EmailActions;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.EmailActions;

/// <summary>
/// Cross-channel concurrency for a Department-Staff participation assignment (spec BUG-02 + the
/// "no Portal-vs-Email race" correction): the ASSIGNED participant row created by
/// <see cref="AssignDepartmentStaffCommandHandler"/> carries two real, one-time
/// PARTICIPATION_ASSIGNMENT_RESPONSE tokens (ACCEPT/DECLINE), and the same row can also be answered
/// from Portal (<see cref="RespondVisitParticipantInvitationCommandHandler"/>).
///
/// <para>
/// Both entry points now go through the same locked <c>VisitInvitationResponse.ApplyCoreAsync</c>
/// (tiers 2-4 of the shared lock hierarchy), so a genuine race between them must serialize instead of
/// letting one commit on a stale pre-lock read. EF InMemory cannot observe this — it has no row locks —
/// so both tests below run against the real disposable MySQL database and drive two separate
/// connections, following the same pattern as <c>AssignDepartmentStaffAtomicityTests</c>.
/// </para>
/// </summary>
public sealed class ParticipationAssignmentResponseConcurrencyTests
    : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-PART-ASSIGN-CONCURRENCY] ";
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);

    private readonly PemsWebApplicationFactory _factory;

    private ulong _campusId;
    private ulong _departmentId;
    private ulong _leaderUserId;
    private ulong _staffUserId;
    private ulong _campusHostUserId;
    private ulong _registrantUserId;
    private ulong _visitRequestId;
    private ulong _visitInstanceId;
    private ulong _leaderParticipantId;

    public ParticipationAssignmentResponseConcurrencyTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var departmentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department).Select(r => r.RoleId).FirstAsync();

        _campusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active)
            .OrderBy(c => c.CampusId).Select(c => c.CampusId).FirstAsync();

        _departmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == _campusId && d.DepartmentType == "GENERAL" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).Select(d => d.DepartmentId).FirstAsync();

        var leader = new User
        {
            FullName = $"{TestPrefix}Leader",
            Email = "it-part-assign-conc-leader@pems.test",
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
            Email = "it-part-assign-conc-staff@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Staff,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var campusStaffLeaderRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Staff).Select(r => r.RoleId).FirstAsync();
        // STAFF accounts must belong to an IC department (trigger), distinct from the GENERAL support
        // department the Leader/Staff pair above belongs to.
        var icDepartmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == _campusId && d.DepartmentType == "IC" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).Select(d => d.DepartmentId).FirstAsync();
        var campusStaffLeader = new User
        {
            FullName = $"{TestPrefix}CampusHost",
            Email = "it-part-assign-conc-host@pems.test",
            RoleId = campusStaffLeaderRoleId,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = _campusId,
            DepartmentId = icDepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.AddRange(leader, staff, campusStaffLeader);
        await db.SaveChangesAsync();
        _leaderUserId = leader.UserId;
        _staffUserId = staff.UserId;
        _campusHostUserId = campusStaffLeader.UserId;
        var campusHostUserId = _campusHostUserId;

        var registrant = new User
        {
            FullName = $"{TestPrefix}Registrant",
            Email = "it-part-assign-conc-registrant@pems.test",
            RoleId = await db.Roles.Where(r => r.RoleCode == RoleCodes.Visitor).Select(r => r.RoleId).FirstAsync(),
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(registrant);
        await db.SaveChangesAsync();
        _registrantUserId = registrant.UserId;

        var request = new VisitRequest
        {
            RequestCode = $"IT-PAC-{Guid.NewGuid().ToString("N")[..8]}",
            RegistrantUserId = registrant.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = "it-part-assign-conc-registrant@pems.test",
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
            Status = VisitInstanceStatuses.BeforeVisit,
            OperationalContactUserId = registrant.UserId,
            OperationalContactConfirmedAt = DateTime.Now,
            OperationalContactConfirmationSource = OperationalContactSources.RegistrantSelfMatch,
            // The BEFORE_VISIT trigger requires an official host + a Staff-Leader decision on record —
            // this suite is about the participant response race, not host/approval eligibility, so a
            // dedicated campus Staff Leader stands in for both.
            CurrentHostUserId = campusHostUserId,
            HostAssignedBy = campusHostUserId,
            HostAssignedAt = DateTime.Now,
            DecidedBy = campusHostUserId,
            DecidedAt = DateTime.Now,
            DecisionActorRole = DecisionActorRole.StaffLeader,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();
        _visitInstanceId = instance.VisitInstanceId;

        db.VisitInstanceFormDetails.Add(new VisitInstanceFormDetail
        {
            VisitInstanceId = _visitInstanceId,
            DelegationName = "Đoàn kiểm thử concurrency",
            VisitType = "MEETING",
            Purpose = "Kiểm thử race Portal/Email",
            WorkingContent = "Nội dung làm việc",
            OperationalContactFullName = "Đầu mối cơ sở",
            OperationalContactJobTitle = "Trưởng phòng Hợp tác",
            OperationalContactPhone = "0900000002",
            OperationalContactEmail = "it-part-assign-conc-op@pems.test",
            WorkingLanguage = "VI",
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

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
            "DELETE FROM notifications WHERE recipient_user_id IN ({0}, {1}, {2})", _leaderUserId, _staffUserId, _campusHostUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM audit_logs WHERE entity_type = 'VisitParticipant' AND entity_id IN "
            + "(SELECT participant_id FROM visit_participants WHERE visit_instance_id = {0})", _visitInstanceId);
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
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM users WHERE user_id IN ({0}, {1}, {2}, {3})",
            _leaderUserId, _staffUserId, _campusHostUserId, _registrantUserId);
    }

    /// <summary>Runs the real assignment handler and returns the (Accept, Decline) raw tokens by
    /// extracting them from the composed action block, since only the hash is ever persisted.</summary>
    private async Task<(string AcceptRaw, string DeclineRaw)> AssignStaffAndCaptureTokensAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var capture = new CapturingDispatcher(scope.ServiceProvider.GetRequiredService<ISystemEmailDispatcher>());
        var handler = new AssignDepartmentStaffCommandHandler(
            db,
            new StaticDepartmentLeader(_leaderUserId, _campusId, _departmentId),
            scope.ServiceProvider.GetRequiredService<IDateTimeService>(),
            capture,
            scope.ServiceProvider.GetRequiredService<IEmailActionTokenService>(),
            scope.ServiceProvider.GetRequiredService<IHtmlSanitizerService>(),
            scope.ServiceProvider.GetRequiredService<IFileStorageService>(),
            scope.ServiceProvider.GetRequiredService<IVisitFormReadService>(),
            new MySqlUserMutationLockService(db),
            scope.ServiceProvider.GetRequiredService<INotificationService>(),
            scope.ServiceProvider.GetRequiredService<PEMS.Application.Emails.Preview.IApprovedEmailContentResolver>());

        await handler.Handle(
            new AssignDepartmentStaffCommand(_leaderParticipantId, _staffUserId, "Nhờ em hỗ trợ đón đoàn.", null),
            CancellationToken.None);

        var block = capture.CapturedActionBlock ?? throw new InvalidOperationException("No action block captured.");
        var matches = Regex.Matches(block, "email-actions/([A-Za-z0-9_-]+)");
        Assert.True(matches.Count >= 2, $"Expected 2 tokens in the action block, found {matches.Count}.");
        return (matches[0].Groups[1].Value, matches[1].Groups[1].Value);
    }

    private ExecuteEmailActionCommandHandler EmailHandler(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new ExecuteEmailActionCommandHandler(
            db,
            scope.ServiceProvider.GetRequiredService<IDateTimeService>(),
            scope.ServiceProvider.GetRequiredService<IEmailActionTokenService>(),
            scope.ServiceProvider.GetRequiredService<IVisitFormReadService>(),
            new MySqlUserMutationLockService(db),
            scope.ServiceProvider.GetRequiredService<INotificationService>());
    }

    [Fact]
    public async Task Email_accept_and_email_decline_racing_the_same_assignment_settle_on_exactly_one_outcome()
    {
        string acceptRaw, declineRaw;
        using (var scope = _factory.Services.CreateScope())
            (acceptRaw, declineRaw) = await AssignStaffAndCaptureTokensAsync(scope);

        // Transaction A: Accept, holding the participant-row lock artificially so B is provably queued
        // behind it rather than racing past.
        var aHoldsLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = TimeSpan.FromMilliseconds(600);

        var acceptTask = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var locks = new MySqlUserMutationLockService(db);
            await using var tx = await db.Database.BeginTransactionAsync();
            var visitRequestId = await db.VisitRequestCampuses.Where(c => c.VisitInstanceId == _visitInstanceId)
                .Select(c => c.VisitRequestId).FirstAsync();
            await locks.LockVisitRequestsAsync(new[] { visitRequestId }, CancellationToken.None);
            await locks.LockVisitRequestCampusesAsync(new[] { _visitInstanceId }, CancellationToken.None);
            var participantId = await db.VisitParticipants
                .Where(p => p.VisitInstanceId == _visitInstanceId && p.UserId == _staffUserId)
                .Select(p => p.ParticipantId).FirstAsync();
            await locks.LockVisitParticipantsAsync(new[] { participantId }, CancellationToken.None);

            aHoldsLock.SetResult();
            await Task.Delay(hold);
            await tx.CommitAsync(); // releases the lock without mutating — B does the real work
        });

        var declineTask = Task.Run(async () =>
        {
            await aHoldsLock.Task;
            using var scope = _factory.Services.CreateScope();
            var handler = EmailHandler(scope);
            var blocked = Stopwatch.StartNew();
            var result = await handler.Handle(
                new ExecuteEmailActionCommand(declineRaw, "127.0.0.1", "xunit", "Không sắp xếp được lịch."),
                CancellationToken.None);
            blocked.Stop();
            return (Result: result, Waited: blocked.Elapsed);
        });

        await acceptTask.WaitAsync(LockWait);
        var (declineResult, waited) = await declineTask.WaitAsync(LockWait);

        Assert.True(waited >= TimeSpan.FromMilliseconds(300),
            $"Decline returned after only {waited.TotalMilliseconds:F0} ms while a holder kept the participant lock for {hold.TotalMilliseconds:F0} ms — it did not contend on the row lock.");
        Assert.Equal(EmailActionViewStatuses.Success, declineResult.Status);

        // Now the real Accept, using the token that already survived the artificial hold above.
        using (var scope = _factory.Services.CreateScope())
        {
            var handler = EmailHandler(scope);
            var acceptResult = await handler.Handle(
                new ExecuteEmailActionCommand(acceptRaw, "127.0.0.1", "xunit", null), CancellationToken.None);
            // The sibling (Decline) already consumed the group — Accept must see it as already answered.
            Assert.Equal(EmailActionViewStatuses.AlreadyResponded, acceptResult.Status);
        }

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participant = await checkDb.VisitParticipants.AsNoTracking()
            .FirstAsync(p => p.VisitInstanceId == _visitInstanceId && p.UserId == _staffUserId);
        Assert.Equal(ParticipantStatuses.Declined, participant.Status);
        Assert.Equal(1, await checkDb.AuditLogs.AsNoTracking()
            .CountAsync(a => a.EntityType == "VisitParticipant" && a.EntityId == participant.ParticipantId));
    }

    [Fact]
    public async Task Portal_accept_racing_email_decline_for_the_same_assignment_settle_on_exactly_one_outcome()
    {
        string acceptRaw, declineRaw;
        using (var scope = _factory.Services.CreateScope())
            (acceptRaw, declineRaw) = await AssignStaffAndCaptureTokensAsync(scope);

        var portalHoldsLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = TimeSpan.FromMilliseconds(600);

        var portalAccept = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var locks = new MySqlUserMutationLockService(db);
            await using var tx = await db.Database.BeginTransactionAsync();
            var visitRequestId = await db.VisitRequestCampuses.Where(c => c.VisitInstanceId == _visitInstanceId)
                .Select(c => c.VisitRequestId).FirstAsync();
            await locks.LockVisitRequestsAsync(new[] { visitRequestId }, CancellationToken.None);
            await locks.LockVisitRequestCampusesAsync(new[] { _visitInstanceId }, CancellationToken.None);
            var participantId = await db.VisitParticipants
                .Where(p => p.VisitInstanceId == _visitInstanceId && p.UserId == _staffUserId)
                .Select(p => p.ParticipantId).FirstAsync();
            await locks.LockVisitParticipantsAsync(new[] { participantId }, CancellationToken.None);

            portalHoldsLock.SetResult();
            await Task.Delay(hold);
            await tx.CommitAsync();
        });

        var emailDecline = Task.Run(async () =>
        {
            await portalHoldsLock.Task;
            using var scope = _factory.Services.CreateScope();
            var handler = EmailHandler(scope);
            var blocked = Stopwatch.StartNew();
            var result = await handler.Handle(
                new ExecuteEmailActionCommand(declineRaw, "127.0.0.1", "xunit", "Bận việc khác."),
                CancellationToken.None);
            blocked.Stop();
            return (Result: result, Waited: blocked.Elapsed);
        });

        await portalAccept.WaitAsync(LockWait);
        var (emailResult, waited) = await emailDecline.WaitAsync(LockWait);

        Assert.True(waited >= TimeSpan.FromMilliseconds(300),
            $"Email Decline returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the participant lock the simulated Portal holder took first.");
        Assert.Equal(EmailActionViewStatuses.Success, emailResult.Status);

        // The real Portal Accept, now that the lock has been released without mutating.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var handler = new RespondVisitParticipantInvitationCommandHandler(
                db,
                new StaticStaffMember(_staffUserId, _campusId, _departmentId),
                scope.ServiceProvider.GetRequiredService<IDateTimeService>(),
                scope.ServiceProvider.GetRequiredService<INotificationService>(),
                new MySqlUserMutationLockService(db));

            var staffParticipantId = await db.VisitParticipants
                .Where(p => p.VisitInstanceId == _visitInstanceId && p.UserId == _staffUserId)
                .Select(p => p.ParticipantId).FirstAsync();
            var ex = await Record.ExceptionAsync(() => handler.Handle(
                new RespondVisitParticipantInvitationCommand(staffParticipantId, true, null),
                CancellationToken.None));
            Assert.NotNull(ex); // already DECLINED by email — Portal Accept must be refused, not silently flip it
        }

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participant = await checkDb.VisitParticipants.AsNoTracking()
            .FirstAsync(p => p.VisitInstanceId == _visitInstanceId && p.UserId == _staffUserId);
        Assert.Equal(ParticipantStatuses.Declined, participant.Status);
    }

    // ── test doubles ──

    private sealed class CapturingDispatcher : ISystemEmailDispatcher
    {
        private readonly ISystemEmailDispatcher _inner;
        public CapturingDispatcher(ISystemEmailDispatcher inner) => _inner = inner;
        public string? CapturedActionBlock { get; private set; }

        public Task<SystemEmailDispatchResult> SendAsync(SystemEmailRequest request, CancellationToken ct = default)
            => _inner.SendAsync(request, ct);

        public Task<PreparedSystemEmail> PrepareAsync(SystemEmailRequest request, CancellationToken ct = default)
        {
            if (request.TrustedBlocks is not null
                && request.TrustedBlocks.TryGetValue(EmailTrustedBlocks.ActionBlock, out var block))
                CapturedActionBlock = block;
            return _inner.PrepareAsync(request, ct);
        }

        public Task<EmailDeliveryResult> DeliverAsync(PreparedSystemEmail prepared, CancellationToken ct = default)
            => _inner.DeliverAsync(prepared, ct);
    }

    private sealed class StaticDepartmentLeader : ICurrentUserService
    {
        public StaticDepartmentLeader(ulong userId, ulong campusId, ulong departmentId)
        {
            UserId = userId;
            PrimaryCampusId = campusId;
            DepartmentId = departmentId;
        }

        public ulong? UserId { get; }
        public string? Email => "it-part-assign-conc-leader@pems.test";
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

    private sealed class StaticStaffMember : ICurrentUserService
    {
        public StaticStaffMember(ulong userId, ulong campusId, ulong departmentId)
        {
            UserId = userId;
            PrimaryCampusId = campusId;
            DepartmentId = departmentId;
        }

        public ulong? UserId { get; }
        public string? Email => "it-part-assign-conc-staff@pems.test";
        public string? RoleCode => RoleCodes.Department;
        public string? SubRole => UserSubRoles.Staff;
        public ulong? RoleId => null;
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId { get; }
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }
}
