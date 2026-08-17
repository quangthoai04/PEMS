using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Commands.DeleteGalleryItem;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Galleries;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// "Xóa nội dung Gallery" handler: Staff Leader campus scope, SOFT delete of the item + its media,
/// audit trail, and deterministic behaviour on a repeat delete. Nothing is ever physically removed —
/// the row survives with <c>deleted_at</c>/<c>deleted_by</c> set, which is what makes every existing
/// management/public query (all of which filter <c>DeletedAt == null</c>) drop it.
/// </summary>
public class DeleteGalleryItemCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 9, 30, 0, DateTimeKind.Unspecified);

    private static Mock<ICurrentUserService> StaffLeader(ulong campusId = 1, ulong userId = 10)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.UserId).Returns(userId);
        user.SetupGet(u => u.RoleCode).Returns(RoleCodes.Staff);
        user.SetupGet(u => u.SubRole).Returns(UserSubRoles.Leader);
        user.SetupGet(u => u.PrimaryCampusId).Returns(campusId);
        return user;
    }

    private static Mock<IDateTimeService> Clock()
    {
        var clock = new Mock<IDateTimeService>();
        clock.SetupGet(c => c.VietnamNow).Returns(Now);
        clock.SetupGet(c => c.UtcNow).Returns(Now);
        return clock;
    }

    /// <summary>Campus 1 → area 2 → location 3 → item 4 (+ 2 active media, 1 already-deleted media).</summary>
    private static GalleryTestDbContext SeedCampusOneItem(ulong areaCampusId = 1)
    {
        var db = GalleryTestDbContext.Create();
        db.GalleryAreas.Add(new GalleryArea
        {
            AreaId = 2, CampusId = areaCampusId, AreaName = "Tòa B", AreaKey = "toa-b", CreatedAt = Now,
        });
        db.GalleryLocations.Add(new GalleryLocation
        {
            LocationId = 3, AreaId = 2, LocationName = "Sảnh 1", LocationKey = "sanh-1", CreatedAt = Now,
        });
        db.GalleryItems.Add(new GalleryItem
        {
            GalleryItemId = 4, LocationId = 3, Title = "Campus Experience 2026",
            Status = "PUBLISHED", CreatedAt = Now,
        });
        db.GalleryItemMedias.AddRange(
            new GalleryItemMedia { MediaId = 41, GalleryItemId = 4, FileId = 900, MediaType = "IMAGE", IsPrimary = true, Status = "ACTIVE", CreatedAt = Now },
            new GalleryItemMedia { MediaId = 42, GalleryItemId = 4, FileId = 901, MediaType = "IMAGE", Status = "ACTIVE", CreatedAt = Now },
            new GalleryItemMedia { MediaId = 43, GalleryItemId = 4, FileId = 902, MediaType = "IMAGE", Status = "HIDDEN", CreatedAt = Now, DeletedAt = Now.AddDays(-1), DeletedBy = 99 });
        db.SaveChanges();
        return db;
    }

    private static DeleteGalleryItemCommandHandler Sut(GalleryTestDbContext db, Mock<ICurrentUserService> user)
        => new(db, user.Object, Clock().Object);

    [Fact]
    public async Task Deletes_Own_Campus_Item_By_Setting_DeletedAt_And_DeletedBy()
    {
        using var db = SeedCampusOneItem();
        var sut = Sut(db, StaffLeader());

        var result = await sut.Handle(new DeleteGalleryItemCommand(4), CancellationToken.None);

        Assert.Equal(4UL, result.GalleryItemId);
        Assert.Equal("Xóa nội dung Gallery thành công.", result.Message);

        var item = await db.GalleryItems.AsNoTracking().SingleAsync(i => i.GalleryItemId == 4);
        Assert.Equal(Now, item.DeletedAt);
        Assert.Equal(10UL, item.DeletedBy);
        Assert.Equal(Now, item.UpdatedAt);
        Assert.Equal(10UL, item.UpdatedBy);
        // Soft delete, NOT a hard delete and NOT a status flip.
        Assert.Equal("PUBLISHED", item.Status);
    }

    [Fact]
    public async Task Soft_Deletes_The_Items_Active_Media_And_Leaves_Already_Deleted_Rows_Alone()
    {
        using var db = SeedCampusOneItem();
        var sut = Sut(db, StaffLeader());

        await sut.Handle(new DeleteGalleryItemCommand(4), CancellationToken.None);

        var media = await db.GalleryItemMedias.AsNoTracking()
            .Where(m => m.GalleryItemId == 4).OrderBy(m => m.MediaId).ToListAsync();
        Assert.Equal(3, media.Count); // nothing physically removed
        Assert.All(media.Where(m => m.MediaId != 43), m =>
        {
            Assert.Equal(Now, m.DeletedAt);
            Assert.Equal(10UL, m.DeletedBy);
            Assert.Equal("HIDDEN", m.Status);
            Assert.False(m.IsPrimary);
        });
        // The row deleted a day earlier keeps its original delete stamp.
        var previouslyDeleted = media.Single(m => m.MediaId == 43);
        Assert.Equal(Now.AddDays(-1), previouslyDeleted.DeletedAt);
        Assert.Equal(99UL, previouslyDeleted.DeletedBy);
    }

    [Fact]
    public async Task Writes_An_Audit_Row()
    {
        using var db = SeedCampusOneItem();
        var sut = Sut(db, StaffLeader());

        await sut.Handle(new DeleteGalleryItemCommand(4), CancellationToken.None);

        var audit = await db.AuditLogEntries.AsNoTracking()
            .Include(a => a.Changes)
            .SingleAsync(a => a.Action == "DELETE_GALLERY_ITEM");
        Assert.Equal("GalleryItem", audit.EntityType);
        Assert.Equal(4UL, audit.EntityId);
        Assert.Equal(10UL, audit.ActorUserId);
        Assert.Equal(1UL, audit.CampusId);
        var payload = audit.Changes.Single(c => c.FieldName == "GalleryItem").NewValueText!;
        Assert.Contains("\"galleryItemId\":4", payload);
        Assert.Contains("Campus Experience 2026", payload);
        Assert.Contains("\"locationId\":3", payload);
        Assert.Contains("\"areaId\":2", payload);
    }

    [Fact]
    public async Task Rejects_An_Item_Of_Another_Campus()
    {
        using var db = SeedCampusOneItem(areaCampusId: 7); // item lives in campus 7
        var sut = Sut(db, StaffLeader(campusId: 1));

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(
            () => sut.Handle(new DeleteGalleryItemCommand(4), CancellationToken.None));
        Assert.Equal("GALLERY_SCOPE_FORBIDDEN", ex.ErrorCode);

        var item = await db.GalleryItems.AsNoTracking().SingleAsync(i => i.GalleryItemId == 4);
        Assert.Null(item.DeletedAt);
    }

    [Fact]
    public async Task Rejects_A_Missing_Item()
    {
        using var db = SeedCampusOneItem();
        var sut = Sut(db, StaffLeader());

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new DeleteGalleryItemCommand(999), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_A_Non_Staff_Leader()
    {
        using var db = SeedCampusOneItem();
        var visitor = new Mock<ICurrentUserService>();
        visitor.SetupGet(u => u.IsAuthenticated).Returns(true);
        visitor.SetupGet(u => u.UserId).Returns(10UL);
        visitor.SetupGet(u => u.RoleCode).Returns(RoleCodes.Visitor);
        visitor.SetupGet(u => u.PrimaryCampusId).Returns(1UL);
        var sut = Sut(db, visitor);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(
            () => sut.Handle(new DeleteGalleryItemCommand(4), CancellationToken.None));
        Assert.Equal("GALLERY_MANAGEMENT_FORBIDDEN", ex.ErrorCode);
    }

    [Fact]
    public async Task Second_Delete_Is_A_Controlled_Conflict_Not_A_Silent_Success()
    {
        using var db = SeedCampusOneItem();
        var sut = Sut(db, StaffLeader());

        await sut.Handle(new DeleteGalleryItemCommand(4), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => sut.Handle(new DeleteGalleryItemCommand(4), CancellationToken.None));

        Assert.Equal("GALLERY_ITEM_ALREADY_DELETED", ex.ErrorCode);
        // Still exactly one delete audit row — the failed retry wrote nothing.
        Assert.Single(await db.AuditLogEntries.AsNoTracking()
            .Where(a => a.Action == "DELETE_GALLERY_ITEM").ToListAsync());
    }

    /// <summary>
    /// The contract every management/public query relies on: a deleted item no longer matches the
    /// <c>DeletedAt == null</c> predicate those queries apply (list, search, detail, public gallery).
    /// </summary>
    [Fact]
    public async Task Deleted_Item_No_Longer_Matches_The_Live_Item_Predicate()
    {
        using var db = SeedCampusOneItem();
        var sut = Sut(db, StaffLeader());

        await sut.Handle(new DeleteGalleryItemCommand(4), CancellationToken.None);

        Assert.Empty(await db.GalleryItems.AsNoTracking()
            .Where(i => i.DeletedAt == null && i.Location.Area.CampusId == 1).ToListAsync());
        Assert.Empty(await db.GalleryItemMedias.AsNoTracking()
            .Where(m => m.GalleryItemId == 4 && m.DeletedAt == null).ToListAsync());
    }

    /// <summary>Delete beats Hide/Publish: a deleted item can no longer be re-published or re-hidden.</summary>
    [Theory]
    [InlineData("PUBLISHED")]
    [InlineData("HIDDEN")]
    public async Task A_Deleted_Item_Can_No_Longer_Change_Status(string status)
    {
        using var db = SeedCampusOneItem();
        await Sut(db, StaffLeader()).Handle(new DeleteGalleryItemCommand(4), CancellationToken.None);

        var toggle = new PEMS.Application.Galleries.Commands.ChangeGalleryItemStatus
            .ChangeGalleryItemStatusCommandHandler(db, StaffLeader().Object, Clock().Object);

        await Assert.ThrowsAsync<NotFoundException>(() => toggle.Handle(
            new PEMS.Application.Galleries.Commands.ChangeGalleryItemStatus
                .ChangeGalleryItemStatusCommand(4, status),
            CancellationToken.None));
    }
}
