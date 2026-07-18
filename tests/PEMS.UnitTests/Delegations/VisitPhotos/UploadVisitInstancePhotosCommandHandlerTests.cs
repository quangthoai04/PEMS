using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos;
using PEMS.Application.Delegations.VisitPhotos.Commands.UploadVisitInstancePhotos;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// Upload scope + pipeline: only an ACTIVE Student with ACCEPTED STUDENT participation in the exact
/// instance may upload; all files are policy-validated BEFORE any Drive call; every visit_photos row
/// links the server-resolved request/instance/folder; a failed link compensates the committed
/// Drive/files upload.
/// </summary>
public class UploadVisitInstancePhotosCommandHandlerTests
{
    private const string CampusFolderId = "drv-campus-C1";
    private const long UploadedFileId = 900;

    private sealed class Sut
    {
        public DelegationsTestDbContext Db = null!;
        public UploadVisitInstancePhotosCommandHandler Handler = null!;
        public FakeDelegationsCurrentUser User = null!;
        public Mock<IFileUploadService> FileUpload = null!;
        public Mock<IVisitPhotoFolderService> FolderService = null!;
        public Mock<IGoogleDriveStorageService> Drive = null!;
    }

    private static Sut CreateSut(
        string participantStatus = ParticipantStatuses.Accepted,
        string instanceStatus = "AFTER_VISIT",
        bool seedParticipant = true)
    {
        var db = DelegationsTestDbContext.Create();
        if (seedParticipant)
        {
            VisitPhotoTestSeed.SeedAcceptedStudent(db, participantStatus: participantStatus, instanceStatus: instanceStatus);
        }
        else
        {
            DelegationsTestData.SeedBase(db, instanceStatus);
            db.Users.Add(DelegationsTestData.CreateUser(
                VisitPhotoTestSeed.StudentUserId, DelegationsTestData.StudentRoleId, null, null));
            db.SaveChanges();
        }

        var folder = VisitPhotoTestSeed.AddFolder(db);

        var fileUpload = new Mock<IFileUploadService>(MockBehavior.Strict);
        var seq = 0;
        fileUpload
            .Setup(f => f.UploadBusinessFileAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                FilePurpose.VisitRequestPhoto, (long)VisitPhotoTestSeed.StudentUserId,
                CampusFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var id = UploadedFileId + seq++;
                return new UploadedFileDto
                {
                    FileId = id,
                    FileUrl = $"/api/files/{id}/content",
                    ExternalFileId = $"drv-file-{id}",
                    MimeType = "image/jpeg",
                    ChecksumSha256 = "00",
                    ObjectKey = $"visit-photos/{id}.jpg",
                };
            });

        var folderService = new Mock<IVisitPhotoFolderService>();
        folderService
            .Setup(s => s.EnsureUploadTargetAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequestCampus>(), "C1",
                VisitPhotoTestSeed.StudentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitPhotoUploadTarget { Folder = folder, CampusFolderExternalId = CampusFolderId });

        var drive = new Mock<IGoogleDriveStorageService>();

        var handler = new UploadVisitInstancePhotosCommandHandler(
            db, VisitPhotoTestSeed.StudentCurrentUser(), fileUpload.Object, new FileValidationPolicy(),
            folderService.Object, drive.Object,
            NullLogger<UploadVisitInstancePhotosCommandHandler>.Instance);

        return new Sut
        {
            Db = db,
            Handler = handler,
            User = VisitPhotoTestSeed.StudentCurrentUser(),
            FileUpload = fileUpload,
            FolderService = folderService,
            Drive = drive,
        };
    }

    private static UploadVisitInstancePhotosCommand Command(params UploadVisitInstancePhotoFile[] files) => new()
    {
        VisitInstanceId = DelegationsTestData.VisitInstanceId,
        Files = files.ToList(),
    };

    private static UploadVisitInstancePhotoFile JpegFile(string name = "photo.jpg")
        => new(VisitPhotoTestSeed.JpegBytes(), name, "image/jpeg");

    [Fact]
    public async Task AcceptedStudent_UploadsPhotos_LinksServerResolvedScope()
    {
        var sut = CreateSut();

        var response = await sut.Handler.Handle(Command(JpegFile("a.jpg"), JpegFile("b.jpg")), default);

        Assert.Equal(2, response.Photos.Count);
        Assert.All(response.Photos, p => Assert.StartsWith("/api/files/", p.Url));

        var rows = sut.Db.VisitPhotos.ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(DelegationsTestData.VisitRequestId, r.VisitRequestId);
            Assert.Equal(DelegationsTestData.VisitInstanceId, r.VisitInstanceId);
            Assert.Equal(VisitPhotoTestSeed.StudentUserId, r.UploadedBy);
            Assert.Equal("ACTIVE", r.Status);
        });

        Assert.Single(sut.Db.AuditLogs.Where(a => a.Action == "UPLOAD_VISIT_PHOTOS"));
        sut.FileUpload.Verify(f => f.UploadBusinessFileAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
            FilePurpose.VisitRequestPhoto, It.IsAny<long>(), CampusFolderId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task StudentWithoutParticipation_IsForbidden()
    {
        var sut = CreateSut(seedParticipant: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.Handler.Handle(Command(JpegFile()), default));
        sut.FolderService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ParticipantStatuses.Invited)]
    [InlineData(ParticipantStatuses.Declined)]
    [InlineData(ParticipantStatuses.Removed)]
    public async Task NonAcceptedParticipantStatus_IsForbidden(string status)
    {
        var sut = CreateSut(participantStatus: status);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.Handler.Handle(Command(JpegFile()), default));
    }

    [Fact]
    public async Task InactiveStudent_IsForbidden()
    {
        var sut = CreateSut();
        var student = sut.Db.Users.Single(u => u.UserId == VisitPhotoTestSeed.StudentUserId);
        student.Status = "INACTIVE";
        sut.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.Handler.Handle(Command(JpegFile()), default));
    }

    [Fact]
    public async Task NonStudentRole_IsForbidden_EvenIfHost()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db, "AFTER_VISIT");
        var handler = new UploadVisitInstancePhotosCommandHandler(
            db, new FakeDelegationsCurrentUser(), Mock.Of<IFileUploadService>(), new FileValidationPolicy(),
            Mock.Of<IVisitPhotoFolderService>(), Mock.Of<IGoogleDriveStorageService>(),
            NullLogger<UploadVisitInstancePhotosCommandHandler>.Instance);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(Command(JpegFile()), default));
    }

    [Fact]
    public async Task UnknownInstance_IsNotFound()
    {
        var sut = CreateSut();
        var command = new UploadVisitInstancePhotosCommand
        {
            VisitInstanceId = 9999,
            Files = new List<UploadVisitInstancePhotoFile> { JpegFile() },
        };

        await Assert.ThrowsAsync<NotFoundException>(() => sut.Handler.Handle(command, default));
    }

    [Fact]
    public async Task CancelledInstance_IsRejected()
    {
        var sut = CreateSut(instanceStatus: "CANCELLED");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => sut.Handler.Handle(Command(JpegFile()), default));
        Assert.Equal("VISIT_PHOTO_INSTANCE_CANCELLED", ex.ErrorCode);
    }

    [Fact]
    public async Task SpoofedMime_FailsBeforeAnyDriveWork()
    {
        var sut = CreateSut();
        var fake = new UploadVisitInstancePhotoFile(VisitPhotoTestSeed.PngBytes(), "photo.jpg", "image/jpeg");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => sut.Handler.Handle(Command(JpegFile(), fake), default));

        Assert.Equal("FILE_MAGIC_BYTES_MISMATCH", ex.ErrorCode);
        sut.FolderService.VerifyNoOtherCalls();
        Assert.Empty(sut.Db.VisitPhotos);
    }

    [Fact]
    public async Task OversizedFile_FailsBeforeAnyDriveWork()
    {
        var sut = CreateSut();
        var big = new UploadVisitInstancePhotoFile(
            VisitPhotoTestSeed.JpegBytes(6 * 1024 * 1024), "big.jpg", "image/jpeg");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => sut.Handler.Handle(Command(big), default));

        Assert.Equal("FILE_TOO_LARGE", ex.ErrorCode);
        sut.FolderService.VerifyNoOtherCalls();
    }

    /// <summary>Throws the relational duplicate-key failure the InMemory provider cannot produce
    /// (it only enforces primary/alternate keys, not unique indexes like uq_visit_photos_file).</summary>
    private sealed class FailingVisitPhotoSaveContext : DelegationsTestDbContext
    {
        public FailingVisitPhotoSaveContext(Microsoft.EntityFrameworkCore.DbContextOptions<DelegationsTestDbContext> options)
            : base(options) { }

        public bool FailVisitPhotoInsert { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailVisitPhotoInsert && ChangeTracker.Entries<PEMS.Domain.Entities.Delegations.VisitPhoto>()
                    .Any(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added))
                throw new Microsoft.EntityFrameworkCore.DbUpdateException("uq_visit_photos_file violated (simulated)");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task DbLinkFailure_AfterDriveUpload_CompensatesTheOrphan()
    {
        var db = new FailingVisitPhotoSaveContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DelegationsTestDbContext>()
                .UseInMemoryDatabase($"pems-visitphotos-fail-{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options);
        VisitPhotoTestSeed.SeedAcceptedStudent(db);
        var folder = VisitPhotoTestSeed.AddFolder(db);
        // The Drive/files half of the upload is already committed when the link row fails —
        // seed the files row the mock's FileId points at so the compensation has something to remove.
        db.Files.Add(new PEMS.Domain.Entities.Documents.UploadedFile
        {
            FileId = UploadedFileId,
            StorageProvider = "GOOGLE_DRIVE",
            ObjectKey = "visit-photos/orphan.jpg",
            OriginalFilename = "orphan.jpg",
            MimeType = "image/jpeg",
            FilePurpose = "VISIT_REQUEST_PHOTO",
            UploadedAt = new DateTime(2026, 7, 2),
        });
        db.SaveChanges();

        var fileUpload = new Mock<IFileUploadService>();
        fileUpload
            .Setup(f => f.UploadBusinessFileAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                FilePurpose.VisitRequestPhoto, It.IsAny<long>(), CampusFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadedFileDto
            {
                FileId = UploadedFileId,
                FileUrl = $"/api/files/{UploadedFileId}/content",
                ExternalFileId = $"drv-file-{UploadedFileId}",
                MimeType = "image/jpeg",
                ChecksumSha256 = "00",
                ObjectKey = "visit-photos/orphan.jpg",
            });
        var folderService = new Mock<IVisitPhotoFolderService>();
        folderService
            .Setup(s => s.EnsureUploadTargetAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequestCampus>(), It.IsAny<string>(),
                It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitPhotoUploadTarget { Folder = folder, CampusFolderExternalId = CampusFolderId });
        var drive = new Mock<IGoogleDriveStorageService>();

        var handler = new UploadVisitInstancePhotosCommandHandler(
            db, VisitPhotoTestSeed.StudentCurrentUser(), fileUpload.Object, new FileValidationPolicy(),
            folderService.Object, drive.Object,
            NullLogger<UploadVisitInstancePhotosCommandHandler>.Instance);

        db.FailVisitPhotoInsert = true;

        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => handler.Handle(Command(JpegFile()), default));

        // Compensation removed both halves of the orphan: the files row and the Drive binary.
        Assert.Empty(db.VisitPhotos.ToList());
        Assert.Empty(db.Files.ToList());
        drive.Verify(d => d.DeleteAsync($"drv-file-{UploadedFileId}", It.IsAny<CancellationToken>()), Times.Once);
    }
}
