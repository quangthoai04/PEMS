using PEMS.Application.Common.Files;
using PEMS.Application.Galleries.Common;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Unit tests for the "video đại diện khu vực" (area cover MP4) foundation: the dedicated
/// <see cref="FilePurpose.GalleryAreaCoverVideo"/> purpose (DB value + Drive folder), its
/// <see cref="FileValidationPolicy"/> rule (MP4 only, ≤ 100 MB), and the IMAGE vs VIDEO cover
/// media-type resolution that keeps legacy image-cover areas working alongside new video-cover areas.
/// </summary>
public class GalleryAreaCoverVideoTests
{
    // ── FilePurpose mapping ──

    [Fact]
    public void GalleryAreaCoverVideo_Maps_To_Canonical_DbValue()
    {
        Assert.Equal("GALLERY_AREA_COVER_VIDEO", FilePurpose.GalleryAreaCoverVideo.ToDbValue());
        Assert.Equal(FilePurposeDbValues.GalleryAreaCoverVideo, FilePurpose.GalleryAreaCoverVideo.ToDbValue());
    }

    [Fact]
    public void GalleryAreaCoverVideo_Uses_The_Gallery_Areas_Folder()
    {
        Assert.Equal("gallery/areas", FilePurpose.GalleryAreaCoverVideo.ToObjectKeyPrefix());
        // Same folder as the legacy image cover — both are area master data.
        Assert.Equal(
            FilePurpose.GalleryAreaCover.ToObjectKeyPrefix(),
            FilePurpose.GalleryAreaCoverVideo.ToObjectKeyPrefix());
    }

    [Fact]
    public void GalleryAreaCoverVideo_Is_Not_A_Gallery_Item_Video_Purpose()
    {
        // Must never be confused with the gallery ITEM video purposes.
        Assert.NotEqual(FilePurposeDbValues.GalleryItemVideo, FilePurpose.GalleryAreaCoverVideo.ToDbValue());
        Assert.NotEqual(FilePurposeDbValues.GalleryVideo, FilePurpose.GalleryAreaCoverVideo.ToDbValue());
        Assert.NotEqual(FilePurposeDbValues.GalleryDelegationVideo, FilePurpose.GalleryAreaCoverVideo.ToDbValue());
    }

    // ── FileValidationPolicy rule ──

    [Fact]
    public void ValidationRule_Allows_Only_Mp4()
    {
        var rule = new FileValidationPolicy().GetRule(FilePurpose.GalleryAreaCoverVideo);

        Assert.Contains("video/mp4", rule.AllowedMimeTypes);
        Assert.Contains(".mp4", rule.AllowedExtensions);

        // Other video / image formats are rejected for an area cover video.
        Assert.DoesNotContain("video/webm", rule.AllowedMimeTypes);
        Assert.DoesNotContain(".webm", rule.AllowedExtensions);
        Assert.DoesNotContain(".mov", rule.AllowedExtensions);
        Assert.DoesNotContain(".avi", rule.AllowedExtensions);
        Assert.DoesNotContain("image/png", rule.AllowedMimeTypes);
    }

    [Fact]
    public void ValidationRule_Caps_Size_At_100_Mb_And_Skips_Image_Magic_Bytes()
    {
        var rule = new FileValidationPolicy().GetRule(FilePurpose.GalleryAreaCoverVideo);

        Assert.Equal(100L * 1024 * 1024, rule.MaxSizeBytes);
        Assert.False(rule.RequireImageMagicBytes);
    }

    // ── Cover media type resolution (legacy image vs new video) ──

    [Theory]
    [InlineData("GALLERY_AREA_COVER_VIDEO", "video/mp4")]
    [InlineData("GALLERY_AREA_COVER_VIDEO", null)]
    [InlineData("SOMETHING_ELSE", "video/mp4")]
    [InlineData(null, "video/quicktime")]
    public void Resolve_Returns_Video_For_Video_Purpose_Or_Video_Mime(string? purpose, string? mime)
    {
        Assert.Equal(GalleryCoverMediaType.Video, GalleryCoverMediaType.Resolve(purpose, mime));
    }

    [Theory]
    [InlineData("GALLERY_AREA_COVER", "image/jpeg")]
    [InlineData("GALLERY_AREA_COVER", null)]
    [InlineData(null, "image/png")]
    [InlineData(null, null)]
    public void Resolve_Returns_Image_For_Legacy_Image_Cover(string? purpose, string? mime)
    {
        Assert.Equal(GalleryCoverMediaType.Image, GalleryCoverMediaType.Resolve(purpose, mime));
    }

    [Fact]
    public void ResolveFor_Defaults_To_Image_When_No_Cover_Or_Metadata_Missing()
    {
        var byFileId = new Dictionary<ulong, (string? Purpose, string? Mime)>
        {
            [5] = ("GALLERY_AREA_COVER_VIDEO", "video/mp4"),
            [7] = ("GALLERY_AREA_COVER", "image/png"),
        };

        Assert.Equal(GalleryCoverMediaType.Image, GalleryCoverMediaType.ResolveFor(null, byFileId));      // no cover
        Assert.Equal(GalleryCoverMediaType.Image, GalleryCoverMediaType.ResolveFor(999, byFileId));       // missing metadata
        Assert.Equal(GalleryCoverMediaType.Video, GalleryCoverMediaType.ResolveFor(5, byFileId));         // video cover
        Assert.Equal(GalleryCoverMediaType.Image, GalleryCoverMediaType.ResolveFor(7, byFileId));         // image cover
    }
}
