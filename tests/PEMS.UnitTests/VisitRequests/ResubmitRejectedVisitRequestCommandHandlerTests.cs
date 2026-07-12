using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

public class ResubmitRejectedVisitRequestCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IDateTimeService> _clockMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly ResubmitRejectedVisitRequestCommandHandler _handler;

    private readonly ulong _visitorId = 100;
    private readonly ulong _visitRequestId = 10;
    private readonly DateTime _now = new DateTime(2026, 1, 1, 10, 0, 0);

    public ResubmitRejectedVisitRequestCommandHandlerTests()
    {
        _dbMock = new Mock<IApplicationDbContext>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _clockMock = new Mock<IDateTimeService>();
        _notificationMock = new Mock<INotificationService>();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(_visitorId);
        _currentUserMock.Setup(x => x.RoleCode).Returns(RoleCodes.Visitor);

        _clockMock.Setup(x => x.UtcNow).Returns(_now);
        _clockMock.Setup(x => x.VietnamNow).Returns(_now.AddHours(7));

        _handler = new ResubmitRejectedVisitRequestCommandHandler(
            _dbMock.Object, _currentUserMock.Object, _clockMock.Object, _notificationMock.Object);
    }

    private void SetupDatabase(VisitRequest visit)
    {
        var visits = new List<VisitRequest> { visit }.AsQueryable().BuildMockDbSet();
        _dbMock.Setup(x => x.VisitRequests).Returns(visits.Object);

        var campuses = new List<Campus>
        {
            new() { CampusId = 1, CampusCode = "HN", Status = "ACTIVE" }
        }.AsQueryable().BuildMockDbSet();
        _dbMock.Setup(x => x.Campuses).Returns(campuses.Object);

        var departments = new List<PEMS.Domain.Entities.Departments.Department>
        {
            new() { DepartmentId = 1, CampusId = 1, DepartmentType = "IC", Status = "ACTIVE" }
        }.AsQueryable().BuildMockDbSet();
        _dbMock.Setup(x => x.Departments).Returns(departments.Object);

        var role = new PEMS.Domain.Entities.Users.Role { RoleCode = RoleCodes.Staff };
        var users = new List<PEMS.Domain.Entities.Users.User>
        {
            new() { UserId = 200, PrimaryCampusId = 1, DepartmentId = 1, Status = "ACTIVE", SubRole = "LEADER", Role = role }
        }.AsQueryable().BuildMockDbSet();
        _dbMock.Setup(x => x.Users).Returns(users.Object);

        var guestMembers = new List<VisitGuestMember>().AsQueryable().BuildMockDbSet();
        _dbMock.Setup(x => x.VisitGuestMembers).Returns(guestMembers.Object);

        var requestCampuses = new List<VisitRequestCampus>().AsQueryable().BuildMockDbSet();
        _dbMock.Setup(x => x.VisitRequestCampuses).Returns(requestCampuses.Object);

        var auditLogs = new List<PEMS.Domain.Entities.Users.AuditLog>().AsQueryable().BuildMockDbSet();
        _dbMock.Setup(x => x.AuditLogs).Returns(auditLogs.Object);

        var transactionMock = new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
        _dbMock.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionMock.Object);
    }

    private VisitRequest CreateValidVisitRequest()
    {
        return new VisitRequest
        {
            VisitRequestId = _visitRequestId,
            VisitorUserId = _visitorId,
            Status = VisitRequestStatuses.Rejected,
            RegistrantFullName = "Old Registrant",
            RegistrantEmail = "old.registrant@example.com",
            ContactPersonEmail = "old.contact@example.com",
            ResubmissionCount = 0,
            CampusInstances = new List<VisitRequestCampus>
            {
                new() { VisitInstanceId = 1, CampusId = 1, Status = VisitInstanceStatuses.Rejected, PlannedStartAt = _now.AddDays(5) }
            },
            GuestMembers = new List<VisitGuestMember>()
        };
    }

    private ResubmitRejectedVisitRequestCommand CreateValidCommand()
    {
        return new ResubmitRejectedVisitRequestCommand(
            RegistrantFullName: "New Registrant", // Tampered
            RegistrantNationality: "VN",
            RegistrantOrganization: "FPT",
            RegistrantPosition: "Staff",
            RegistrantPhone: "0999999999",
            RegistrantEmail: "new.registrant@example.com", // Tampered
            DelegationName: "Test Delegation",
            VisitScope: "SINGLE_CAMPUS",
            VisitType: "CAMPUS_TOUR",
            VisitTypeOther: null,
            CampusVisits: new List<PEMS.Application.Common.DTOs.VisitSlotDto>
            {
                new("HN", _now.AddDays(5), _now.AddDays(5).AddHours(4))
            },
            Purpose: "Test Purpose",
            WorkingContent: "Test Content",
            Visitors: new List<PEMS.Application.Common.DTOs.VisitorDto>(),
            SupportMembers: new List<PEMS.Application.Common.DTOs.SupportTeamMemberDto>(),
            ContactPerson: new PEMS.Application.Common.DTOs.ContactPointDto("New Contact Name", "New Contact Org", "0888888888", "new.contact@example.com"), // Email tampered
            IsContactSelf: true, // Tampered relation
            WorkingLanguage: "VI",
            TransportationNote: null,
            MediaConsentStatus: "DECLINED",
            MediaConsentNote: null,
            PartnerId: null,
            Notes: null
        ) { VisitRequestId = _visitRequestId };
    }

    [Fact]
    public async Task ResubmitRejected_UpdatesAllowedFields_And_RejectsIdentityChanges()
    {
        var visit = CreateValidVisitRequest();
        SetupDatabase(visit);
        var command = CreateValidCommand();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _handler.Handle(command, CancellationToken.None));
        
        Assert.Contains("Thông tin người đăng ký không được phép thay đổi", ex.Message);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResubmitRejected_TamperingContactEmail_ThrowsException()
    {
        var visit = CreateValidVisitRequest();
        SetupDatabase(visit);
        var command = CreateValidCommand() with
        {
            RegistrantFullName = "Old Registrant",
            RegistrantEmail = "old.registrant@example.com",
            RegistrantNationality = visit.RegistrantNationality,
            RegistrantOrganization = visit.RegistrantOrganization,
            RegistrantPosition = visit.RegistrantJobTitle,
            RegistrantPhone = visit.RegistrantPhone,
            ContactPerson = new PEMS.Application.Common.DTOs.ContactPointDto("New Contact Name", "New Contact Org", "0888888888", "new.contact@example.com") // Tampered Email
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Không được phép thay đổi email của đầu mối", ex.Message);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResubmitRejected_TamperingIsContactSelf_ThrowsException()
    {
        var visit = CreateValidVisitRequest();
        SetupDatabase(visit);
        var command = CreateValidCommand() with
        {
            RegistrantFullName = "Old Registrant",
            RegistrantEmail = "old.registrant@example.com",
            RegistrantNationality = visit.RegistrantNationality,
            RegistrantOrganization = visit.RegistrantOrganization,
            RegistrantPosition = visit.RegistrantJobTitle,
            RegistrantPhone = visit.RegistrantPhone,
            ContactPerson = new PEMS.Application.Common.DTOs.ContactPointDto("New Contact Name", "New Contact Org", "0888888888", "old.contact@example.com"),
            IsContactSelf = true // Tampered from false -> true
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Không được phép thay đổi email của đầu mối", ex.Message);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResubmitRejected_ValidUpdate_Succeeds_And_PreservesAgenda_And_BumpsCounter()
    {
        var visit = CreateValidVisitRequest();
        SetupDatabase(visit);
        var command = CreateValidCommand() with
        {
            RegistrantFullName = "Old Registrant",
            RegistrantEmail = "old.registrant@example.com",
            RegistrantNationality = visit.RegistrantNationality,
            RegistrantOrganization = visit.RegistrantOrganization,
            RegistrantPosition = visit.RegistrantJobTitle,
            RegistrantPhone = visit.RegistrantPhone,
            ContactPerson = new PEMS.Application.Common.DTOs.ContactPointDto("New Contact Name", "New Contact Org", "0888888888", "old.contact@example.com"),
            IsContactSelf = false
        };

        await _handler.Handle(command, CancellationToken.None);

        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());

        Assert.Equal("New Contact Name", visit.ContactPersonFullName);
        Assert.Equal("Old Registrant", visit.RegistrantFullName);
        Assert.Equal(1u, visit.ResubmissionCount);
        Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
    }
}
