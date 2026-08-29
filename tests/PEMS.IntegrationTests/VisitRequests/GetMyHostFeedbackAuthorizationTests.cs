using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Feedbacks.Common;
using PEMS.Application.Feedbacks.Queries.GetMyHostFeedback;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Feedbacks;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// GetMyHostFeedbackQueryHandler used to return the full visit/delegation/host/schedule envelope for
/// ANY authenticated caller who could guess a visitInstanceId - the Feedbacks list itself was scoped to
/// TargetUserId == caller, but nothing gated the surrounding metadata. Its sibling
/// GetVisitorFeedbackQueryHandler, on the same feature, already re-derives the caller's relation before
/// returning anything; this module's fix reuses VisitInstanceAccess (the same primitive
/// GetVisitInstanceParticipantsQueryHandler already uses) instead of inventing new authorization logic.
/// </summary>
public sealed class GetMyHostFeedbackAuthorizationTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-MYHOSTFEEDBACK-AUTHZ] ";

    private readonly PemsWebApplicationFactory _factory;
    private ulong _campusId;
    private ulong _icDepartmentId;
    private ulong _hostUserId;
    private ulong _ratedParticipantUserId;
    private ulong _outsiderUserId;
    private ulong _registrantUserId;
    private ulong _visitRequestId;
    private ulong _visitInstanceId;
    private ulong _feedbackId;

    public GetMyHostFeedbackAuthorizationTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var icDept = await db.Departments.AsNoTracking()
            .Where(d => d.DepartmentType == "IC" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).FirstAsync();
        _campusId = icDept.CampusId;
        _icDepartmentId = icDept.DepartmentId;

        var staffRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Staff).Select(r => r.RoleId).FirstAsync();
        var studentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Student).Select(r => r.RoleId).FirstAsync();
        var visitorRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Visitor).Select(r => r.RoleId).FirstAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var host = new User
        {
            FullName = $"{TestPrefix}Host",
            Email = $"host_{suffix}@pems.test",
            RoleId = staffRoleId,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = _campusId,
            DepartmentId = _icDepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var rated = new User
        {
            FullName = $"{TestPrefix}RatedParticipant",
            Email = $"rated_{suffix}@pems.test",
            RoleId = studentRoleId,
            PrimaryCampusId = _campusId,
            StudentCode = $"ITMHF{DateTime.Now:HHmmssfff}",
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var outsider = new User
        {
            FullName = $"{TestPrefix}Outsider",
            Email = $"outsider_{suffix}@pems.test",
            RoleId = studentRoleId,
            PrimaryCampusId = _campusId,
            StudentCode = $"ITMHF{DateTime.Now:HHmmssff}O",
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
        db.Users.AddRange(host, rated, outsider, registrant);
        await db.SaveChangesAsync();
        _hostUserId = host.UserId;
        _ratedParticipantUserId = rated.UserId;
        _outsiderUserId = outsider.UserId;
        _registrantUserId = registrant.UserId;

        var visit = new VisitRequest
        {
            RequestCode = $"IT-MHF-{Guid.NewGuid().ToString("N")[..8]}",
            RegistrantUserId = registrant.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = registrant.Email,
            VisitScope = VisitScopes.SingleCampus,
            Status = VisitRequestStatuses.Approved,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync();
        _visitRequestId = visit.VisitRequestId;

        // Created BEFORE_VISIT first (no agenda/CLOSED constraints yet), then moved forward once the
        // agenda exists - trg_visit_campuses_status_validate_bu refuses DURING_VISIT/AFTER_VISIT/CLOSED
        // with no agenda row, and that row can only be added once the instance itself exists.
        var instance = new VisitRequestCampus
        {
            VisitRequestId = _visitRequestId,
            CampusId = _campusId,
            CurrentHostUserId = _hostUserId,
            OperationalContactUserId = registrant.UserId,
            PlannedStartAt = DateTime.Now.AddDays(-3),
            PlannedEndAt = DateTime.Now.AddDays(-3).AddHours(2),
            Status = VisitInstanceStatuses.BeforeVisit,
            // The full host-decision metadata block, not just CurrentHostUserId -
            // trg_visit_campuses_assignment_validate_bu refuses otherwise.
            HostAssignedBy = _hostUserId,
            HostAssignedAt = DateTime.Now.AddDays(-4),
            DecidedBy = _hostUserId,
            DecidedAt = DateTime.Now.AddDays(-4),
            DecisionActorRole = "STAFF_LEADER",
            DecisionSource = "STANDARD_CAMPUS_REVIEW",
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();
        _visitInstanceId = instance.VisitInstanceId;

        db.VisitAgendas.Add(new VisitAgenda
        {
            VisitInstanceId = _visitInstanceId,
            Title = "Tiếp đón đoàn",
            StartTime = instance.PlannedStartAt,
            EndTime = instance.PlannedEndAt,
            SequenceOrder = 1,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        instance.Status = VisitInstanceStatuses.Closed;
        await db.SaveChangesAsync();

        // The rated user's real relation to the instance: an accepted, non-host support participant -
        // exactly the population VisitInstanceAccess/CanViewInternal grants access to.
        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = _visitInstanceId,
            UserId = _ratedParticipantUserId,
            ParticipantRole = ParticipantRoles.Student,
            IsHost = false,
            Status = ParticipantStatuses.Accepted,
            InvitedAt = DateTime.Now.AddDays(-5),
            RespondedAt = DateTime.Now.AddDays(-5),
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        var feedback = new Feedback
        {
            VisitRequestId = _visitRequestId,
            VisitInstanceId = _visitInstanceId,
            FeedbackType = FeedbackTypes.HostParticipant,
            SubmittedByUserId = _hostUserId,
            SubmitterRole = FeedbackSubmitterRoles.Host,
            SubmitterNameSnapshot = host.FullName,
            TargetType = FeedbackTargetTypes.User,
            TargetUserId = _ratedParticipantUserId,
            TargetNameSnapshot = rated.FullName,
            Rating = 5,
            Comment = "Hỗ trợ rất tốt.",
            SubmittedAt = DateTime.Now.AddDays(-2),
        };
        db.Feedbacks.Add(feedback);
        await db.SaveChangesAsync();
        _feedbackId = feedback.FeedbackId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await FixtureCleanup.For(db)
            .Root("visit_requests", $"visit_request_id = {_visitRequestId}")
            .Root("users", $"user_id IN ({_hostUserId}, {_ratedParticipantUserId}, {_outsiderUserId}, {_registrantUserId})")
            .RunAsync();
    }

    private GetMyHostFeedbackQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user);

    private FakeCurrentUser AsUser(ulong userId) => new()
    {
        UserId = userId,
        RoleCode = RoleCodes.Student,
        PrimaryCampusId = _campusId,
    };

    /// <summary>The rated participant can see their own feedback and the visit metadata around it.</summary>
    [Fact]
    public async Task RatedParticipant_CanViewTheirOwnFeedback()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var response = await Handler(db, AsUser(_ratedParticipantUserId))
            .Handle(new GetMyHostFeedbackQuery(_visitInstanceId), CancellationToken.None);

        Assert.Equal(_visitInstanceId, response.VisitInstanceId);
        var item = Assert.Single(response.Feedbacks);
        Assert.Equal(_feedbackId, item.FeedbackId);
        Assert.Equal(5, item.Rating);
    }

    /// <summary>The hole this closes: a user with no relation to the instance could read its delegation
    /// name, organization, campus, host and schedule by guessing the instance id.</summary>
    [Fact]
    public async Task OutsiderWithNoRelationToTheInstance_IsForbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Handler(db, AsUser(_outsiderUserId))
                .Handle(new GetMyHostFeedbackQuery(_visitInstanceId), CancellationToken.None));
    }

    /// <summary>The host themself (who submitted the feedback, not the target of it) can still open the
    /// screen - they have a real relation to the instance, and correctly sees an empty list since no
    /// feedback targets them.</summary>
    [Fact]
    public async Task Host_CanOpenTheScreen_ButSeesNoFeedbackTargetingThemself()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var response = await Handler(db, AsUser(_hostUserId))
            .Handle(new GetMyHostFeedbackQuery(_visitInstanceId), CancellationToken.None);

        Assert.Equal(_visitInstanceId, response.VisitInstanceId);
        Assert.Empty(response.Feedbacks);
    }

    [Fact]
    public async Task Unauthenticated_IsForbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var anon = new FakeCurrentUser { IsAuthenticated = false };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Handler(db, anon).Handle(new GetMyHostFeedbackQuery(_visitInstanceId), CancellationToken.None));
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated { get; set; } = true;
        public ulong? UserId { get; set; }
        public string? Email { get; set; }
        public ulong? RoleId { get; set; }
        public string? RoleCode { get; set; }
        public string? SubRole { get; set; }
        public ulong? PrimaryCampusId { get; set; }
        public ulong? DepartmentId { get; set; }
        public ulong? SessionId { get; set; }
        public string? LoginPortal { get; set; }
    }
}
