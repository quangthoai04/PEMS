using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentReceptionTasks.Commands.AcceptInvitation;
using PEMS.Application.DepartmentReceptionTasks.Commands.DeclineInvitation;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.DepartmentReceptionTasks;

/// <summary>
/// One business action, one set of rules — asked of the DEPARTMENT entry point, which is the one that
/// had none.
///
/// <para>
/// <c>DepartmentReceptionTasks.AcceptInvitation</c> / <c>DeclineInvitation</c> used to load the
/// participant by id and write the new status, with both status guards commented out and no ownership
/// check at all. So the identical click obeyed different rules depending on which screen it was on:
/// from the delegations screen a user could only answer their own pending invitation on a live visit;
/// from the department screens anyone could answer anyone's, twice, on a cancelled visit. Every case
/// below goes through the department handlers on purpose — they are the ones that used to say yes.
/// </para>
/// </summary>
public sealed class InvitationResponseAuthorizationTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-INVITE-RESPOND] ";

    private readonly PemsWebApplicationFactory _factory;
    private ulong _campusId;
    private ulong _departmentId;
    private ulong _leaderUserId;
    private ulong _otherLeaderUserId;
    private ulong _contactUserId;
    /// <summary>An ACTIVE Staff Leader of the campus — the only account the DB triggers accept as the
    /// approver of a campus instance, and (self-hosting) as its Host.</summary>
    private ulong _staffLeaderUserId;

    public InvitationResponseAuthorizationTests(PemsWebApplicationFactory factory) => _factory = factory;

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

        _staffLeaderUserId = await (from u in db.Users.AsNoTracking()
                                    join r in db.Roles.AsNoTracking() on u.RoleId equals r.RoleId
                                    where r.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader
                                          && u.PrimaryCampusId == _campusId && u.Status == UserStatuses.Active
                                    orderby u.UserId
                                    select u.UserId).FirstAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        User Make(string name, string mail) => new()
        {
            FullName = $"{TestPrefix}{name}",
            Email = $"{mail}_{suffix}@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = _campusId,
            DepartmentId = _departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };

        var invitee = Make("Invitee", "rlead");
        var outsider = Make("Outsider", "rout");
        var contact = Make("Contact", "rcontact");
        db.Users.AddRange(invitee, outsider, contact);
        await db.SaveChangesAsync();

        _leaderUserId = invitee.UserId;
        _otherLeaderUserId = outsider.UserId;
        _contactUserId = contact.UserId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Fixture ─────────────────────────────────────────────────────────────────────────────────

    private sealed class SilentNotifications : INotificationService
    {
        public Task CreateManyAsync(System.Collections.Generic.IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(System.Collections.Generic.IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong r, string t, string? m, string nt, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>A campus instance in the state an invitation actually lives in: the Host is preparing.</summary>
    private async Task<ulong> CreateInstanceAsync(
        ApplicationDbContext db, string instanceStatus = VisitInstanceStatuses.BeforeVisit,
        string requestStatus = VisitRequestStatuses.Approved)
    {
        var visit = new VisitRequest
        {
            RequestCode = $"VR-{DateTime.Now.Ticks}-{Guid.NewGuid().ToString("N")[..4]}",
            Status = requestStatus,
            RegistrantFullName = "Registrant",
            RegistrantEmail = "r@pems.test",
            RegistrantPhone = "0123",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantNationality = "VN",
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync();

        var cancelled = instanceStatus == VisitInstanceStatuses.Cancelled;
        var instance = new VisitRequestCampus
        {
            VisitRequestId = visit.VisitRequestId,
            CampusId = _campusId,
            // The campus instance triggers are strict about the shapes they admit, and rightly so —
            // a fixture that skips them would be testing a state production cannot reach. Past the
            // confirmation gate an instance must name its operational contact; approved/operational it
            // must carry a full host assignment and decision, whose actors must be a Staff Leader of
            // this campus. The simplest real shape is that Leader approving and self-hosting.
            OperationalContactUserId = cancelled ? null : _contactUserId,
            CurrentHostUserId = cancelled ? null : _staffLeaderUserId,
            HostAssignedBy = cancelled ? null : _staffLeaderUserId,
            HostAssignedAt = cancelled ? null : DateTime.Now,
            DecidedBy = cancelled ? null : _staffLeaderUserId,
            DecidedAt = cancelled ? null : DateTime.Now,
            DecisionActorRole = cancelled ? null : "STAFF_LEADER",
            DecisionSource = cancelled ? null : "STANDARD_CAMPUS_REVIEW",
            PlannedStartAt = DateTime.Now.AddDays(5),
            PlannedEndAt = DateTime.Now.AddDays(5).AddHours(2),
            Status = cancelled ? instanceStatus : VisitInstanceStatuses.BeforeVisit,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();

        // Anything past BEFORE_VISIT also needs a real agenda (a visit that happened has a programme),
        // so it is reached by moving the row forward rather than by inventing the end state.
        if (!cancelled && instanceStatus != VisitInstanceStatuses.BeforeVisit)
        {
            db.VisitAgendas.Add(new VisitAgenda
            {
                VisitInstanceId = instance.VisitInstanceId,
                Title = "Tiếp đón đoàn",
                StartTime = instance.PlannedStartAt,
                EndTime = instance.PlannedEndAt,
                SequenceOrder = 1,
                CreatedAt = DateTime.Now,
            });
            await db.SaveChangesAsync();

            instance.Status = instanceStatus;
            await db.SaveChangesAsync();
        }

        return instance.VisitInstanceId;
    }

    private async Task<VisitParticipant> InviteAsync(
        ApplicationDbContext db, ulong instanceId, ulong userId, string status = ParticipantStatuses.Invited)
    {
        var p = new VisitParticipant
        {
            VisitInstanceId = instanceId,
            UserId = userId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            IsHost = false,
            Status = status,
            InvitedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        db.VisitParticipants.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    private AcceptInvitationCommandHandler Accepting(ApplicationDbContext db, ulong actorId)
        => new(db, Actor(actorId), new SilentNotifications(), new MySqlUserMutationLockService(db));

    private DeclineInvitationCommandHandler Declining(ApplicationDbContext db, ulong actorId)
        => new(db, Actor(actorId), new SilentNotifications(), new MySqlUserMutationLockService(db));

    private FakeCurrentUser Actor(ulong userId) => new()
    {
        UserId = userId,
        RoleCode = RoleCodes.Department,
        SubRole = UserSubRoles.Leader,
        PrimaryCampusId = _campusId,
        DepartmentId = _departmentId,
    };

    private async Task<string> StatusOfAsync(ulong participantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.VisitParticipants.AsNoTracking()
            .Where(p => p.ParticipantId == participantId).Select(p => p.Status).SingleAsync();
    }

    // ── The invitee's own answer still works ────────────────────────────────────────────────────

    [Fact]
    public async Task The_invitee_may_accept_their_own_invitation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instanceId = await CreateInstanceAsync(db);
        var p = await InviteAsync(db, instanceId, _leaderUserId);

        await Accepting(db, _leaderUserId).Handle(
            new AcceptInvitationCommand { ParticipantId = p.ParticipantId }, CancellationToken.None);

        Assert.Equal(ParticipantStatuses.Accepted, await StatusOfAsync(p.ParticipantId));
    }

    [Fact]
    public async Task The_invitee_may_decline_their_own_invitation_with_a_reason()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instanceId = await CreateInstanceAsync(db);
        var p = await InviteAsync(db, instanceId, _leaderUserId);

        await Declining(db, _leaderUserId).Handle(
            new DeclineInvitationCommand { ParticipantId = p.ParticipantId, Reason = "Trùng lịch họp giao ban" },
            CancellationToken.None);

        Assert.Equal(ParticipantStatuses.Declined, await StatusOfAsync(p.ParticipantId));
    }

    // ── …and nobody else's ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The hole this closes: the department handler read the participant id straight off the route and
    /// never asked whose invitation it was, so one click with a colleague's id answered on their behalf.
    /// </summary>
    [Fact]
    public async Task Answering_somebody_else_s_invitation_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instanceId = await CreateInstanceAsync(db);
        var p = await InviteAsync(db, instanceId, _leaderUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Accepting(db, _otherLeaderUserId).Handle(
                new AcceptInvitationCommand { ParticipantId = p.ParticipantId }, CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Declining(db, _otherLeaderUserId).Handle(
                new DeclineInvitationCommand { ParticipantId = p.ParticipantId, Reason = "Không liên quan" },
                CancellationToken.None));

        // Refused means UNCHANGED, not "refused but written anyway".
        Assert.Equal(ParticipantStatuses.Invited, await StatusOfAsync(p.ParticipantId));
    }

    // ── An answered invitation is final ─────────────────────────────────────────────────────────

    [Fact]
    public async Task An_already_declined_invitation_cannot_be_flipped_to_accepted()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instanceId = await CreateInstanceAsync(db);
        var p = await InviteAsync(db, instanceId, _leaderUserId, ParticipantStatuses.Declined);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Accepting(db, _leaderUserId).Handle(
                new AcceptInvitationCommand { ParticipantId = p.ParticipantId }, CancellationToken.None));

        Assert.Equal(ParticipantStatuses.Declined, await StatusOfAsync(p.ParticipantId));
    }

    [Fact]
    public async Task An_already_accepted_invitation_cannot_be_accepted_again()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instanceId = await CreateInstanceAsync(db);
        var p = await InviteAsync(db, instanceId, _leaderUserId, ParticipantStatuses.Accepted);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Accepting(db, _leaderUserId).Handle(
                new AcceptInvitationCommand { ParticipantId = p.ParticipantId }, CancellationToken.None));
    }

    // ── A visit that is over, cancelled or not yet in preparation admits no answer ───────────────

    [Fact]
    public async Task A_cancelled_visit_cannot_be_joined()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instanceId = await CreateInstanceAsync(
            db, VisitInstanceStatuses.Cancelled, VisitRequestStatuses.Cancelled);
        var p = await InviteAsync(db, instanceId, _leaderUserId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Accepting(db, _leaderUserId).Handle(
                new AcceptInvitationCommand { ParticipantId = p.ParticipantId }, CancellationToken.None));

        Assert.Equal(ParticipantStatuses.Invited, await StatusOfAsync(p.ParticipantId));
    }

    /// <summary>
    /// The same fact the assignments list reports as EXPIRED rather than "Hoàn thành": once the visit is
    /// over, answering its invitation means nothing. The list and the endpoint have to agree, or the
    /// screen offers a button the API refuses.
    /// </summary>
    [Fact]
    public async Task A_finished_visit_cannot_be_joined()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instanceId = await CreateInstanceAsync(db, VisitInstanceStatuses.AfterVisit);
        var p = await InviteAsync(db, instanceId, _leaderUserId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Accepting(db, _leaderUserId).Handle(
                new AcceptInvitationCommand { ParticipantId = p.ParticipantId }, CancellationToken.None));
    }

    // ── A decline still needs a reason, whichever screen it came from ────────────────────────────

    [Fact]
    public async Task Declining_without_a_reason_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instanceId = await CreateInstanceAsync(db);
        var p = await InviteAsync(db, instanceId, _leaderUserId);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Declining(db, _leaderUserId).Handle(
                new DeclineInvitationCommand { ParticipantId = p.ParticipantId, Reason = "   " },
                CancellationToken.None));

        Assert.Equal(ParticipantStatuses.Invited, await StatusOfAsync(p.ParticipantId));
    }
}
