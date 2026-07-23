using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Delegations.VisitPhotos.Queries.GetMyVisitPhotoFolders;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// "Quản lý ảnh đoàn khách" listing: one row per ACCEPTED STUDENT instance only, active-photo
/// counts, search on the RESOLVED name, and — for a v2 MIXED request — each campus instance shows
/// its OWN per-campus delegation name from the dual-read resolver.
/// </summary>
public class GetMyVisitPhotoFoldersQueryHandlerTests
{
    private static (DelegationsTestDbContext Db, GetMyVisitPhotoFoldersQueryHandler Handler,
        Mock<IVisitFormReadService> FormRead) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        VisitPhotoTestSeed.SeedAcceptedStudent(db);

        var formRead = new Mock<IVisitFormReadService>();
        var handler = new GetMyVisitPhotoFoldersQueryHandler(
            db, VisitPhotoTestSeed.StudentCurrentUser(), formRead.Object);
        return (db, handler, formRead);
    }

    [Fact]
    public async Task ListsOnlyAcceptedStudentInstances_WithFolderAndCounts()
    {
        var (db, handler, _) = CreateSut();
        // Instance 11: only INVITED — must not appear.
        var invited = DelegationsTestData.CreateVisitInstance(visitInstanceId: 11, campusId: DelegationsTestData.OtherCampusId);
        db.VisitRequestCampuses.Add(invited);
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            801, VisitPhotoTestSeed.StudentUserId, ParticipantRoles.Student, ParticipantStatuses.Invited,
            visitInstanceId: 11));
        db.SaveChanges();

        var folder = VisitPhotoTestSeed.AddFolder(db);
        VisitPhotoTestSeed.AddPhoto(db, folder, 900, VisitPhotoTestSeed.StudentUserId);
        VisitPhotoTestSeed.AddPhoto(db, folder, 901, VisitPhotoTestSeed.StudentUserId, status: "REMOVED");

        var page = await handler.Handle(new GetMyVisitPhotoFoldersQuery(), default);

        var row = Assert.Single(page.Items);
        Assert.Equal(DelegationsTestData.VisitInstanceId, row.VisitInstanceId);
        Assert.Equal("Đoàn khách kiểm thử", row.DelegationName);
        Assert.Equal(folder.FolderName, row.FolderName);
        Assert.Equal(1, row.ActivePhotoCount); // REMOVED photos are not counted
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task SearchFiltersOnResolvedDelegationName()
    {
        var (db, handler, _) = CreateSut();

        var none = await handler.Handle(new GetMyVisitPhotoFoldersQuery { Search = "không khớp" }, default);
        Assert.Empty(none.Items);

        var hit = await handler.Handle(new GetMyVisitPhotoFoldersQuery { Search = "kiểm thử" }, default);
        Assert.Single(hit.Items);
    }

    [Fact]
    public async Task V2MixedRequest_ShowsEachInstanceOwnPerCampusName()
    {
        var (db, handler, formRead) = CreateSut();
        var visit = db.VisitRequests.Single();
        visit.FormSchemaVersion = FormSchemaVersions.PerCampus;

        var second = DelegationsTestData.CreateVisitInstance(visitInstanceId: 11, campusId: DelegationsTestData.OtherCampusId);
        db.VisitRequestCampuses.Add(second);
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            802, VisitPhotoTestSeed.StudentUserId, ParticipantRoles.Student, ParticipantStatuses.Accepted,
            visitInstanceId: 11));
        db.SaveChanges();

        formRead
            .Setup(f => f.ResolveCampusFormContentAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequest>(),
                It.IsAny<IReadOnlyList<ulong>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ulong, VisitCampusFormContent>
            {
                [DelegationsTestData.VisitInstanceId] = new() { DelegationName = "Đoàn Hà Nội" },
                [11] = new() { DelegationName = "Đoàn Hồ Chí Minh" },
            });

        var page = await handler.Handle(new GetMyVisitPhotoFoldersQuery(), default);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal("Đoàn Hà Nội",
            page.Items.Single(i => i.VisitInstanceId == DelegationsTestData.VisitInstanceId).DelegationName);
        Assert.Equal("Đoàn Hồ Chí Minh",
            page.Items.Single(i => i.VisitInstanceId == 11).DelegationName);
        // Nếu handler rơi về compatibility projection thì cả hai dòng sẽ cùng tên request-level.
        Assert.DoesNotContain(page.Items, i => i.DelegationName == "Đoàn khách kiểm thử");
    }

    /// <summary>
    /// READING the folder list is deliberately broader than UPLOADING. The face-scan / photo-tagging
    /// workflow needs the Host (Staff) to open the đoàn's folder, so a Staff host sees the instances they
    /// host. Uploading stays Student-only — that stricter rule is enforced by
    /// <c>VisitPhotoStudentScope</c> and by the DB trigger, and is covered in
    /// <c>UploadVisitInstancePhotosCommandHandlerTests</c>.
    /// </summary>
    [Fact]
    public async Task StaffHost_SeesHostedInstances()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        var handler = new GetMyVisitPhotoFoldersQueryHandler(
            db, new FakeDelegationsCurrentUser(), Mock.Of<IVisitFormReadService>());

        var page = await handler.Handle(new GetMyVisitPhotoFoldersQuery(), default);

        Assert.All(page.Items, i => Assert.Equal(DelegationsTestData.VisitInstanceId, i.VisitInstanceId));
    }

    [Fact]
    public async Task UnauthenticatedCaller_IsForbidden()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        var handler = new GetMyVisitPhotoFoldersQueryHandler(
            db, new FakeDelegationsCurrentUser { IsAuthenticated = false, UserId = null },
            Mock.Of<IVisitFormReadService>());

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new GetMyVisitPhotoFoldersQuery(), default));
    }
}
