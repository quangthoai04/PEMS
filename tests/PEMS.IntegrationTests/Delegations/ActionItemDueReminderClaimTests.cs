using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Minutes;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.BackgroundJobs;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Delegations;

/// <summary>
/// DB-TXN-003: <see cref="ActionItemDueReminderHostedService"/> used to stamp
/// <c>due_reminder_sent_at</c> only AFTER the email attempt succeeded, so the row stayed looking
/// unclaimed to any other process for the whole dispatch. Two overlapping instances of this job (a
/// normal outcome of horizontal scaling) could both pick the same due item and both send it.
///
/// <para>
/// A green unit suite cannot see this: EF InMemory has no row-level atomicity to lose. This drives two
/// genuinely concurrent MySQL-backed ticks at the same item.
/// </para>
/// </summary>
public sealed class ActionItemDueReminderClaimTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-ACTIONITEM-REMINDER] ";
    private static readonly TimeSpan RaceWait = TimeSpan.FromSeconds(15);

    private readonly PemsWebApplicationFactory _factory;
    private ulong _campusId;
    private ulong _assigneeUserId;
    private ulong _registrantUserId;
    private ulong _visitRequestId;
    private ulong _visitInstanceId;
    private ulong _minutesId;

    public ActionItemDueReminderClaimTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _campusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active).OrderBy(c => c.CampusId)
            .Select(c => c.CampusId).FirstAsync();

        var studentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Student).Select(r => r.RoleId).FirstAsync();
        var visitorRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Visitor).Select(r => r.RoleId).FirstAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var assignee = new User
        {
            FullName = $"{TestPrefix}Assignee",
            Email = $"assignee_{suffix}@pems.test",
            RoleId = studentRoleId,
            PrimaryCampusId = _campusId,
            StudentCode = $"ITAR{DateTime.Now:HHmmssfff}",
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var registrant = new User
        {
            FullName = $"{TestPrefix}Registrant",
            Email = $"reg_{suffix}@pems.test",
            RoleId = visitorRoleId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.AddRange(assignee, registrant);
        await db.SaveChangesAsync();
        _assigneeUserId = assignee.UserId;
        _registrantUserId = registrant.UserId;

        var visit = new VisitRequest
        {
            RequestCode = $"IT-AR-{Guid.NewGuid().ToString("N")[..8]}",
            RegistrantUserId = registrant.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = registrant.Email,
            VisitScope = VisitScopes.SingleCampus,
            Status = VisitRequestStatuses.PendingApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync();
        _visitRequestId = visit.VisitRequestId;

        var instance = new VisitRequestCampus
        {
            VisitRequestId = _visitRequestId,
            CampusId = _campusId,
            OperationalContactUserId = registrant.UserId,
            PlannedStartAt = DateTime.Now.AddDays(-1),
            PlannedEndAt = DateTime.Now.AddDays(-1).AddHours(2),
            Status = VisitInstanceStatuses.WaitingRequestApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();
        _visitInstanceId = instance.VisitInstanceId;

        var minute = new Minute
        {
            VisitInstanceId = _visitInstanceId,
            Title = "Biên bản kiểm thử",
            Status = "SAVED",
            CreatedAt = DateTime.Now,
        };
        db.Minutes.Add(minute);
        await db.SaveChangesAsync();
        _minutesId = minute.MinutesId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await FixtureCleanup.For(db)
            .Root("visit_requests", $"visit_request_id = {_visitRequestId}")
            .Root("users", $"user_id IN ({_assigneeUserId}, {_registrantUserId})")
            .RunAsync();
    }

    private async Task<ulong> CreateDueActionItemAsync(ApplicationDbContext db)
    {
        var item = new MinuteActionItem
        {
            MinutesId = _minutesId,
            Title = $"{TestPrefix}Task {Guid.NewGuid():N}",
            AssignedToUserId = _assigneeUserId,
            DueDate = DateTime.Now.AddMinutes(-10),
            Status = "TODO",
            CreatedAt = DateTime.Now,
        };
        db.MinuteActionItems.Add(item);
        await db.SaveChangesAsync();
        return item.ActionItemId;
    }

    /// <summary>
    /// Two ticks (two connections, standing in for two horizontally-scaled instances of the same
    /// hosted service) both see the item as due and race to dispatch it. Exactly one email must go out
    /// and the row must end up claimed.
    /// </summary>
    [Fact]
    public async Task Two_concurrent_ticks_dispatch_the_same_due_item_exactly_once()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var itemId = await CreateDueActionItemAsync(db);

        var counter = new CountingEmailService();

        var tickA = Task.Run(() => RunOneTickAsync(counter));
        var tickB = Task.Run(() => RunOneTickAsync(counter));
        await Task.WhenAll(tickA, tickB).WaitAsync(RaceWait);

        Assert.Equal(1, counter.SendCount);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await checkDb.MinuteActionItems.AsNoTracking().FirstAsync(a => a.ActionItemId == itemId);
        Assert.NotNull(row.DueReminderSentAt);
    }

    /// <summary>The pre-existing retry contract is preserved: a failed send releases the claim so the
    /// next tick tries again, instead of the item being lost or permanently stuck.</summary>
    [Fact]
    public async Task A_failed_dispatch_releases_the_claim_for_a_later_retry()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var itemId = await CreateDueActionItemAsync(db);

        var failing = new CountingEmailService { ShouldFail = true };
        await RunOneTickAsync(failing);

        Assert.Equal(1, failing.SendCount);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await checkDb.MinuteActionItems.AsNoTracking().FirstAsync(a => a.ActionItemId == itemId);
        Assert.Null(row.DueReminderSentAt);

        // And it really is retried: a second tick with a working email service picks it right up.
        var working = new CountingEmailService();
        await RunOneTickAsync(working);
        Assert.Equal(1, working.SendCount);

        var rowAfterRetry = await checkDb.MinuteActionItems.AsNoTracking()
            .FirstAsync(a => a.ActionItemId == itemId);
        Assert.NotNull(rowAfterRetry.DueReminderSentAt);
    }

    /// <summary>
    /// The hosted service resolves its own scope internally (it is built to run standalone, waking up
    /// on a timer), so driving it here means handing it a scope FACTORY rather than a scope - one whose
    /// container is the real one from <see cref="_factory"/> with only <see cref="IEmailService"/>
    /// swapped for the given fake, so DbContext, notifications and clock stay real.
    /// </summary>
    private async Task RunOneTickAsync(IEmailService emailService)
    {
        var scopedFactory = new SingleScopeFactory(_factory.Services, emailService);
        var service = new ActionItemDueReminderHostedService(
            scopedFactory, NullLogger<ActionItemDueReminderHostedService>.Instance,
            new ConfigurationBuilder().Build());
        await service.DispatchDueRemindersAsync(CancellationToken.None);
    }

    /// <summary>Builds ONE scope per Get, backed by the real container but with <see cref="IEmailService"/>
    /// swapped for the given fake — every DI-resolved collaborator the hosted service uses (DbContext,
    /// notifications, clock) stays the real one from <paramref name="root"/>.</summary>
    private sealed class SingleScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _root;
        private readonly IEmailService _email;
        public SingleScopeFactory(IServiceProvider root, IEmailService email)
        {
            _root = root;
            _email = email;
        }
        public IServiceScope CreateScope() => new FakeEmailScope(_root.CreateScope(), _email);
    }

    private sealed class FakeEmailScope : IServiceScope
    {
        private readonly IServiceScope _inner;
        public FakeEmailScope(IServiceScope inner, IEmailService email)
        {
            _inner = inner;
            ServiceProvider = new OverrideProvider(_inner.ServiceProvider, email);
        }
        public IServiceProvider ServiceProvider { get; }
        public void Dispose() => _inner.Dispose();
    }

    private sealed class OverrideProvider : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly IEmailService _email;
        public OverrideProvider(IServiceProvider inner, IEmailService email)
        {
            _inner = inner;
            _email = email;
        }
        public object? GetService(Type serviceType)
            => serviceType == typeof(IEmailService) ? _email : _inner.GetService(serviceType);
    }

    private sealed class CountingEmailService : IEmailService
    {
        public bool ShouldFail;
        private int _sendCount;
        public int SendCount => _sendCount;

        public Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _sendCount);
            if (ShouldFail) throw new InvalidOperationException("Simulated SMTP failure.");
            return Task.CompletedTask;
        }
    }
}
