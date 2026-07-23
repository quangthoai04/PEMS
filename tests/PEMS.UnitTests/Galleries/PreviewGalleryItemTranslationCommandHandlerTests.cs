using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Commands.PreviewGalleryItemTranslation;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Galleries;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Unit tests for the gallery item title "Dịch sang EN" preview handler: Staff Leader scope (never
/// public), entityType/field validation, source normalization, provider failure → retryable 422, and
/// the §8 EDIT-modal optimization — an unchanged, already-READY stored title is served from the
/// database with ZERO provider calls ("Dịch lại" on an unchanged title never pays Google again).
/// The handler must write NOTHING.
/// </summary>
public class PreviewGalleryItemTranslationCommandHandlerTests
{
    private static Mock<ICurrentUserService> StaffLeader(ulong campusId = 1UL)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.UserId).Returns(10UL);
        user.SetupGet(u => u.RoleCode).Returns(RoleCodes.Staff);
        user.SetupGet(u => u.SubRole).Returns(UserSubRoles.Leader);
        user.SetupGet(u => u.PrimaryCampusId).Returns(campusId);
        return user;
    }

    private static Mock<IGalleryTranslationCoordinator> TranslatorReturning(
        Func<IReadOnlyList<GalleryTranslationRequest>, IReadOnlyList<GalleryTranslationResult>> map)
    {
        var translator = new Mock<IGalleryTranslationCoordinator>(MockBehavior.Strict);
        translator
            .Setup(t => t.TranslateAsync(
                It.IsAny<IReadOnlyList<GalleryTranslationRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GalleryTranslationRequest> reqs, CancellationToken _) => map(reqs));
        return translator;
    }

    private static IReadOnlyList<GalleryTranslationResult> SuccessResults(
        IReadOnlyList<GalleryTranslationRequest> reqs)
        => reqs.Select(r => new GalleryTranslationResult
        {
            SourceText = r.NormalizedSource,
            SourceHash = TranslationSourceHasher.ComputeHash(r.NormalizedSource),
            TranslatedText = r.NormalizedSource + " EN",
            Success = true,
        }).ToList();

    /// <summary>Never-called strict coordinator — any provider call fails the test.</summary>
    private static Mock<IGalleryTranslationCoordinator> TranslatorNeverCalled()
        => new(MockBehavior.Strict);

    /// <summary>Seeds one item (id 5) in area/campus <paramref name="campusId"/> with the given
    /// stored translation snapshot.</summary>
    private static GalleryTestDbContext SeedItem(
        string title, string? titleEn, string status, string? hash, ulong campusId = 1UL)
    {
        var db = GalleryTestDbContext.Create();
        db.GalleryAreas.Add(new GalleryArea
        {
            AreaId = 2, CampusId = campusId, AreaName = "Tòa Alpha", AreaKey = "toa-alpha",
            CreatedAt = DateTime.UtcNow,
        });
        db.GalleryLocations.Add(new GalleryLocation
        {
            LocationId = 3, AreaId = 2, LocationName = "Trước tòa", LocationKey = "truoc-toa",
            CreatedAt = DateTime.UtcNow,
        });
        db.GalleryItems.Add(new GalleryItem
        {
            GalleryItemId = 5, LocationId = 3, Title = title, TitleEn = titleEn,
            TranslationStatus = status, TranslationSourceHash = hash,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return db;
    }

    private static PreviewGalleryItemTranslationCommand Command(
        string? sourceText, long? entityId = null,
        string? entityType = "GALLERY_ITEM", string? field = "TITLE")
        => new(entityType, field, entityId, sourceText);

    [Fact]
    public async Task Create_Preview_Normalizes_And_Returns_Google_Result()
    {
        IReadOnlyList<GalleryTranslationRequest>? seen = null;
        var translator = TranslatorReturning(reqs => { seen = reqs; return SuccessResults(reqs); });
        using var db = GalleryTestDbContext.Create();
        var sut = new PreviewGalleryItemTranslationCommandHandler(db, StaffLeader().Object, translator.Object);

        var result = await sut.Handle(Command("  Tượng   rồng Việt Nam "), CancellationToken.None);

        Assert.Single(seen!);
        Assert.Equal("Tượng rồng Việt Nam", seen![0].NormalizedSource); // normalized before the provider
        Assert.Equal("Tượng rồng Việt Nam", result.SourceText);
        Assert.Equal("Tượng rồng Việt Nam EN", result.TranslatedText);
        Assert.Equal(TranslationSourceHasher.ComputeHash("Tượng rồng Việt Nam"), result.SourceHash);
        Assert.Equal(GalleryTranslationPreviewSources.Google, result.ServedFrom);
    }

    [Fact]
    public async Task Edit_Preview_With_Unchanged_Ready_Title_Serves_From_Database_Zero_Provider_Calls()
    {
        var hash = TranslationSourceHasher.ComputeHash("Tượng rồng Việt Nam");
        using var db = SeedItem(
            "Tượng rồng Việt Nam", "Vietnamese Dragon Statue", GalleryTranslationStatuses.Ready, hash);
        var translator = TranslatorNeverCalled(); // strict: a provider call would throw
        var sut = new PreviewGalleryItemTranslationCommandHandler(db, StaffLeader().Object, translator.Object);

        var result = await sut.Handle(
            Command("Tượng rồng Việt Nam", entityId: 5), CancellationToken.None);

        Assert.Equal(GalleryTranslationPreviewSources.Database, result.ServedFrom);
        Assert.Equal("Vietnamese Dragon Statue", result.TranslatedText);
        Assert.Equal(hash, result.SourceHash);
    }

    [Fact]
    public async Task Edit_Preview_With_Changed_Title_Calls_Provider()
    {
        var storedHash = TranslationSourceHasher.ComputeHash("Tượng rồng Việt Nam");
        using var db = SeedItem(
            "Tượng rồng Việt Nam", "Vietnamese Dragon Statue", GalleryTranslationStatuses.Ready, storedHash);
        var calls = 0;
        var translator = TranslatorReturning(reqs => { calls++; return SuccessResults(reqs); });
        var sut = new PreviewGalleryItemTranslationCommandHandler(db, StaffLeader().Object, translator.Object);

        var result = await sut.Handle(
            Command("Tượng cóc Việt Nam", entityId: 5), CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(GalleryTranslationPreviewSources.Google, result.ServedFrom);
        Assert.Equal("Tượng cóc Việt Nam EN", result.TranslatedText);
    }

    [Fact]
    public async Task Edit_Preview_With_Failed_Stored_Translation_Calls_Provider()
    {
        // Stored FAILED metadata must never be served — provider is called even for an unchanged title.
        var hash = TranslationSourceHasher.ComputeHash("Tượng rồng Việt Nam");
        using var db = SeedItem(
            "Tượng rồng Việt Nam", null, GalleryTranslationStatuses.Failed, hash);
        var calls = 0;
        var translator = TranslatorReturning(reqs => { calls++; return SuccessResults(reqs); });
        var sut = new PreviewGalleryItemTranslationCommandHandler(db, StaffLeader().Object, translator.Object);

        var result = await sut.Handle(
            Command("Tượng rồng Việt Nam", entityId: 5), CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(GalleryTranslationPreviewSources.Google, result.ServedFrom);
    }

    [Fact]
    public async Task Edit_Preview_For_Other_Campus_Item_Is_Forbidden()
    {
        var hash = TranslationSourceHasher.ComputeHash("Tượng rồng Việt Nam");
        using var db = SeedItem(
            "Tượng rồng Việt Nam", "Vietnamese Dragon Statue", GalleryTranslationStatuses.Ready, hash,
            campusId: 9UL); // item belongs to campus 9, caller is campus 1
        var sut = new PreviewGalleryItemTranslationCommandHandler(
            db, StaffLeader().Object, TranslatorNeverCalled().Object);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => sut.Handle(
            Command("Tượng rồng Việt Nam", entityId: 5), CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.GalleryScopeForbidden, ex.ErrorCode);
    }

    [Fact]
    public async Task Edit_Preview_For_Missing_Item_Throws_NotFound()
    {
        using var db = GalleryTestDbContext.Create();
        var sut = new PreviewGalleryItemTranslationCommandHandler(
            db, StaffLeader().Object, TranslatorNeverCalled().Object);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.Handle(
            Command("Tượng rồng Việt Nam", entityId: 999), CancellationToken.None));
    }

    [Theory]
    [InlineData(null, "TITLE")]
    [InlineData("", "TITLE")]
    [InlineData("NEWS_ARTICLE", "TITLE")]
    [InlineData("GALLERY_ITEM", null)]
    [InlineData("GALLERY_ITEM", "DESCRIPTION")]
    public async Task Invalid_EntityType_Or_Field_Throws_Invalid_Mode(string? entityType, string? field)
    {
        using var db = GalleryTestDbContext.Create();
        var sut = new PreviewGalleryItemTranslationCommandHandler(
            db, StaffLeader().Object, TranslatorNeverCalled().Object);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.Handle(
            Command("Tượng rồng", entityType: entityType, field: field), CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.InvalidMode, ex.ErrorCode);
    }

    [Fact]
    public async Task Blank_Source_Throws_Preview_Empty()
    {
        using var db = GalleryTestDbContext.Create();
        var sut = new PreviewGalleryItemTranslationCommandHandler(
            db, StaffLeader().Object, TranslatorNeverCalled().Object);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.Handle(
            Command("   "), CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.TranslationPreviewEmpty, ex.ErrorCode);
    }

    [Fact]
    public async Task Provider_Failure_Throws_Retryable_Preview_Failed()
    {
        // The coordinator NEVER throws — it reports FAILED results; the preview must surface that as a
        // retryable error instead of returning an empty EN.
        var translator = TranslatorReturning(reqs => reqs.Select(r => new GalleryTranslationResult
        {
            SourceText = r.NormalizedSource,
            SourceHash = TranslationSourceHasher.ComputeHash(r.NormalizedSource),
            TranslatedText = null,
            Success = false,
        }).ToList());
        using var db = GalleryTestDbContext.Create();
        var sut = new PreviewGalleryItemTranslationCommandHandler(db, StaffLeader().Object, translator.Object);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.Handle(
            Command("Tượng rồng Việt Nam"), CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.TranslationPreviewFailed, ex.ErrorCode);
    }

    [Fact]
    public async Task Non_Staff_Leader_Is_Forbidden()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.RoleCode).Returns(RoleCodes.Student);
        using var db = GalleryTestDbContext.Create();
        var sut = new PreviewGalleryItemTranslationCommandHandler(
            db, user.Object, TranslatorNeverCalled().Object);

        await Assert.ThrowsAsync<AuthBusinessException>(() => sut.Handle(
            Command("Tượng rồng Việt Nam"), CancellationToken.None));
    }
}
