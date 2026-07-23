using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Delegations.VisitPhotos.Queries.GetVisitInstancePhotos;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// Detail scope: only ACTIVE photos of the requested instance (never a sibling campus), proxy URLs
/// instead of Drive ids, canRemove only for the caller's own photos, and the v2 per-campus name from
/// the central dual-read resolver — never the request-level compatibility projection.
/// </summary>
public class GetVisitInstancePhotosQueryHandlerTests
{
    private static (DelegationsTestDbContext Db, GetVisitInstancePhotosQueryHandler Handler,
        Mock<IVisitFormReadService> FormRead) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        VisitPhotoTestSeed.SeedAcceptedStudent(db);
        db.Users.Add(DelegationsTestData.CreateUser(
            VisitPhotoTestSeed.OtherStudentUserId, DelegationsTestData.StudentRoleId, null, null));
        db.SaveChanges();

        // Pure V2: the handler ALWAYS resolves the target instance's own detail, so the resolver must
        // answer for every requested instance. Individual tests override this with per-campus names.
        var formRead = new Mock<IVisitFormReadService>();
        formRead
            .Setup(f => f.ResolveCampusFormContentAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequest>(),
                It.IsAny<IReadOnlyList<ulong>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PEMS.Domain.Entities.Delegations.VisitRequest _, IReadOnlyList<ulong> ids, CancellationToken _) =>
                ids.ToDictionary(id => id, _ => new VisitCampusFormContent { DelegationName = "Đoàn khách kiểm thử" })
                   as IReadOnlyDictionary<ulong, VisitCampusFormContent>);
        var handler = new GetVisitInstancePhotosQueryHandler(
            db, VisitPhotoTestSeed.StudentCurrentUser(), formRead.Object);
        return (db, handler, formRead);
    }

    [Fact]
    public async Task ReturnsOnlyActivePhotosOfTheInstance_WithOwnershipFlags()
    {
        var (db, handler, _) = CreateSut();
        var sibling = DelegationsTestData.CreateVisitInstance(visitInstanceId: 11, campusId: DelegationsTestData.OtherCampusId);
        db.VisitRequestCampuses.Add(sibling);
        db.SaveChanges();

        var folder = VisitPhotoTestSeed.AddFolder(db);
        var mine = VisitPhotoTestSeed.AddPhoto(db, folder, 900, VisitPhotoTestSeed.StudentUserId);
        var theirs = VisitPhotoTestSeed.AddPhoto(db, folder, 901, VisitPhotoTestSeed.OtherStudentUserId);
        var removed = VisitPhotoTestSeed.AddPhoto(db, folder, 902, VisitPhotoTestSeed.StudentUserId, status: "REMOVED");
        var siblingPhoto = VisitPhotoTestSeed.AddPhoto(db, folder, 903, VisitPhotoTestSeed.StudentUserId, visitInstanceId: 11);

        var dto = await handler.Handle(new GetVisitInstancePhotosQuery(DelegationsTestData.VisitInstanceId), default);

        Assert.Equal(2, dto.Photos.Count);
        Assert.DoesNotContain(dto.Photos,
            p => p.VisitPhotoId == removed.VisitPhotoId || p.VisitPhotoId == siblingPhoto.VisitPhotoId);
        Assert.True(dto.CanUpload);
        Assert.Equal(folder.FolderName, dto.FolderName);
        Assert.Equal(folder.WebViewUrl, dto.FolderWebViewUrl);

        var minePhoto = dto.Photos.Single(p => p.VisitPhotoId == mine.VisitPhotoId);
        Assert.True(minePhoto.UploadedByMe);
        Assert.True(minePhoto.CanRemove);
        Assert.Equal("/api/files/900/content", minePhoto.Url);

        var theirPhoto = dto.Photos.Single(p => p.VisitPhotoId == theirs.VisitPhotoId);
        Assert.False(theirPhoto.UploadedByMe);
        Assert.False(theirPhoto.CanRemove);
    }

    [Fact]
    public async Task InstanceOutsideStudentScope_IsForbidden_Idor()
    {
        var (db, handler, _) = CreateSut();
        var sibling = DelegationsTestData.CreateVisitInstance(visitInstanceId: 11, campusId: DelegationsTestData.OtherCampusId);
        db.VisitRequestCampuses.Add(sibling);
        db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new GetVisitInstancePhotosQuery(11), default));
    }

    [Fact]
    public async Task V2Request_UsesPerCampusDelegationName()
    {
        var (db, handler, formRead) = CreateSut();
        var visit = db.VisitRequests.Single();
        db.SaveChanges();

        formRead
            .Setup(f => f.ResolveCampusFormContentAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequest>(),
                It.Is<IReadOnlyList<ulong>>(ids => ids.Single() == DelegationsTestData.VisitInstanceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ulong, VisitCampusFormContent>
            {
                [DelegationsTestData.VisitInstanceId] = new() { DelegationName = "Đoàn per-campus HN" },
            });

        var dto = await handler.Handle(new GetVisitInstancePhotosQuery(DelegationsTestData.VisitInstanceId), default);

        Assert.Equal("Đoàn per-campus HN", dto.DelegationName);
    }
}
