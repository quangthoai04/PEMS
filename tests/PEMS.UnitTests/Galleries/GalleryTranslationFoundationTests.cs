using System;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Galleries;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Unit tests for the Gallery auto-translation foundation: source normalization (trim + collapse
/// whitespace, diacritics/casing preserved), the SHA-256 source hash (stable, 64 lowercase hex chars)
/// and the shared entity applier (success → AUTO/READY + EN + hash + translated_at; failure → EN NULL +
/// FAILED + hash of the NEW source + translated_at NULL; up-to-date check per BR §6.4).
/// </summary>
public class GalleryTranslationFoundationTests
{
    // ── TranslationSourceNormalizer ──

    [Fact]
    public void Normalize_Trims_And_Collapses_Whitespace()
    {
        Assert.Equal("Tòa Alpha", TranslationSourceNormalizer.Normalize("  Tòa    Alpha  "));
    }

    [Fact]
    public void Normalize_Preserves_Diacritics_And_Casing()
    {
        Assert.Equal("TÒA Delta đẹp", TranslationSourceNormalizer.Normalize("TÒA\t Delta\n đẹp"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_Blank_Returns_Empty(string? raw)
    {
        Assert.Equal(string.Empty, TranslationSourceNormalizer.Normalize(raw));
    }

    // ── TranslationSourceHasher ──

    [Fact]
    public void ComputeHash_Is_64_Lowercase_Hex_Chars()
    {
        var hash = TranslationSourceHasher.ComputeHash("Tòa Alpha");
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeHash_Same_Source_Same_Hash()
    {
        Assert.Equal(
            TranslationSourceHasher.ComputeHash("Tòa Alpha"),
            TranslationSourceHasher.ComputeHash("Tòa Alpha"));
    }

    [Fact]
    public void ComputeHash_Different_Source_Different_Hash()
    {
        Assert.NotEqual(
            TranslationSourceHasher.ComputeHash("Tòa Alpha"),
            TranslationSourceHasher.ComputeHash("Tòa Beta"));
    }

    // ── GalleryTranslationApplier.Apply ──

    [Fact]
    public void Apply_Success_Sets_Ready_Metadata()
    {
        var now = new DateTime(2026, 7, 23, 10, 0, 0);
        var area = new GalleryArea { AreaName = "Tòa Alpha" };
        var result = new GalleryTranslationResult
        {
            SourceText = "Tòa Alpha",
            SourceHash = TranslationSourceHasher.ComputeHash("Tòa Alpha"),
            TranslatedText = "Alpha Building",
            Success = true,
        };

        GalleryTranslationApplier.Apply(area, result, now);

        Assert.Equal("Alpha Building", area.AreaNameEn);
        Assert.Equal(GalleryTranslationSources.Auto, area.TranslationSource);
        Assert.Equal(GalleryTranslationStatuses.Ready, area.TranslationStatus);
        Assert.Equal(result.SourceHash, area.TranslationSourceHash);
        Assert.Equal(now, area.TranslatedAt);
    }

    [Fact]
    public void Apply_Failure_Clears_En_And_Marks_Failed_With_New_Hash()
    {
        var now = new DateTime(2026, 7, 23, 10, 0, 0);
        var location = new GalleryLocation
        {
            LocationName = "Trước tòa",
            // Stale EN from a previous source — MUST be cleared on failure (never keep stale meaning).
            LocationNameEn = "Old English",
            TranslationStatus = GalleryTranslationStatuses.Ready,
            TranslatedAt = now.AddDays(-1),
        };
        var result = new GalleryTranslationResult
        {
            SourceText = "Trước tòa",
            SourceHash = TranslationSourceHasher.ComputeHash("Trước tòa"),
            TranslatedText = null,
            Success = false,
        };

        GalleryTranslationApplier.Apply(location, result, now);

        Assert.Null(location.LocationNameEn);
        Assert.Equal(GalleryTranslationSources.Auto, location.TranslationSource);
        Assert.Equal(GalleryTranslationStatuses.Failed, location.TranslationStatus);
        Assert.Equal(result.SourceHash, location.TranslationSourceHash);
        Assert.Null(location.TranslatedAt);
    }

    [Fact]
    public void Apply_Item_Success_Sets_TitleEn()
    {
        var now = DateTime.UtcNow;
        var item = new GalleryItem { Title = "Tượng rồng Việt Nam" };
        var result = new GalleryTranslationResult
        {
            SourceText = "Tượng rồng Việt Nam",
            SourceHash = TranslationSourceHasher.ComputeHash("Tượng rồng Việt Nam"),
            TranslatedText = "Vietnamese Dragon Statue",
            Success = true,
        };

        GalleryTranslationApplier.Apply(item, result, now);

        Assert.Equal("Vietnamese Dragon Statue", item.TitleEn);
        Assert.Equal(GalleryTranslationStatuses.Ready, item.TranslationStatus);
    }

    // ── GalleryTranslationApplier.IsUpToDate (BR §6.4 skip rule) ──

    [Fact]
    public void IsUpToDate_Ready_HashMatch_EnPresent_True()
    {
        var hash = TranslationSourceHasher.ComputeHash("Tòa Alpha");
        Assert.True(GalleryTranslationApplier.IsUpToDate(
            GalleryTranslationStatuses.Ready, hash, "Alpha Building", "Tòa Alpha"));
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("FAILED")]
    [InlineData("OUTDATED")]
    [InlineData(null)]
    public void IsUpToDate_NonReady_Status_False(string? status)
    {
        var hash = TranslationSourceHasher.ComputeHash("Tòa Alpha");
        Assert.False(GalleryTranslationApplier.IsUpToDate(status, hash, "Alpha Building", "Tòa Alpha"));
    }

    [Fact]
    public void IsUpToDate_Hash_Mismatch_False()
    {
        var otherHash = TranslationSourceHasher.ComputeHash("Tòa Beta");
        Assert.False(GalleryTranslationApplier.IsUpToDate(
            GalleryTranslationStatuses.Ready, otherHash, "Alpha Building", "Tòa Alpha"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsUpToDate_Blank_En_False(string? en)
    {
        var hash = TranslationSourceHasher.ComputeHash("Tòa Alpha");
        Assert.False(GalleryTranslationApplier.IsUpToDate(
            GalleryTranslationStatuses.Ready, hash, en, "Tòa Alpha"));
    }
}
