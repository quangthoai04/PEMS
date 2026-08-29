using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetInvitationDetail;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.DepartmentReceptionTasks;

/// <summary>
/// DB-AUTHZ-002: <see cref="GetInvitationDetailQueryHandler"/> had <see cref="ICurrentUserService"/>
/// injected but never read it, so any authenticated user of any department could read a colleague's
/// invitation detail (sender identity, delegation content, operational-contact fields) by guessing the
/// participant id. The fix mirrors this module's own list
/// (GetAssignmentsProgressListQueryHandler, ItemType=INVITATION): the invitee may always view their own
/// invitation (same ownership rule VisitInvitationResponse.ApplyCoreAsync already applies to
/// accept/decline), and a Department Leader additionally gets oversight of every invitation addressed to
/// their own department's roster; a Department Staff member gets no such extra grant.
/// </summary>
public sealed class InvitationDetailAuthorizationTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-INVITATION-DETAIL-AUTHZ] ";

    private readonly PemsWebApplicationFactory _factory;
    private ulong _campusId;
    private ulong _departmentAId;
    private ulong _departmentBId;
    private ulong _deptALeaderUserId;
    private ulong _deptAStaffUserId;
    private ulong _deptAOtherStaffUserId;
    private ulong _deptBLeaderUserId;

    public InvitationDetailAuthorizationTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var departmentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department).Select(r => r.RoleId).FirstAsync();

        _campusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active)
            .OrderBy(c => c.CampusId).Select(c => c.CampusId).FirstAsync();

        var deptIds = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == _campusId && d.DepartmentType == "GENERAL" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).Select(d => d.DepartmentId).Take(2).ToListAsync();
        Assert.True(deptIds.Count >= 2, "Test data needs at least 2 GENERAL departments on the same campus.");
        _departmentAId = deptIds[0];
        _departmentBId = deptIds[1];

        var suffix = Guid.NewGuid().ToString("N")[..8];
        User Make(string name, string mail, string subRole, ulong departmentId) => new()
        {
            FullName = $"{TestPrefix}{name}",
            Email = $"{mail}_{suffix}@pems.test",
            RoleId = departmentRoleId,
            SubRole = subRole,
            PrimaryCampusId = _campusId,
            DepartmentId = departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };

        var deptALeader = Make("DeptALeader", "dalead", UserSubRoles.Leader, _departmentAId);
        var deptAStaff = Make("DeptAStaff", "dastaff", UserSubRoles.Staff, _departmentAId);
        var deptAOtherStaff = Make("DeptAOtherStaff", "daother", UserSubRoles.Staff, _departmentAId);
        var deptBLeader = Make("DeptBLeader", "dblead", UserSubRoles.Leader, _departmentBId);
        db.Users.AddRange(deptALeader, deptAStaff, deptAOtherStaff, deptBLeader);
        await db.SaveChangesAsync();

        _deptALeaderUserId = deptALeader.UserId;
        _deptAStaffUserId = deptAStaff.UserId;
        _deptAOtherStaffUserId = deptAOtherStaff.UserId;
        _deptBLeaderUserId = deptBLeader.UserId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ulong> InviteAsync(ApplicationDbContext db, ulong userId)
    {
        var visit = new VisitRequest
        {
            RequestCode = $"VR-{DateTime.Now.Ticks}-{Guid.NewGuid().ToString("N")[..4]}",
            Status = VisitRequestStatuses.PendingApproval,
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

        var instance = new VisitRequestCampus
        {
            VisitRequestId = visit.VisitRequestId,
            CampusId = _campusId,
            OperationalContactUserId = _deptALeaderUserId,
            PlannedStartAt = DateTime.Now.AddDays(5),
            PlannedEndAt = DateTime.Now.AddDays(5).AddHours(2),
            Status = VisitInstanceStatuses.WaitingRequestApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();

        // Pure V2: GetInvitationDetail resolves content through IVisitFormReadService, which requires
        // exactly one detail row per instance.
        db.VisitInstanceFormDetails.Add(new VisitInstanceFormDetail
        {
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = "Đoàn khách test",
            VisitType = "MEETING",
            Purpose = "Purpose",
            WorkingContent = "Content",
            OperationalContactFullName = "Contact",
            OperationalContactJobTitle = "Job",
            OperationalContactEmail = "contact@pems.test",
            WorkingLanguage = "EN",
            MediaConsentStatus = "AGREED",
            FormRevision = 1,
            ApprovalRevision = 1,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        var p = new VisitParticipant
        {
            VisitInstanceId = instance.VisitInstanceId,
            UserId = userId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            IsHost = false,
            Status = ParticipantStatuses.Invited,
            InvitedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        db.VisitParticipants.Add(p);
        await db.SaveChangesAsync();

        return p.ParticipantId;
    }

    private FakeCurrentUser AsUser(ulong userId, ulong departmentId, string subRole) => new()
    {
        UserId = userId,
        RoleCode = RoleCodes.Department,
        SubRole = subRole,
        PrimaryCampusId = _campusId,
        DepartmentId = departmentId,
    };

    private GetInvitationDetailQueryHandler Handler(ApplicationDbContext db, IServiceScope scope, ICurrentUserService user)
        => new(db, user, scope.ServiceProvider.GetRequiredService<IVisitFormReadService>());

    [Fact]
    public async Task Invitee_CanViewOwnInvitation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participantId = await InviteAsync(db, _deptAStaffUserId);

        var dto = await Handler(db, scope, AsUser(_deptAStaffUserId, _departmentAId, UserSubRoles.Staff))
            .Handle(new GetInvitationDetailQuery { ParticipantId = participantId }, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(participantId, dto.ParticipantId);
    }

    /// <summary>Oversight: a Department Leader can see every invitation addressed to their own
    /// department, same visibility the assignments-progress list already grants them.</summary>
    [Fact]
    public async Task Leader_CanViewDepartmentColleaguesInvitation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participantId = await InviteAsync(db, _deptAStaffUserId);

        var dto = await Handler(db, scope, AsUser(_deptALeaderUserId, _departmentAId, UserSubRoles.Leader))
            .Handle(new GetInvitationDetailQuery { ParticipantId = participantId }, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(participantId, dto.ParticipantId);
    }

    /// <summary>The hole this closes: a plain Staff member could read a colleague's invitation detail
    /// (sender identity, delegation content, operational-contact fields) by guessing the participant id
    /// — something even that colleague's own Leader is the only other party meant to see.</summary>
    [Fact]
    public async Task Staff_CannotViewColleaguesInvitation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participantId = await InviteAsync(db, _deptAStaffUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Handler(db, scope, AsUser(_deptAOtherStaffUserId, _departmentAId, UserSubRoles.Staff))
                .Handle(new GetInvitationDetailQuery { ParticipantId = participantId }, CancellationToken.None));
    }

    [Fact]
    public async Task CrossDepartment_IsForbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participantId = await InviteAsync(db, _deptAStaffUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Handler(db, scope, AsUser(_deptBLeaderUserId, _departmentBId, UserSubRoles.Leader))
                .Handle(new GetInvitationDetailQuery { ParticipantId = participantId }, CancellationToken.None));
    }

    [Fact]
    public async Task Unauthenticated_IsForbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participantId = await InviteAsync(db, _deptAStaffUserId);

        var anon = new FakeCurrentUser { IsAuthenticated = false };
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Handler(db, scope, anon)
                .Handle(new GetInvitationDetailQuery { ParticipantId = participantId }, CancellationToken.None));
    }
}
