using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Delegations.VisitPhotos;
using PEMS.Domain.Entities.Delegations;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// Drive-folder provisioning: the VR-{request} row is created exactly once per request (the DB
/// unique key arbitrates concurrent first uploads — the loser reuses the winner's row and cleans up
/// its duplicate Drive folder), and the campus subfolder is ensured idempotently below it.
/// </summary>
public class VisitPhotoFolderServiceTests
{
    private const string RootFolderId = "drv-root";

    private static DbContextOptions<DelegationsTestDbContext> Options(string dbName) =>
        new DbContextOptionsBuilder<DelegationsTestDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static Mock<IFileStorageFolderResolver> Resolver()
    {
        var resolver = new Mock<IFileStorageFolderResolver>();
        resolver.Setup(r => r.ResolveFolderId(FilePurpose.VisitRequestPhoto)).Returns(RootFolderId);
        return resolver;
    }

    [Fact]
    public async Task FirstUpload_CreatesTheRequestFolderRowAndDriveTree()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);

        var drive = new Mock<IGoogleDriveStorageService>();
        drive.Setup(d => d.EnsureChildFolderAsync($"VR-{instance.VisitRequestId}", RootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveFolderResult { ExternalFolderId = "drv-vr", WebViewUrl = "https://drive/vr" });
        drive.Setup(d => d.EnsureChildFolderAsync("C1", "drv-vr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveFolderResult { ExternalFolderId = "drv-campus" });
        drive.Setup(d => d.EnsureChildFolderAsync("Ảnh", "drv-campus", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveFolderResult { ExternalFolderId = "drv-photo" });

        var service = new VisitPhotoFolderService(
            db, drive.Object, Resolver().Object, NullLogger<VisitPhotoFolderService>.Instance);

        var target = await service.EnsureUploadTargetAsync(instance, "C1", 200, default);

        Assert.Equal("drv-photo", target.PhotoFolderExternalId);
        var row = Assert.Single(db.VisitPhotoFolders.ToList());
        Assert.Equal($"VR-{instance.VisitRequestId}", row.FolderName);
        Assert.Equal("drv-vr", row.ExternalFolderId);
        Assert.Equal("https://drive/vr", row.WebViewUrl);
        Assert.Equal(200UL, row.CreatedBy);
    }

    [Fact]
    public async Task ExistingFolderRow_IsReused_NoNewRequestFolderOnDrive()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var existing = VisitPhotoTestSeed.AddFolder(db);

        var drive = new Mock<IGoogleDriveStorageService>(MockBehavior.Strict);
        drive.Setup(d => d.EnsureChildFolderAsync("C1", existing.ExternalFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveFolderResult { ExternalFolderId = "drv-campus" });
        drive.Setup(d => d.EnsureChildFolderAsync("Ảnh", "drv-campus", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveFolderResult { ExternalFolderId = "drv-photo" });

        var service = new VisitPhotoFolderService(
            db, drive.Object, Resolver().Object, NullLogger<VisitPhotoFolderService>.Instance);

        var target = await service.EnsureUploadTargetAsync(instance, "C1", 200, default);

        Assert.Equal(existing.VisitPhotoFolderId, target.Folder.VisitPhotoFolderId);
        Assert.Equal("drv-photo", target.PhotoFolderExternalId);
        Assert.Single(db.VisitPhotoFolders.ToList());
        drive.VerifyAll(); // strict: only the campus + Ảnh ensures ran — no second VR folder was created
    }

    /// <summary>Simulates the uq_visit_photo_folders_request duplicate-key failure the InMemory
    /// provider cannot produce (it only enforces primary/alternate keys, not unique indexes).</summary>
    private sealed class FailingFolderInsertContext : DelegationsTestDbContext
    {
        public FailingFolderInsertContext(DbContextOptions<DelegationsTestDbContext> options) : base(options) { }

        public bool FailNextFolderInsert { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailNextFolderInsert && ChangeTracker.Entries<VisitPhotoFolder>()
                    .Any(e => e.State == EntityState.Added))
            {
                FailNextFolderInsert = false;
                throw new DbUpdateException("uq_visit_photo_folders_request violated (simulated)");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task ConcurrentFirstUpload_LoserReusesWinnerRow_AndDeletesItsDuplicateDriveFolder()
    {
        // Two contexts over the SAME InMemory store simulate two racing requests.
        var dbName = $"pems-visitphotos-race-{Guid.NewGuid():N}";
        var db = new FailingFolderInsertContext(Options(dbName));
        var (instance, _) = DelegationsTestData.SeedBase(db);

        var drive = new Mock<IGoogleDriveStorageService>();
        drive.Setup(d => d.EnsureChildFolderAsync($"VR-{instance.VisitRequestId}", RootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // The "winner" commits its row between our existence check and our SaveChanges;
                // our own insert then fails on the request unique key (simulated above).
                db.FailNextFolderInsert = true;
                using var winnerDb = new DelegationsTestDbContext(Options(dbName));
                winnerDb.VisitPhotoFolders.Add(new VisitPhotoFolder
                {
                    VisitRequestId = instance.VisitRequestId,
                    ExternalFolderId = "drv-winner",
                    FolderName = $"VR-{instance.VisitRequestId}",
                    Status = "ACTIVE",
                    CreatedAt = new DateTime(2026, 7, 1),
                });
                winnerDb.SaveChanges();
                return new GoogleDriveFolderResult { ExternalFolderId = "drv-loser" };
            });
        drive.Setup(d => d.EnsureChildFolderAsync("C1", "drv-winner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveFolderResult { ExternalFolderId = "drv-campus" });
        drive.Setup(d => d.EnsureChildFolderAsync("Ảnh", "drv-campus", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveFolderResult { ExternalFolderId = "drv-photo" });

        var service = new VisitPhotoFolderService(
            db, drive.Object, Resolver().Object, NullLogger<VisitPhotoFolderService>.Instance);

        var target = await service.EnsureUploadTargetAsync(instance, "C1", 200, default);

        // The loser adopted the winner's folder and dropped its own duplicate Drive folder.
        Assert.Equal("drv-winner", target.Folder.ExternalFolderId);
        Assert.Equal("drv-photo", target.PhotoFolderExternalId);
        drive.Verify(d => d.DeleteAsync("drv-loser", It.IsAny<CancellationToken>()), Times.Once);

        using var verifyDb = new DelegationsTestDbContext(Options(dbName));
        Assert.Single(verifyDb.VisitPhotoFolders.ToList());
    }
}
