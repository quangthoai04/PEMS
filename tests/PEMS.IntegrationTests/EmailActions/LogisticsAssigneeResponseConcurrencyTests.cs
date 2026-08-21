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
using PEMS.Application.DepartmentReceptionTasks.Commands.AcceptAssignedLogisticsTask;
using PEMS.Application.DepartmentReceptionTasks.Commands.AssignRequestAssignee;
using PEMS.Application.DepartmentReceptionTasks.Commands.DeclineAssignedLogisticsTask;
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
/// Cross-channel concurrency for a logistics assignee response (spec's Logistics email/Portal race list):
/// <see cref="AssignRequestAssigneeCommandHandler"/> mints two real, one-time LOGISTICS_ASSIGNEE_RESPONSE
/// tokens (ACCEPT/DECLINE) on the assigned staff member's email, and the same item can also be answered
/// from Portal (<see cref="AcceptAssignedLogisticsTaskCommandHandler"/> / <see
/// cref="DeclineAssignedLogisticsTaskCommandHandler"/>).
///
/// <para>
/// All three entry points lock the same tier 2-4 hierarchy (VisitRequest → VisitRequestCampus →
/// VisitLogisticsItem) before deciding, and <see cref="ExecuteEmailActionCommandHandler"/> additionally
/// re-locks and re-verifies the token group at tier 5 immediately before consuming it — so a genuine race
/// between any two of them must serialize instead of one committing on a stale pre-lock read. Run against
/// real disposable MySQL with two separate connections, mirroring
/// <see cref="ParticipationAssignmentResponseConcurrencyTests"/>.
/// </para>
/// </summary>
public sealed class LogisticsAssigneeResponseConcurrencyTests
    : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-LOGI-ASSIGNEE-CONCURRENCY] ";
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
    private ulong _logisticsItemId;

    public LogisticsAssigneeResponseConcurrencyTests(PemsWebApplicationFactory factory) => _factory = factory;

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
            Email = "it-logi-assignee-conc-leader@pems.test",
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
            Email = "it-logi-assignee-conc-staff@pems.test",
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
            Email = "it-logi-assignee-conc-host@pems.test",
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
            Email = "it-logi-assignee-conc-registrant@pems.test",
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
            RequestCode = $"IT-LGA-{Guid.NewGuid().ToString("N")[..8]}",
            RegistrantUserId = registrant.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = "it-logi-assignee-conc-registrant@pems.test",
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
            DelegationName = "Đoàn kiểm thử logistics concurrency",
            VisitType = "MEETING",
            Purpose = "Kiểm thử race Portal/Email hậu cần",
            WorkingContent = "Nội dung làm việc",
            OperationalContactFullName = "Đầu mối cơ sở",
            OperationalContactJobTitle = "Trưởng phòng Hợp tác",
            OperationalContactPhone = "0900000002",
            OperationalContactEmail = "it-logi-assignee-conc-op@pems.test",
            WorkingLanguage = "VI",
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        var item = new VisitLogisticsItem
        {
            VisitInstanceId = _visitInstanceId,
            ItemType = "EQUIPMENT",
            Title = "Máy chiếu hội trường",
            Status = LogisticsItemStatus.Requested,
            CoordinationMode = "SYSTEM_REQUEST",
            RequestedToDepartmentId = _departmentId,
            RequestedBy = campusHostUserId,
            RequestedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        db.VisitLogisticsItems.Add(item);
        await db.SaveChangesAsync();
        _logisticsItemId = item.LogisticsItemId;
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
            "DELETE FROM audit_logs WHERE entity_type = 'VisitLogisticsItem' AND entity_id = {0}", _logisticsItemId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_logistics_assignment_attempts WHERE logistics_item_id = {0}", _logisticsItemId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM sent_email_recipients WHERE sent_email_id IN "
            + "(SELECT sent_email_id FROM sent_emails WHERE sent_by IN ({0}, {1}))", _leaderUserId, _staffUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM sent_emails WHERE sent_by IN ({0}, {1})", _leaderUserId, _staffUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_logistics_items WHERE visit_instance_id = {0}", _visitInstanceId);
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
    private async Task<(string AcceptRaw, string DeclineRaw)> AssignAndCaptureTokensAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var capture = new CapturingDispatcher(scope.ServiceProvider.GetRequiredService<ISystemEmailDispatcher>());
        var handler = new AssignRequestAssigneeCommandHandler(
            db,
            new StaticDepartmentLeader(_leaderUserId, _campusId, _departmentId),
            scope.ServiceProvider.GetRequiredService<IDateTimeService>(),
            capture,
            scope.ServiceProvider.GetRequiredService<IEmailActionTokenService>(),
            scope.ServiceProvider.GetRequiredService<IHtmlSanitizerService>(),
            scope.ServiceProvider.GetRequiredService<IFileStorageService>(),
            scope.ServiceProvider.GetRequiredService<PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer>(),
            scope.ServiceProvider.GetRequiredService<INotificationService>(),
            new MySqlUserMutationLockService(db),
            scope.ServiceProvider.GetRequiredService<PEMS.Application.Emails.Preview.IApprovedEmailContentResolver>());

        await handler.Handle(
            new AssignRequestAssigneeCommand { LogisticsItemId = _logisticsItemId, AssigneeUserId = _staffUserId },
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
            scope.ServiceProvider.GetRequiredService<PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService>(),
            new MySqlUserMutationLockService(db),
            scope.ServiceProvider.GetRequiredService<INotificationService>());
    }

    /// <summary>Locks the item's tier 2-4 hierarchy and holds it artificially so the concurrent real
    /// action is provably queued behind it, then releases without mutating — the real work happens in
    /// the other task once unblocked. Mirrors <see cref="ParticipationAssignmentResponseConcurrencyTests"/>.</summary>
    private async Task HoldLogisticsItemLockAsync(TaskCompletionSource holding, TimeSpan hold)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var locks = new MySqlUserMutationLockService(db);
        await using var tx = await db.Database.BeginTransactionAsync();
        await locks.LockVisitRequestsAsync(new[] { _visitRequestId }, CancellationToken.None);
        await locks.LockVisitRequestCampusesAsync(new[] { _visitInstanceId }, CancellationToken.None);
        await locks.LockVisitLogisticsItemsAsync(new[] { _logisticsItemId }, CancellationToken.None);

        holding.SetResult();
        await Task.Delay(hold);
        await tx.CommitAsync(); // releases the lock without mutating
    }

    [Fact]
    public async Task Email_accept_and_email_decline_racing_the_same_assignment_settle_on_exactly_one_outcome()
    {
        string acceptRaw, declineRaw;
        using (var scope = _factory.Services.CreateScope())
            (acceptRaw, declineRaw) = await AssignAndCaptureTokensAsync(scope);

        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = TimeSpan.FromMilliseconds(600);

        var holderTask = Task.Run(() => HoldLogisticsItemLockAsync(holding, hold));

        var declineTask = Task.Run(async () =>
        {
            await holding.Task;
            using var scope = _factory.Services.CreateScope();
            var handler = EmailHandler(scope);
            var blocked = Stopwatch.StartNew();
            var result = await handler.Handle(
                new ExecuteEmailActionCommand(declineRaw, "127.0.0.1", "xunit", "Không sắp xếp được lịch."),
                CancellationToken.None);
            blocked.Stop();
            return (Result: result, Waited: blocked.Elapsed);
        });

        await holderTask.WaitAsync(LockWait);
        var (declineResult, waited) = await declineTask.WaitAsync(LockWait);

        Assert.True(waited >= TimeSpan.FromMilliseconds(300),
            $"Decline returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the item lock.");
        Assert.Equal(EmailActionViewStatuses.Success, declineResult.Status);

        using (var scope = _factory.Services.CreateScope())
        {
            var handler = EmailHandler(scope);
            var acceptResult = await handler.Handle(
                new ExecuteEmailActionCommand(acceptRaw, "127.0.0.1", "xunit", null), CancellationToken.None);
            Assert.Equal(EmailActionViewStatuses.AlreadyResponded, acceptResult.Status);
        }

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await checkDb.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == _logisticsItemId);
        Assert.Equal(LogisticsItemStatus.Declined, item.Status);
    }

    [Fact]
    public async Task Portal_accept_racing_email_decline_for_the_same_assignment_settle_on_exactly_one_outcome()
    {
        string acceptRaw, declineRaw;
        using (var scope = _factory.Services.CreateScope())
            (acceptRaw, declineRaw) = await AssignAndCaptureTokensAsync(scope);

        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = TimeSpan.FromMilliseconds(600);

        var holderTask = Task.Run(() => HoldLogisticsItemLockAsync(holding, hold));

        var emailDeclineTask = Task.Run(async () =>
        {
            await holding.Task;
            using var scope = _factory.Services.CreateScope();
            var handler = EmailHandler(scope);
            var blocked = Stopwatch.StartNew();
            var result = await handler.Handle(
                new ExecuteEmailActionCommand(declineRaw, "127.0.0.1", "xunit", "Bận việc khác."),
                CancellationToken.None);
            blocked.Stop();
            return (Result: result, Waited: blocked.Elapsed);
        });

        await holderTask.WaitAsync(LockWait);
        var (emailResult, waited) = await emailDeclineTask.WaitAsync(LockWait);

        Assert.True(waited >= TimeSpan.FromMilliseconds(300),
            $"Email Decline returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the item lock.");
        Assert.Equal(EmailActionViewStatuses.Success, emailResult.Status);

        // The real Portal Accept, now that the lock has been released and the item already DECLINED.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var handler = new AcceptAssignedLogisticsTaskCommandHandler(
                db, new StaticStaffMember(_staffUserId, _campusId, _departmentId),
                scope.ServiceProvider.GetRequiredService<INotificationService>(),
                new MySqlUserMutationLockService(db));
            var ex = await Record.ExceptionAsync(() => handler.Handle(
                new AcceptAssignedLogisticsTaskCommand { LogisticsItemId = _logisticsItemId }, CancellationToken.None));
            Assert.NotNull(ex); // already DECLINED by email — Portal Accept must be refused, not silently flip it
        }

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await checkDb.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == _logisticsItemId);
        Assert.Equal(LogisticsItemStatus.Declined, item.Status);
    }

    [Fact]
    public async Task Portal_decline_racing_email_accept_for_the_same_assignment_settle_on_exactly_one_outcome()
    {
        string acceptRaw, declineRaw;
        using (var scope = _factory.Services.CreateScope())
            (acceptRaw, declineRaw) = await AssignAndCaptureTokensAsync(scope);

        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = TimeSpan.FromMilliseconds(600);

        var holderTask = Task.Run(() => HoldLogisticsItemLockAsync(holding, hold));

        var portalDeclineTask = Task.Run(async () =>
        {
            await holding.Task;
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var handler = new DeclineAssignedLogisticsTaskCommandHandler(
                db, new StaticStaffMember(_staffUserId, _campusId, _departmentId),
                scope.ServiceProvider.GetRequiredService<INotificationService>(),
                new MySqlUserMutationLockService(db));
            var blocked = Stopwatch.StartNew();
            await handler.Handle(
                new DeclineAssignedLogisticsTaskCommand { LogisticsItemId = _logisticsItemId, Reason = "Đang bận nhiệm vụ khác" },
                CancellationToken.None);
            blocked.Stop();
            return blocked.Elapsed;
        });

        await holderTask.WaitAsync(LockWait);
        var waited = await portalDeclineTask.WaitAsync(LockWait);

        Assert.True(waited >= TimeSpan.FromMilliseconds(300),
            $"Portal Decline returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the item lock.");

        // The real Email Accept, now that the lock has been released and the item already DECLINED.
        // A Portal response retires every pending emailed link for the item (BUG-05) via
        // EmailTokenInvalidationHelper — a distinct mechanism from Email's own sibling-burn — so the
        // sibling Accept token's own ResultStatus is already INVALID by the time this runs, not
        // AlreadyResponded (that status is reserved for a token Email itself burned as a sibling).
        using (var scope = _factory.Services.CreateScope())
        {
            var handler = EmailHandler(scope);
            var acceptResult = await handler.Handle(
                new ExecuteEmailActionCommand(acceptRaw, "127.0.0.1", "xunit", null), CancellationToken.None);
            Assert.Equal(EmailActionViewStatuses.Invalid, acceptResult.Status);
        }

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await checkDb.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == _logisticsItemId);
        Assert.Equal(LogisticsItemStatus.Declined, item.Status);
    }

    /// <summary>Same-token double-submit: two concurrent POSTs of the SAME raw decline link (a
    /// double-click). Only one may succeed; the loser must see AlreadyResponded, never a second mutation.</summary>
    [Fact]
    public async Task Double_clicking_the_same_decline_link_settles_on_exactly_one_outcome()
    {
        string declineRaw;
        using (var scope = _factory.Services.CreateScope())
            (_, declineRaw) = await AssignAndCaptureTokensAsync(scope);

        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = TimeSpan.FromMilliseconds(600);
        var holderTask = Task.Run(() => HoldLogisticsItemLockAsync(holding, hold));

        await holding.Task;

        var first = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            return await EmailHandler(scope).Handle(
                new ExecuteEmailActionCommand(declineRaw, "127.0.0.1", "xunit-1", "Không sắp xếp được lịch."),
                CancellationToken.None);
        });
        var second = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            return await EmailHandler(scope).Handle(
                new ExecuteEmailActionCommand(declineRaw, "127.0.0.1", "xunit-2", "Không sắp xếp được lịch."),
                CancellationToken.None);
        });

        await holderTask.WaitAsync(LockWait);
        var results = await Task.WhenAll(first, second).WaitAsync(LockWait);

        Assert.Single(results, r => r.Status == EmailActionViewStatuses.Success);
        Assert.Single(results, r => r.Status == EmailActionViewStatuses.AlreadyResponded);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await checkDb.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == _logisticsItemId);
        Assert.Equal(LogisticsItemStatus.Declined, item.Status);
        var attempts = await checkDb.VisitLogisticsAssignmentAttempts.AsNoTracking()
            .Where(a => a.LogisticsItemId == _logisticsItemId && a.Status == "DECLINED").CountAsync();
        Assert.Equal(1, attempts); // exactly one response recorded, not two
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
        public string? Email => "it-logi-assignee-conc-leader@pems.test";
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
        public string? Email => "it-logi-assignee-conc-staff@pems.test";
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
