using System.Linq;
using PEMS.Application.Common.Files;
using Xunit;

namespace PEMS.UnitTests.Common;

/// <summary>
/// Guards the per-purpose upload limits, and specifically the split that lets Gallery ITEM media use
/// 20 MB while every other image purpose (area/location covers, news, partner, visit photos) stays on
/// the shared 5 MB rule. Formats must be identical across the split — only the size differs.
/// </summary>
public class FileValidationPolicyTests
{
    private const long Mb = 1024 * 1024;

    private static readonly FileValidationPolicy Policy = new();

    [Theory]
    [InlineData(FilePurpose.GalleryItemImage)]
    [InlineData(FilePurpose.GalleryDelegationImage)]
    public void GalleryItemImages_Allow_Up_To_20Mb(FilePurpose purpose)
    {
        var rule = Policy.GetRule(purpose);

        Assert.Equal(20 * Mb, rule.MaxSizeBytes);
        // Anything at or below the cap passes the size gate; anything above it does not.
        Assert.True(20 * Mb <= rule.MaxSizeBytes);
        Assert.True(19_900_000 <= rule.MaxSizeBytes);
        Assert.False(20 * Mb + 1 <= rule.MaxSizeBytes);
    }

    [Theory]
    [InlineData(FilePurpose.GalleryItemImage)]
    [InlineData(FilePurpose.GalleryDelegationImage)]
    public void GalleryItemImages_Keep_The_Image_Only_Formats(FilePurpose purpose)
    {
        var rule = Policy.GetRule(purpose);

        Assert.True(rule.RequireImageMagicBytes);
        Assert.Equal(
            new[] { "image/jpeg", "image/png", "image/webp" },
            rule.AllowedMimeTypes.OrderBy(m => m).ToArray());
        Assert.Equal(
            new[] { ".jpeg", ".jpg", ".png", ".webp" },
            rule.AllowedExtensions.OrderBy(e => e).ToArray());

        // Never silently widened to the formats the gallery explicitly rejects.
        Assert.DoesNotContain("image/svg+xml", rule.AllowedMimeTypes);
        Assert.DoesNotContain("image/gif", rule.AllowedMimeTypes);
        Assert.DoesNotContain("video/mp4", rule.AllowedMimeTypes);
        Assert.DoesNotContain("application/pdf", rule.AllowedMimeTypes);
        Assert.DoesNotContain(".svg", rule.AllowedExtensions);
        Assert.DoesNotContain(".gif", rule.AllowedExtensions);
        Assert.DoesNotContain(".mp4", rule.AllowedExtensions);
        Assert.DoesNotContain(".pdf", rule.AllowedExtensions);
    }

    /// <summary>Regression: the 20 MB bump must NOT leak into any neighbouring image purpose.</summary>
    [Theory]
    [InlineData(FilePurpose.GalleryLocationCover)]
    [InlineData(FilePurpose.GalleryAreaCover)]
    [InlineData(FilePurpose.GalleryImage)]
    [InlineData(FilePurpose.NewsImage)]
    [InlineData(FilePurpose.VisitRequestPhoto)]
    [InlineData(FilePurpose.PartnerLogo)]
    [InlineData(FilePurpose.PartnerCover)]
    public void Other_Image_Purposes_Stay_At_5Mb(FilePurpose purpose)
    {
        var rule = Policy.GetRule(purpose);

        Assert.Equal(5 * Mb, rule.MaxSizeBytes);
        Assert.False(5 * Mb + 1 <= rule.MaxSizeBytes);
        Assert.True(rule.RequireImageMagicBytes);
    }

    /// <summary>Area cover VIDEO is untouched by this change: MP4 only, 100 MB.</summary>
    [Fact]
    public void AreaCoverVideo_Rule_Is_Unchanged()
    {
        var rule = Policy.GetRule(FilePurpose.GalleryAreaCoverVideo);

        Assert.Equal(100 * Mb, rule.MaxSizeBytes);
        Assert.Equal(new[] { "video/mp4" }, rule.AllowedMimeTypes.ToArray());
        Assert.Equal(new[] { ".mp4" }, rule.AllowedExtensions.ToArray());
        Assert.False(rule.RequireImageMagicBytes);
    }
}
