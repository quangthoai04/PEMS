using MediatR;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.UploadVisitPhotos;
using PEMS.Application.Files.Commands.UploadFile;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.UploadVisitPhotos;

/// <summary>
/// An accepted DEPARTMENT participant must stay read-only on visit media — same split as Minutes
/// (MinuteAccess) and News (VisitNewsAccess): Department contributes logistics, not the visit's
/// own media/record. Only the host or an accepted IC/Student participant may upload.
/// </summary>
public class UploadVisitPhotosDepartmentAccessTests
{
    private const ulong DeptStaffId = 500;

    private static (DelegationsTestDbContext Db, UploadVisitPhotosCommandHandler Handler, FakeDelegationsCurrentUser User)
        CreateSut(string participantRole, string participantStatus, string instanceStatus = PEMS.Shared.VisitInstanceStatus.AfterVisit)
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db, instanceStatus: instanceStatus);

        db.Departments.Add(DelegationsTestData.CreateDepartment(60));
        db.Users.Add(DelegationsTestData.CreateUser(DeptStaffId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Staff, 60));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(600, DeptStaffId, participantRole, participantStatus));
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser { UserId = DeptStaffId, RoleId = DelegationsTestData.DepartmentRoleId, RoleCode = RoleCodes.Department, SubRole = UserSubRoles.Staff };

        var mediator = new Mock<IMediator>(MockBehavior.Loose);
        mediator.Setup(m => m.Send(It.IsAny<UploadFileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadedFileDto { FileId = 1, OriginalFilename = "a.jpg" });

        var handler = new UploadVisitPhotosCommandHandler(db, user, mediator.Object);
        return (db, handler, user);
    }

    private static UploadVisitPhotosCommand Command() => new()
    {
        VisitInstanceId = DelegationsTestData.VisitInstanceId,
        Files = new List<UploadVisitPhotoFileDto>
        {
            new(new byte[] { 1, 2, 3 }, "a.jpg", "image/jpeg"),
        },
    };

    [Fact]
    public async Task AcceptedDepartmentParticipant_CannotUploadMedia()
    {
        var (_, handler, _) = CreateSut(ParticipantRoles.DeptSupport, ParticipantStatuses.Accepted);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(Command(), default));
    }

    [Fact]
    public async Task AssignedDepartmentParticipant_CannotUploadMedia()
    {
        var (_, handler, _) = CreateSut(ParticipantRoles.DeptSupport, ParticipantStatuses.Assigned);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(Command(), default));
    }

    [Fact]
    public async Task AcceptedIcSupportParticipant_PassesTheAuthorizationGate()
    {
        var (_, handler, _) = CreateSut(ParticipantRoles.IcSupport, ParticipantStatuses.Accepted);
        // Empty file list: isolates the gate itself (ForbiddenException or not) from the unrelated
        // Document-persistence path, which this lightweight fixture's DbContext doesn't model.
        var command = new UploadVisitPhotosCommand { VisitInstanceId = DelegationsTestData.VisitInstanceId };

        var response = await handler.Handle(command, default);

        Assert.NotNull(response);
    }
}
