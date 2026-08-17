using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Galleries.Commands.UpdateGalleryLocation;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Galleries;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Copy-on-write area rules for "Chỉnh sửa khu vực và vị trí":
///   • rename + siblings      → new area created, ONLY this location moves, old area untouched
///   • video only (no rename) → shared area cover replaced, every sibling location follows
///   • rename + new video     → new area gets the new video, old area keeps the old one
///   • rename, no new video   → new area INHERITS the old cover file id (no Drive copy)
///   • single-location area   → renamed in place, no duplicate area
///   • duplicate area name    → rejected before any upload, nothing written
/// </summary>
public class UpdateGalleryLocationAreaSplitTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 10, 0, 0, DateTimeKind.Unspecified);

    private const ulong CampusId = 1;
    private const ulong SharedAreaId = 2;
    private const ulong SoloAreaId = 5;
    private const ulong OldCoverFileId = 800;
    private const ulong NewUploadFileId = 801;

    // ── Fakes ───────────────────────────────────────────────────────────────────────────────

    private static Mock<ICurrentUserService> StaffLeader()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.UserId).Returns(10UL);
        user.SetupGet(u => u.RoleCode).Returns(RoleCodes.Staff);
        user.SetupGet(u => u.SubRole).Returns(UserSubRoles.Leader);
        user.SetupGet(u => u.PrimaryCampusId).Returns(CampusId);
        return user;
    }

    private static Mock<IDateTimeService> Clock()
    {
        var clock = new Mock<IDateTimeService>();
        clock.SetupGet(c => c.VietnamNow).Returns(Now);
        clock.SetupGet(c => c.UtcNow).Returns(Now);
        return clock;
    }

    /// <summary>Uploader that always "stores" a file and hands back <see cref="NewUploadFileId"/>.</summary>
    private static Mock<IFileUploadService> Uploader(List<FilePurpose> seenPurposes)
    {
        var upload = new Mock<IFileUploadService>();
        upload.Setup(u => u.UploadBusinessFileAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<FilePurpose>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string _, string _, long _, FilePurpose purpose, long _, CancellationToken _) =>
            {
                seenPurposes.Add(purpose);
                return new UploadedFileDto { FileId = (long)NewUploadFileId };
            });
        return upload;
    }

    private static Mock<IGalleryTranslationCoordinator> Translator(List<string> seenSources)
    {
        var translator = new Mock<IGalleryTranslationCoordinator>();
        translator.Setup(t => t.TranslateAsync(
                It.IsAny<IReadOnlyList<GalleryTranslationRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GalleryTranslationRequest> reqs, CancellationToken _) =>
            {
                seenSources.AddRange(reqs.Select(r => r.NormalizedSource));
                return reqs.Select(r => new GalleryTranslationResult
                {
                    SourceText = r.NormalizedSource,
                    SourceHash = TranslationSourceHasher.ComputeHash(r.NormalizedSource),
                    TranslatedText = r.NormalizedSource + " EN",
                    Success = true,
                }).ToList();
            });
        return translator;
    }

    private static GalleryUploadFileCommandDto Mp4(string name = "beta.mp4")
        => new(new byte[] { 1, 2, 3, 4 }, name, "video/mp4", 4, null, null);

    // ── Seed ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Campus 1:
    ///   area 2 "Tòa B" (cover 800) → locations 101 "Sảnh 1", 102 "Tầng 1", 103 "Đồi Blockchain"
    ///   area 5 "Tòa C" (cover 800) → location 105 "Sảnh chính"   (single-location area)
    /// </summary>
    private static GalleryTestDbContext Seed()
    {
        var db = GalleryTestDbContext.Create();
        db.UploadedFiles.Add(new UploadedFile
        {
            FileId = OldCoverFileId, ObjectKey = "gallery/areas/B.mp4", OriginalFilename = "B.mp4",
            MimeType = "video/mp4", FilePurpose = FilePurposeDbValues.GalleryAreaCoverVideo,
            UploadedAt = Now,
        });
        db.GalleryAreas.AddRange(
            new GalleryArea
            {
                AreaId = SharedAreaId, CampusId = CampusId, AreaName = "Tòa B", AreaKey = "toa-b",
                AreaNameEn = "Building B", CoverFileId = OldCoverFileId, Status = "ACTIVE",
                DisplayOrder = 3, TranslationStatus = "READY", CreatedAt = Now,
            },
            new GalleryArea
            {
                AreaId = SoloAreaId, CampusId = CampusId, AreaName = "Tòa C", AreaKey = "toa-c",
                CoverFileId = OldCoverFileId, Status = "ACTIVE", CreatedAt = Now,
            });
        db.GalleryLocations.AddRange(
            new GalleryLocation { LocationId = 101, AreaId = SharedAreaId, LocationName = "Sảnh 1", LocationKey = "sanh-1", Status = "ACTIVE", CreatedAt = Now },
            new GalleryLocation { LocationId = 102, AreaId = SharedAreaId, LocationName = "Tầng 1", LocationKey = "tang-1", Status = "ACTIVE", CreatedAt = Now },
            new GalleryLocation { LocationId = 103, AreaId = SharedAreaId, LocationName = "Đồi Blockchain", LocationKey = "doi-blockchain", Status = "ACTIVE", CreatedAt = Now },
            new GalleryLocation { LocationId = 105, AreaId = SoloAreaId, LocationName = "Sảnh chính", LocationKey = "sanh-chinh", Status = "ACTIVE", CreatedAt = Now });
        db.SaveChanges();
        return db;
    }

    private static UpdateGalleryLocationCommandHandler Sut(
        GalleryTestDbContext db,
        Mock<IFileUploadService>? upload = null,
        Mock<IGalleryTranslationCoordinator>? translator = null)
        => new(
            db,
            StaffLeader().Object,
            (upload ?? Uploader(new List<FilePurpose>())).Object,
            new Mock<IGoogleDriveStorageService>().Object,
            (translator ?? Translator(new List<string>())).Object,
            Clock().Object,
            NullLogger<UpdateGalleryLocationCommandHandler>.Instance);

    private static UpdateGalleryLocationCommand Edit(
        long locationId, string areaName, string locationName,
        GalleryUploadFileCommandDto? areaVideo = null)
        => new(locationId, areaName, null, null, null, locationName, null, null, null, areaVideo, null);

    // ── Case 1 — shared area, rename only ───────────────────────────────────────────────────

    [Fact]
    public async Task Rename_On_A_Shared_Area_Splits_And_Moves_Only_The_Edited_Location()
    {
        using var db = Seed();
        var sut = Sut(db);

        await sut.Handle(Edit(101, "Tòa Beta", "Sảnh 1"), CancellationToken.None);

        var newArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaKey == "toa-beta");
        Assert.NotEqual(SharedAreaId, newArea.AreaId);
        Assert.Equal("Tòa Beta", newArea.AreaName);
        Assert.Equal(CampusId, newArea.CampusId);
        Assert.Equal("ACTIVE", newArea.Status);
        Assert.Equal(3u, newArea.DisplayOrder);   // copied from the source area
        Assert.Equal(10UL, newArea.CreatedBy);

        var locations = await db.GalleryLocations.AsNoTracking().OrderBy(l => l.LocationId).ToListAsync();
        Assert.Equal(newArea.AreaId, locations.Single(l => l.LocationId == 101).AreaId);
        Assert.Equal(SharedAreaId, locations.Single(l => l.LocationId == 102).AreaId);
        Assert.Equal(SharedAreaId, locations.Single(l => l.LocationId == 103).AreaId);

        // The old area is completely untouched — name, EN name and cover.
        var oldArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaId == SharedAreaId);
        Assert.Equal("Tòa B", oldArea.AreaName);
        Assert.Equal("Building B", oldArea.AreaNameEn);
        Assert.Equal(OldCoverFileId, oldArea.CoverFileId);
        Assert.Null(oldArea.UpdatedAt);
    }

    [Fact]
    public async Task Split_Translates_The_New_Area_Name_And_Never_Copies_The_Old_En()
    {
        using var db = Seed();
        var sources = new List<string>();
        var sut = Sut(db, translator: Translator(sources));

        await sut.Handle(Edit(101, "Tòa Beta", "Sảnh 1"), CancellationToken.None);

        var newArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaKey == "toa-beta");
        Assert.Equal("Tòa Beta EN", newArea.AreaNameEn);
        Assert.NotEqual("Building B", newArea.AreaNameEn);
        Assert.Equal(GalleryTranslationStatuses.Ready, newArea.TranslationStatus);
        Assert.Equal(TranslationSourceHasher.ComputeHash("Tòa Beta"), newArea.TranslationSourceHash);
        // Only the changed area name went to the provider — the unchanged location name did not.
        Assert.Equal(new[] { "Tòa Beta" }, sources.ToArray());
    }

    [Fact]
    public async Task Split_Writes_Create_And_Move_Audit_Rows_And_None_For_The_Old_Area()
    {
        using var db = Seed();
        var sut = Sut(db);

        await sut.Handle(Edit(101, "Tòa Beta", "Sảnh 1"), CancellationToken.None);

        var newArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaKey == "toa-beta");
        var audits = await db.AuditLogEntries.AsNoTracking().Include(a => a.Changes).ToListAsync();

        var created = audits.Single(a => a.Action == "CREATE_GALLERY_AREA_FROM_LOCATION_EDIT");
        Assert.Equal("GalleryArea", created.EntityType);
        Assert.Equal(newArea.AreaId, created.EntityId);

        var moved = audits.Single(a => a.Action == "MOVE_GALLERY_LOCATION_TO_NEW_AREA");
        Assert.Equal("GalleryLocation", moved.EntityType);
        Assert.Equal(101UL, moved.EntityId);
        var areaIdChange = moved.Changes.Single(c => c.FieldName == "AreaId");
        Assert.Equal(SharedAreaId.ToString(), areaIdChange.OldValueText);
        Assert.Equal(newArea.AreaId.ToString(), areaIdChange.NewValueText);
        // JsonSerializer escapes non-ASCII, so read the payload back instead of substring-matching it.
        using var payload = JsonDocument.Parse(
            moved.Changes.Single(c => c.FieldName == "GalleryLocation").NewValueText!);
        Assert.Equal("Tòa B", payload.RootElement.GetProperty("oldAreaName").GetString());
        Assert.Equal("Tòa Beta", payload.RootElement.GetProperty("newAreaName").GetString());
        Assert.False(payload.RootElement.GetProperty("areaVideoChanged").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("inheritedCover").GetBoolean());

        // The old area did not change, so it must NOT get an UPDATE_GALLERY_AREA row.
        Assert.DoesNotContain(audits, a => a.Action == "UPDATE_GALLERY_AREA");
        // The location's own fields did not change either — only its area membership.
        Assert.DoesNotContain(audits, a => a.Action == "UPDATE_GALLERY_LOCATION");
    }

    // ── Case 2 — shared area, video only ────────────────────────────────────────────────────

    [Fact]
    public async Task Video_Only_Edit_Keeps_The_Shared_Area_And_Applies_To_Every_Sibling()
    {
        using var db = Seed();
        var purposes = new List<FilePurpose>();
        var sources = new List<string>();
        var sut = Sut(db, Uploader(purposes), Translator(sources));

        await sut.Handle(Edit(101, "Tòa B", "Sảnh 1", Mp4("b-new.mp4")), CancellationToken.None);

        // No new area at all.
        Assert.Equal(2, await db.GalleryAreas.AsNoTracking().CountAsync());
        var area = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaId == SharedAreaId);
        Assert.Equal(NewUploadFileId, area.CoverFileId);   // shared cover replaced
        Assert.Equal("Tòa B", area.AreaName);
        Assert.Equal("Building B", area.AreaNameEn);       // translation metadata untouched

        var locations = await db.GalleryLocations.AsNoTracking()
            .Where(l => l.AreaId == SharedAreaId).Select(l => l.LocationId).OrderBy(id => id).ToListAsync();
        Assert.Equal(new ulong[] { 101, 102, 103 }, locations.ToArray()); // all three follow the new video

        Assert.Equal(new[] { FilePurpose.GalleryAreaCoverVideo }, purposes.ToArray());
        Assert.Empty(sources); // a cover-only edit never hits the translation provider
    }

    // ── Case 3 — shared area, rename + new video ────────────────────────────────────────────

    [Fact]
    public async Task Rename_With_New_Video_Gives_The_Video_To_The_New_Area_Only()
    {
        using var db = Seed();
        var sut = Sut(db);

        await sut.Handle(Edit(101, "Tòa Beta", "Sảnh 1", Mp4()), CancellationToken.None);

        var newArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaKey == "toa-beta");
        var oldArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaId == SharedAreaId);

        Assert.Equal(NewUploadFileId, newArea.CoverFileId);
        Assert.Equal(OldCoverFileId, oldArea.CoverFileId);  // siblings keep B.mp4
        Assert.Equal(SharedAreaId, (await db.GalleryLocations.AsNoTracking().SingleAsync(l => l.LocationId == 102)).AreaId);
    }

    // ── Case 4 — shared area, rename without a new video ────────────────────────────────────

    [Fact]
    public async Task Rename_Without_A_New_Video_Inherits_The_Old_Cover_File_Reference()
    {
        using var db = Seed();
        var sut = Sut(db);

        await sut.Handle(Edit(101, "Tòa Beta", "Sảnh 1"), CancellationToken.None);

        var newArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaKey == "toa-beta");
        var oldArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaId == SharedAreaId);

        // SAME file row referenced twice — never duplicated on Drive, never deleted.
        Assert.Equal(OldCoverFileId, newArea.CoverFileId);
        Assert.Equal(OldCoverFileId, oldArea.CoverFileId);
        Assert.NotNull(await db.UploadedFiles.AsNoTracking().SingleOrDefaultAsync(f => f.FileId == OldCoverFileId));
    }

    // ── Case 5 — single-location area ───────────────────────────────────────────────────────

    [Fact]
    public async Task Rename_Of_A_Single_Location_Area_Happens_In_Place()
    {
        using var db = Seed();
        var sut = Sut(db);

        await sut.Handle(Edit(105, "Tòa Gamma", "Sảnh chính"), CancellationToken.None);

        Assert.Equal(2, await db.GalleryAreas.AsNoTracking().CountAsync()); // no extra area
        var area = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaId == SoloAreaId);
        Assert.Equal("Tòa Gamma", area.AreaName);
        Assert.Equal("toa-gamma", area.AreaKey);
        Assert.Equal(SoloAreaId, (await db.GalleryLocations.AsNoTracking().SingleAsync(l => l.LocationId == 105)).AreaId);

        var audits = await db.AuditLogEntries.AsNoTracking().ToListAsync();
        Assert.Contains(audits, a => a.Action == "UPDATE_GALLERY_AREA");
        Assert.DoesNotContain(audits, a => a.Action == "CREATE_GALLERY_AREA_FROM_LOCATION_EDIT");
    }

    // ── Case 6 — duplicate area name ────────────────────────────────────────────────────────

    [Fact]
    public async Task Renaming_To_An_Existing_Campus_Area_Is_Rejected_Before_Any_Upload()
    {
        using var db = Seed();
        var purposes = new List<FilePurpose>();
        var sut = Sut(db, Uploader(purposes));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => sut.Handle(Edit(101, "Tòa C", "Sảnh 1", Mp4()), CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.AreaDuplicate, ex.ErrorCode);
        Assert.Empty(purposes);                                    // nothing uploaded → no Drive orphan
        Assert.Equal(2, await db.GalleryAreas.AsNoTracking().CountAsync());
        Assert.Equal(SharedAreaId, (await db.GalleryLocations.AsNoTracking().SingleAsync(l => l.LocationId == 101)).AreaId);
        Assert.Empty(await db.AuditLogEntries.AsNoTracking().ToListAsync());
    }

    // ── Case 7 — location renamed in the same request ───────────────────────────────────────

    [Fact]
    public async Task Area_Split_And_Location_Rename_Both_Apply_In_One_Request()
    {
        using var db = Seed();
        var sources = new List<string>();
        var sut = Sut(db, translator: Translator(sources));

        await sut.Handle(Edit(101, "Tòa Beta", "Sảnh chính A"), CancellationToken.None);

        var newArea = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaKey == "toa-beta");
        var location = await db.GalleryLocations.AsNoTracking().SingleAsync(l => l.LocationId == 101);

        Assert.Equal(newArea.AreaId, location.AreaId);
        Assert.Equal("Sảnh chính A", location.LocationName);
        Assert.Equal("sanh-chinh-a", location.LocationKey);
        Assert.Equal("Sảnh chính A EN", location.LocationNameEn);
        // Both names went to the provider in ONE batched call.
        Assert.Equal(new[] { "Tòa Beta", "Sảnh chính A" }, sources.ToArray());

        var audits = await db.AuditLogEntries.AsNoTracking().Select(a => a.Action).ToListAsync();
        Assert.Contains("CREATE_GALLERY_AREA_FROM_LOCATION_EDIT", audits);
        Assert.Contains("MOVE_GALLERY_LOCATION_TO_NEW_AREA", audits);
        Assert.Contains("UPDATE_GALLERY_LOCATION", audits);
    }

    // ── Case 8 — stale translation preview ──────────────────────────────────────────────────

    [Fact]
    public async Task A_Stale_Area_Preview_Writes_Nothing_And_Uploads_Nothing()
    {
        using var db = Seed();
        var purposes = new List<FilePurpose>();
        var sut = Sut(db, Uploader(purposes));

        var stale = new UpdateGalleryLocationCommand(
            101, "Tòa Beta", "Beta Building", GalleryTranslationOrigins.AutoPreview,
            TranslationSourceHasher.ComputeHash("Tòa Delta"), // hash of a DIFFERENT source
            "Sảnh 1", null, null, null, Mp4(), null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => sut.Handle(stale, CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.TranslationPreviewStale, ex.ErrorCode);
        Assert.Empty(purposes);
        Assert.Equal(2, await db.GalleryAreas.AsNoTracking().CountAsync());
        Assert.Equal(SharedAreaId, (await db.GalleryLocations.AsNoTracking().SingleAsync(l => l.LocationId == 101)).AreaId);
    }

    // ── Cosmetic rename that keeps the normalized key ───────────────────────────────────────

    /// <summary>
    /// (campus_id, area_key) is UNIQUE, so a rename that collapses to the SAME key cannot produce a
    /// second area. It stays an in-place rename of the one shared area rather than a 409 against itself.
    /// </summary>
    [Fact]
    public async Task Rename_That_Keeps_The_Same_Key_Renames_In_Place()
    {
        using var db = Seed();
        var sut = Sut(db);

        await sut.Handle(Edit(101, "TÒA B", "Sảnh 1"), CancellationToken.None);

        Assert.Equal(2, await db.GalleryAreas.AsNoTracking().CountAsync());
        var area = await db.GalleryAreas.AsNoTracking().SingleAsync(a => a.AreaId == SharedAreaId);
        Assert.Equal("TÒA B", area.AreaName);
        Assert.Equal("toa-b", area.AreaKey);
        Assert.Equal(SharedAreaId, (await db.GalleryLocations.AsNoTracking().SingleAsync(l => l.LocationId == 101)).AreaId);
    }
}
