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
    /// <summary>Resolver stub that answers for every requested instance (Pure V2 always resolves).</summary>
    private static IVisitFormReadService StubFormRead()
    {
        var mock = new Mock<IVisitFormReadService>();
        mock.Setup(f => f.ResolveCampusFormContentAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequest>(),
                It.IsAny<IReadOnlyList<ulong>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PEMS.Domain.Entities.Delegations.VisitRequest _, IReadOnlyList<ulong> ids, CancellationToken _) =>
                ids.ToDictionary(id => id, _ => new VisitCampusFormContent { DelegationName = "Đoàn khách kiểm thử" })
                   as IReadOnlyDictionary<ulong, VisitCampusFormContent>);
        return mock.Object;
    }

    private static (DelegationsTestDbContext Db, GetMyVisitPhotoFoldersQueryHandler Handler,
        Mock<IVisitFormReadService> FormRead) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        VisitPhotoTestSeed.SeedAcceptedStudent(db);

        // Pure V2: the handler ALWAYS resolves each folder row's own instance detail, so the resolver
        // must answer for every requested instance. Mixed tests override this with per-campus names.
        var formRead = new Mock<IVisitFormReadService>();
        formRead
            .Setup(f => f.ResolveCampusFormContentAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequest>(),
                It.IsAny<IReadOnlyList<ulong>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PEMS.Domain.Entities.Delegations.VisitRequest _, IReadOnlyList<ulong> ids, CancellationToken _) =>
                ids.ToDictionary(id => id, _ => new VisitCampusFormContent { DelegationName = "Đoàn khách kiểm thử" })
                   as IReadOnlyDictionary<ulong, VisitCampusFormContent>);
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
    public async Task SearchFiltersOnFolderName()
    {
        var (db, handler, _) = CreateSut();
        var folder = VisitPhotoTestSeed.AddFolder(db);

        var hit = await handler.Handle(new GetMyVisitPhotoFoldersQuery { Search = folder.FolderName }, default);
        Assert.Single(hit.Items);
    }

    /// <summary>
    /// The search matches a person tagged in one of the delegation's photos. The tag table is a real
    /// part of the model here: a slice that pruned it would fail the query outright rather than
    /// report "no match", which is how this branch went unnoticed.
    /// </summary>
    [Fact]
    public async Task SearchFiltersOnTaggedPersonDisplayName()
    {
        var (db, handler, _) = CreateSut();
        var folder = VisitPhotoTestSeed.AddFolder(db);
        VisitPhotoTestSeed.AddPhoto(db, folder, 910, VisitPhotoTestSeed.StudentUserId);
        AddFaceTag(db, fileId: 910, displayName: "Nguyễn Văn Khách", personNameKey: "nguyen van khach");

        Assert.Single((await handler.Handle(
            new GetMyVisitPhotoFoldersQuery { Search = "Văn Khách" }, default)).Items);
        Assert.Empty((await handler.Handle(
            new GetMyVisitPhotoFoldersQuery { Search = "Không Có Ai" }, default)).Items);
    }

    /// <summary>Accent-free typing still finds the tag, via the normalized <c>person_name_key</c>.</summary>
    [Fact]
    public async Task SearchFiltersOnTaggedPersonNameKey()
    {
        var (db, handler, _) = CreateSut();
        var folder = VisitPhotoTestSeed.AddFolder(db);
        VisitPhotoTestSeed.AddPhoto(db, folder, 911, VisitPhotoTestSeed.StudentUserId);
        AddFaceTag(db, fileId: 911, displayName: "Nguyễn Văn Khách", personNameKey: "nguyen van khach");

        // "van khach" appears in the KEY only — the display name is accented, so a match here proves
        // the key branch ran rather than the display-name one.
        Assert.Single((await handler.Handle(
            new GetMyVisitPhotoFoldersQuery { Search = "van khach" }, default)).Items);
    }

    /// <summary>A tag on a photo of ANOTHER delegation must not pull this one into the results.</summary>
    [Fact]
    public async Task SearchOnTaggedPerson_DoesNotLeakInstancesOutOfScope()
    {
        var (db, handler, _) = CreateSut();
        var folder = VisitPhotoTestSeed.AddFolder(db);
        VisitPhotoTestSeed.AddPhoto(db, folder, 912, VisitPhotoTestSeed.StudentUserId);
        // Photo 913 belongs to instance 11, which this student does not take part in.
        db.VisitPhotos.Add(new PEMS.Domain.Entities.Delegations.VisitPhoto
        {
            VisitRequestId = folder.VisitRequestId,
            VisitInstanceId = 11,
            VisitPhotoFolderId = folder.VisitPhotoFolderId,
            FileId = 913,
            Status = "ACTIVE",
            UploadedBy = VisitPhotoTestSeed.StudentUserId,
            UploadedAt = new DateTime(2026, 7, 2),
        });
        db.SaveChanges();
        AddFaceTag(db, fileId: 913, displayName: "Người Ngoài Phạm Vi", personNameKey: "nguoi ngoai pham vi");

        Assert.Empty((await handler.Handle(
            new GetMyVisitPhotoFoldersQuery { Search = "Ngoài Phạm Vi" }, default)).Items);
    }

    [Fact]
    public async Task SearchFiltersOnGuestMemberFullName()
    {
        var (db, handler, _) = CreateSut();
        AddGuestMember(db, guestMemberId: 601, fullName: "Trần Thị Đoàn Viên", organization: "Đại học Bách Khoa");

        Assert.Single((await handler.Handle(
            new GetMyVisitPhotoFoldersQuery { Search = "Đoàn Viên" }, default)).Items);
        Assert.Empty((await handler.Handle(
            new GetMyVisitPhotoFoldersQuery { Search = "Người Lạ" }, default)).Items);
    }

    [Fact]
    public async Task SearchFiltersOnGuestMemberOrganization()
    {
        var (db, handler, _) = CreateSut();
        AddGuestMember(db, guestMemberId: 602, fullName: "Trần Thị Đoàn Viên", organization: "Đại học Bách Khoa");

        Assert.Single((await handler.Handle(
            new GetMyVisitPhotoFoldersQuery { Search = "Bách Khoa" }, default)).Items);
    }

    /// <summary>A guest member linked to a campus instance the caller cannot see stays invisible.</summary>
    [Fact]
    public async Task SearchOnGuestMember_DoesNotLeakInstancesOutOfScope()
    {
        var (db, handler, _) = CreateSut();
        AddGuestMember(db, guestMemberId: 603, fullName: "Khách Của Cơ Sở Khác",
            organization: "Tổ Chức Khác", visitInstanceId: 11);

        Assert.Empty((await handler.Handle(
            new GetMyVisitPhotoFoldersQuery { Search = "Cơ Sở Khác" }, default)).Items);
    }

    private static void AddFaceTag(
        DelegationsTestDbContext db, ulong fileId, string displayName, string personNameKey)
    {
        db.FaceTags.Add(new PEMS.Domain.Entities.Galleries.PhotoFaceTag
        {
            FileId = fileId,
            DisplayName = displayName,
            PersonNameKey = personNameKey,
            TagStatus = "ACTIVE",
            CreatedAt = new DateTime(2026, 7, 4),
        });
        db.SaveChanges();
    }

    /// <summary>
    /// A guest member plus the per-campus link row that binds it to an instance — the pair the
    /// search joins on. Both carry the same request id, as the composite FKs require.
    /// </summary>
    private static void AddGuestMember(
        DelegationsTestDbContext db, ulong guestMemberId, string fullName, string organization,
        ulong visitInstanceId = DelegationsTestData.VisitInstanceId)
    {
        db.GuestMembers.Add(new PEMS.Domain.Entities.Delegations.VisitGuestMember
        {
            GuestMemberId = guestMemberId,
            VisitRequestId = DelegationsTestData.VisitRequestId,
            FullName = fullName,
            Organization = organization,
            JobTitle = "Trưởng đoàn",
            Nationality = "VN",
            CreatedAt = new DateTime(2026, 7, 1),
        });
        db.InstanceGuestMembers.Add(new PEMS.Domain.Entities.Delegations.VisitInstanceGuestMember
        {
            VisitRequestId = DelegationsTestData.VisitRequestId,
            VisitInstanceId = visitInstanceId,
            GuestMemberId = guestMemberId,
            CreatedAt = new DateTime(2026, 7, 1),
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task V2MixedRequest_ShowsEachInstanceOwnPerCampusName()
    {
        var (db, handler, formRead) = CreateSut();
        var visit = db.VisitRequests.Single();

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
    /// READING the folder list uses its own visibility rule (Admin/Staff-Leader see everything; a
    /// regular Staff host sees only the instances they host or participate in). Uploading has a
    /// separate rule enforced by <c>VisitPhotoStudentScope</c> and by the DB trigger — see
    /// <c>UploadVisitInstancePhotosCommandHandlerTests</c>.
    /// </summary>
    [Fact]
    public async Task StaffHost_SeesHostedInstances()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        var handler = new GetMyVisitPhotoFoldersQueryHandler(
            db, new FakeDelegationsCurrentUser(), StubFormRead());

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
