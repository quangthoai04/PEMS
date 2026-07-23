using PEMS.Application.Common.Exceptions;
using PEMS.Application.Galleries.Common;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Unit tests for <see cref="GalleryPreviewedTranslation"/> — the save-time decision that lets a
/// previewed (AUTO_PREVIEW + matching hash) or hand-typed (MANUAL) EN be persisted WITHOUT a second
/// provider call, rejects a stale preview (PREVIEW_STALE, never silently re-translated), and falls back
/// to the legacy translate-during-save path (null) in every other case.
/// </summary>
public class GalleryPreviewedTranslationTests
{
    private const int MaxLength = 255;
    private const string Vi = "Tòa Alpha";

    // ── MANUAL ──

    [Fact]
    public void Manual_En_Is_Reused_With_Manual_Source_And_Fresh_Hash()
    {
        var resolved = GalleryPreviewedTranslation.TryResolve(
            Vi, "  Alpha Building  ", GalleryTranslationOrigins.Manual, providedHash: null, MaxLength);

        Assert.NotNull(resolved);
        Assert.Equal(GalleryTranslationSources.Manual, resolved!.TranslationSource);
        Assert.True(resolved.Result.Success);
        Assert.Equal("Alpha Building", resolved.Result.TranslatedText);
        Assert.Equal(GalleryTranslationStatuses.Ready, resolved.Result.Status);
        Assert.Equal(TranslationSourceHasher.ComputeHash(Vi), resolved.Result.SourceHash);
    }

    [Fact]
    public void Manual_Origin_Is_Case_Insensitive()
    {
        var resolved = GalleryPreviewedTranslation.TryResolve(Vi, "Alpha Building", "manual", null, MaxLength);
        Assert.NotNull(resolved);
        Assert.Equal(GalleryTranslationSources.Manual, resolved!.TranslationSource);
    }

    // ── AUTO_PREVIEW ──

    [Fact]
    public void AutoPreview_With_Matching_Hash_Is_Reused_As_Auto()
    {
        var hash = TranslationSourceHasher.ComputeHash(Vi);
        var resolved = GalleryPreviewedTranslation.TryResolve(
            Vi, "Alpha Building", GalleryTranslationOrigins.AutoPreview, hash, MaxLength);

        Assert.NotNull(resolved);
        Assert.Equal(GalleryTranslationSources.Auto, resolved!.TranslationSource);
        Assert.True(resolved.Result.Success);
        Assert.Equal("Alpha Building", resolved.Result.TranslatedText);
        Assert.Equal(hash, resolved.Result.SourceHash);
    }

    [Fact]
    public void AutoPreview_With_Stale_Hash_Throws_Preview_Stale()
    {
        var staleHash = TranslationSourceHasher.ComputeHash("Tòa Alpha CŨ");

        var ex = Assert.Throws<BusinessRuleException>(() => GalleryPreviewedTranslation.TryResolve(
            Vi, "Alpha Building", GalleryTranslationOrigins.AutoPreview, staleHash, MaxLength));

        Assert.Equal(GalleryErrorCodes.TranslationPreviewStale, ex.ErrorCode);
    }

    [Fact]
    public void AutoPreview_With_Missing_Hash_Throws_Preview_Stale()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => GalleryPreviewedTranslation.TryResolve(
            Vi, "Alpha Building", GalleryTranslationOrigins.AutoPreview, null, MaxLength));

        Assert.Equal(GalleryErrorCodes.TranslationPreviewStale, ex.ErrorCode);
    }

    // ── Fallback to translate-during-save ──

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_En_Falls_Back_To_Provider(string? en)
    {
        Assert.Null(GalleryPreviewedTranslation.TryResolve(
            Vi, en, GalleryTranslationOrigins.Manual, null, MaxLength));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NONE")]
    [InlineData("AUTO_ON_SAVE")]
    [InlineData("SOMETHING_ELSE")]
    public void Non_Reusable_Origin_Falls_Back_To_Provider(string? origin)
    {
        Assert.Null(GalleryPreviewedTranslation.TryResolve(Vi, "Alpha Building", origin, null, MaxLength));
    }

    // ── Over-cap EN is rejected, never truncated ──

    [Fact]
    public void Over_Cap_Manual_En_Throws()
    {
        var tooLong = new string('a', MaxLength + 1);
        Assert.Throws<BusinessRuleException>(() => GalleryPreviewedTranslation.TryResolve(
            Vi, tooLong, GalleryTranslationOrigins.Manual, null, MaxLength));
    }
}
