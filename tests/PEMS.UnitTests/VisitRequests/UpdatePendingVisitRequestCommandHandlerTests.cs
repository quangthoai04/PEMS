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
using PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

public class UpdatePendingVisitRequestCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IDateTimeService> _clockMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly UpdatePendingVisitRequestCommandHandler _handler;

    private readonly ulong _visitorId = 100;
    private readonly ulong _visitRequestId = 10;
    private readonly DateTime _now = new DateTime(2026, 1, 1, 10, 0, 0);

    public UpdatePendingVisitRequestCommandHandlerTests()
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

        _handler = new UpdatePendingVisitRequestCommandHandler(
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
            Status = VisitRequestStatuses.PendingApproval,
            RegistrantFullName = "Old Registrant",
            RegistrantEmail = "old.registrant@example.com",
            ContactPersonEmail = "old.contact@example.com",
            CampusInstances = new List<VisitRequestCampus>
            {
                new() { VisitInstanceId = 1, CampusId = 1, Status = VisitInstanceStatuses.WaitingRequestApproval, PlannedStartAt = _now.AddDays(5) }
            },
            GuestMembers = new List<VisitGuestMember>()
        };
    }

    private UpdatePendingVisitRequestCommand CreateValidCommand()
    {
        return new UpdatePendingVisitRequestCommand(
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
    public async Task UpdatePendingVisitRequest_UpdatesAllowedFields_And_RejectsIdentityChanges()
    {
        // Arrange
        var visit = CreateValidVisitRequest();
        SetupDatabase(visit);
        var command = CreateValidCommand();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _handler.Handle(command, CancellationToken.None));
        
        // Ensure exception is thrown due to Registrant Information tampering
        Assert.Contains("Thông tin người đăng ký không được phép thay đổi", ex.Message);
        
        // Verify DB was NOT saved
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePendingVisitRequest_TamperingContactEmail_ThrowsException()
    {
        // Arrange
        var visit = CreateValidVisitRequest();
        SetupDatabase(visit);
        var command = CreateValidCommand() with
        {
            RegistrantFullName = "Old Registrant",
            RegistrantEmail = "old.registrant@example.com",
            // Wait, we need to match all registrant fields to bypass Registrant Check
            RegistrantNationality = visit.RegistrantNationality,
            RegistrantOrganization = visit.RegistrantOrganization,
            RegistrantPosition = visit.RegistrantJobTitle,
            RegistrantPhone = visit.RegistrantPhone,
            ContactPerson = new PEMS.Application.Common.DTOs.ContactPointDto("New Contact Name", "New Contact Org", "0888888888", "new.contact@example.com") // Tampered Email
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Không được phép thay đổi email của đầu mối", ex.Message);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePendingVisitRequest_TamperingIsContactSelf_ThrowsException()
    {
        // Arrange
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

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Không được phép thay đổi email của đầu mối", ex.Message);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePendingVisitRequest_ValidUpdate_Succeeds_And_PreservesAgenda()
    {
        // Arrange
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

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Verify that contact name and org were updated
        Assert.Equal("New Contact Name", visit.ContactPersonFullName);
        Assert.Equal("New Contact Org", visit.ContactPersonOrganization);
        Assert.Equal("0888888888", visit.ContactPersonPhone);

        // Verify that registrant info was NOT updated
        Assert.Equal("Old Registrant", visit.RegistrantFullName);
        Assert.Equal("old.registrant@example.com", visit.RegistrantEmail);
    }
}
