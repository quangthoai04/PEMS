using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using PEMS.Api.Controllers;
using PEMS.Api.PublicGallery;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Galleries.Common;
using PEMS.Application.Galleries.Public.Common;
using PEMS.Application.Galleries.Public.Queries.GetPublicGalleryItemDetail;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// The Facebook/Open Graph preview of a public gallery item
/// (<c>GET /api/public/visit-fptu/share-preview/{campusCode}?locationId=&amp;itemId=</c>).
///
/// Two things must hold. First, the preview may never exist where the page does not: it reuses
/// <c>GetPublicGalleryItemDetailQuery</c>, so a hidden item — or one under an inactive
/// location/area/campus — 404s, and a hand-edited campus/location in the URL 404s too (otherwise the
/// card would advertise one item and the link open another). Second, what it emits must be safe and
/// usable: absolute https image URLs off the public media proxy, HTML-encoded text, never an mp4 as
/// the card image, and never an empty description.
/// </summary>
public class PublicGallerySharePreviewTests
{
    private const string FrontendBase = "https://www.pems-fpt.site";
    private const string CampusCode = "HN";
    private const long LocationId = 21;
    private const long ItemId = 105;

    // ── Fixtures ─────────────────────────────────────────────────────────────

    private static PublicGalleryMediaDto UploadedImage(ulong mediaId, ulong fileId, bool isPrimary = false) =>
        new()
        {
            MediaId = mediaId,
            FileId = fileId,
            MediaType = "IMAGE",
            SourceType = GalleryMediaSourceTypes.UploadedFile,
            Url = $"/api/public/visit-fptu/media/{fileId}/content",
            IsPrimary = isPrimary,
            DisplayOrder = (int)mediaId,
        };

    private static PublicGalleryMediaDto UploadedVideo(ulong mediaId, ulong fileId, bool isPrimary = false) =>
        new()
        {
            MediaId = mediaId,
            FileId = fileId,
            MediaType = "VIDEO",
            SourceType = GalleryMediaSourceTypes.UploadedFile,
            Url = $"/api/public/visit-fptu/media/{fileId}/content",
            IsPrimary = isPrimary,
            DisplayOrder = (int)mediaId,
        };

    private static PublicGalleryMediaDto YouTube(ulong mediaId, string? thumbnailUrl, bool isPrimary = false) =>
        new()
        {
            MediaId = mediaId,
            FileId = mediaId,
            MediaType = "VIDEO",
            SourceType = GalleryMediaSourceTypes.YouTube,
            Url = null,
            ThumbnailUrl = thumbnailUrl,
            YoutubeVideoId = "abcdefghijk",
            EmbedUrl = "https://www.youtube-nocookie.com/embed/abcdefghijk",
            IsPrimary = isPrimary,
            DisplayOrder = (int)mediaId,
        };

    private static PublicGalleryItemDetailDto Detail(
        string title = "Tượng 01",
        string description = "FPT là một trong những tập đoàn công nghệ hàng đầu Việt Nam.",
        string campusCode = CampusCode,
        ulong locationId = (ulong)LocationId,
        IReadOnlyList<PublicGalleryMediaDto>? media = null) =>
        new()
        {
            Campus = new PublicCampusDto
            {
                CampusId = 1, CampusCode = campusCode, CampusName = "FPTU Hà Nội", City = "Hà Nội",
            },
            Area = new PublicGalleryAreaSummaryDto { AreaId = 3, AreaName = "Tòa Demo 01" },
            Location = new PublicGalleryLocationSummaryDto { LocationId = locationId, LocationName = "Sảnh chính" },
            GalleryItem = new PublicGalleryItemSummaryDto
            {
                GalleryItemId = (ulong)ItemId,
                Title = title,
                Content = new PublicGalleryItemContentDto
                {
                    Vi = new PublicGalleryLanguageContentDto { Description = description },
                    En = new PublicGalleryLanguageContentDto { Description = "FPT is a leading tech corporation." },
                },
                MediaKind = "IMAGE",
                Status = "PUBLISHED",
            },
            Media = media ?? new[] { UploadedImage(1, 123, isPrimary: true) },
        };

    /// <summary>Controller wired to a mediator that answers with <paramref name="detail"/>.</summary>
    private static PublicVisitFptuController ControllerReturning(PublicGalleryItemDetailDto detail)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetPublicGalleryItemDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);
        return BuildController(mediator);
    }

    /// <summary>Controller wired to a mediator that reports the item as not public-visible (404).</summary>
    private static PublicVisitFptuController ControllerNotFound()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetPublicGalleryItemDetailQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("PublicGalleryItem", ItemId));
        return BuildController(mediator);
    }

    private static PublicVisitFptuController BuildController(Mock<IMediator> mediator)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:FrontendBaseUrl"] = FrontendBase })
            .Build();

        return new PublicVisitFptuController(mediator.Object, configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    /// <summary>Ids reach the endpoint as raw query strings (see the duplicate-param test below).</summary>
    private static string Q(long id) => id.ToString();

    private static async Task<ContentResult> PreviewOf(
        PublicGalleryItemDetailDto detail, string campusCode = CampusCode, long locationId = LocationId)
    {
        var controller = ControllerReturning(detail);
        var result = await controller.GetSharePreview(campusCode, Q(locationId), Q(ItemId), CancellationToken.None);
        return Assert.IsType<ContentResult>(result);
    }

    private static string ImageUrlOf(PublicGalleryItemDetailDto detail) =>
        PublicGallerySharePreviewBuilder.BuildMetadata(detail, FrontendBase, CampusCode, LocationId, ItemId).ImageUrl;

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_Of_Public_Item_Returns_Html_With_Every_Required_Open_Graph_Tag()
    {
        var content = await PreviewOf(Detail());

        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        var html = content.Content!;
        Assert.Contains("<meta property=\"og:type\" content=\"website\" />", html);
        Assert.Contains("<meta property=\"og:site_name\" content=\"PEMS - VisitFPTU\" />", html);
        Assert.Contains("<meta property=\"og:title\" content=\"Tượng 01\" />", html);
        Assert.Contains("og:description", html);
        Assert.Contains("FPT là một trong những tập đoàn", html);
        Assert.Contains(
            "<meta property=\"og:url\" content=\"https://www.pems-fpt.site/visit-fptu/hn?locationId=21&amp;itemId=105\" />",
            html);
        Assert.Contains(
            "<meta property=\"og:image\" content=\"https://www.pems-fpt.site/api/public/visit-fptu/media/123/content\" />",
            html);
        Assert.Contains("og:image:secure_url", html);
        Assert.Contains("og:image:alt", html);
        Assert.Contains("<meta property=\"og:locale\" content=\"vi_VN\" />", html);
        Assert.Contains("<meta property=\"og:locale:alternate\" content=\"en_US\" />", html);
        Assert.Contains(
            "<link rel=\"canonical\" href=\"https://www.pems-fpt.site/visit-fptu/hn?locationId=21&amp;itemId=105\" />",
            html);
    }

    [Fact]
    public async Task Preview_Is_Never_Cached_And_Never_Sniffed()
    {
        var controller = ControllerReturning(Detail());

        await controller.GetSharePreview(CampusCode, Q(LocationId), Q(ItemId), CancellationToken.None);

        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions.ToString());
    }

    [Fact]
    public async Task Preview_Body_Is_Not_The_Spa_Shell_But_A_Link_To_The_Canonical_Page()
    {
        var content = await PreviewOf(Detail());

        Assert.Contains("<a href=\"https://www.pems-fpt.site/visit-fptu/hn?locationId=21&amp;itemId=105\"", content.Content!);
        Assert.DoesNotContain("<script", content.Content!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Canonical_Url_Lowercases_The_Campus_Code_And_Carries_Both_Ids()
    {
        Assert.Equal(
            "https://www.pems-fpt.site/visit-fptu/hn?locationId=21&itemId=105",
            PublicGallerySharePreviewBuilder.BuildCanonicalUrl(FrontendBase + "/", "HN", LocationId, ItemId));
    }

    // ── Visibility (hidden item / inactive location, area, campus) ────────────

    // The public query answers 404 for every one of these — hidden or deleted item, inactive
    // location, inactive area, inactive campus — so one test states the rule for all four.
    [Fact]
    public async Task Item_That_Is_Not_Public_Visible_Has_No_Preview()
    {
        var controller = ControllerNotFound();

        var result = await controller.GetSharePreview(CampusCode, Q(LocationId), Q(ItemId), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Hidden_Item_Never_Falls_Back_To_A_Generic_Card()
    {
        var controller = ControllerNotFound();

        var result = await controller.GetSharePreview(CampusCode, Q(LocationId), Q(ItemId), CancellationToken.None);

        Assert.IsNotType<ContentResult>(result);
    }

    // ── Tamper: the URL must describe the item it opens ───────────────────────

    [Fact]
    public async Task Location_That_Does_Not_Own_The_Item_Is_Rejected()
    {
        var controller = ControllerReturning(Detail());

        var result = await controller.GetSharePreview(CampusCode, "999", Q(ItemId), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Campus_That_Does_Not_Own_The_Item_Is_Rejected()
    {
        var controller = ControllerReturning(Detail());

        var result = await controller.GetSharePreview("DN", Q(LocationId), Q(ItemId), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Campus_Code_Match_Ignores_Casing()
    {
        var controller = ControllerReturning(Detail());

        var result = await controller.GetSharePreview("hn", Q(LocationId), Q(ItemId), CancellationToken.None);

        Assert.IsType<ContentResult>(result);
    }

    [Theory]
    [InlineData(null, "105")]           // crawler hit /visit-fptu/hn with no item at all
    [InlineData("", "105")]
    [InlineData("0", "105")]
    [InlineData("-1", "105")]
    [InlineData("21", "0")]
    [InlineData("21", "-5")]
    [InlineData("21", "abc")]
    [InlineData("21", "105; DROP")]
    [InlineData("21,22", "105")]        // two DIFFERENT locations = tampering, not an edge rewrite
    public async Task Missing_Or_Malformed_Ids_Are_Rejected_Without_Touching_The_Query(
        string? locationId, string? itemId)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = BuildController(mediator);

        var result = await controller.GetSharePreview(CampusCode, locationId, itemId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        mediator.Verify(
            m => m.Send(It.IsAny<GetPublicGalleryItemDetailQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ids_Repeated_By_The_Vercel_Rewrite_Still_Produce_The_Preview()
    {
        // The rewrite destination carries locationId/itemId AND the crawler's original query is passed
        // through, so model binding can see "21,21" / "105,105". That is a valid crawl, not a bad request.
        var controller = ControllerReturning(Detail());

        var result = await controller.GetSharePreview(CampusCode, "21,21", "105,105", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Contains("locationId=21&amp;itemId=105", content.Content!);
    }

    // ── HTML escaping ────────────────────────────────────────────────────────

    [Fact]
    public async Task Dynamic_Text_Is_Html_Encoded_So_No_Markup_Can_Execute()
    {
        var detail = Detail(
            title: "A \"test\" <script>alert(1)</script>",
            description: "Ký tự < > & \" trong mô tả");

        var html = (await PreviewOf(detail)).Content!;

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&quot;test&quot;", html);
        // The quotes that delimit the meta attributes are the only raw ones left.
        Assert.DoesNotContain("content=\"Ký tự < > & \"", html);
    }

    // ── Description ──────────────────────────────────────────────────────────

    [Fact]
    public void Description_Whitespace_Is_Collapsed()
    {
        var detail = Detail(description: "  Dòng một\n\n  Dòng hai\t\tvà ba  ");

        var metadata = PublicGallerySharePreviewBuilder.BuildMetadata(
            detail, FrontendBase, CampusCode, LocationId, ItemId);

        Assert.Equal("Dòng một Dòng hai và ba", metadata.Description);
    }

    [Fact]
    public void Long_Description_Is_Truncated()
    {
        var detail = Detail(description: string.Join(" ", Enumerable.Repeat("từ", 300)));

        var metadata = PublicGallerySharePreviewBuilder.BuildMetadata(
            detail, FrontendBase, CampusCode, LocationId, ItemId);

        Assert.True(metadata.Description.Length <= 201, metadata.Description.Length.ToString());
        Assert.EndsWith("…", metadata.Description);
    }

    [Fact]
    public void Blank_Description_Falls_Back_To_The_Item_Location_Never_Empty()
    {
        var detail = Detail(description: "   ");

        var metadata = PublicGallerySharePreviewBuilder.BuildMetadata(
            detail, FrontendBase, CampusCode, LocationId, ItemId);

        Assert.Equal("Khám phá Sảnh chính tại FPTU Hà Nội trên VisitFPTU.", metadata.Description);
    }

    // ── Image selection ──────────────────────────────────────────────────────

    [Fact]
    public void Primary_Uploaded_Image_Becomes_The_Absolute_Public_Proxy_Url()
    {
        var detail = Detail(media: new[] { UploadedImage(1, 123, isPrimary: true), UploadedImage(2, 456) });

        Assert.Equal("https://www.pems-fpt.site/api/public/visit-fptu/media/123/content", ImageUrlOf(detail));
    }

    [Fact]
    public void Primary_YouTube_Media_Uses_Its_Thumbnail()
    {
        var detail = Detail(media: new[]
        {
            YouTube(1, "https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg", isPrimary: true),
        });

        Assert.Equal("https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg", ImageUrlOf(detail));
    }

    [Fact]
    public void Unusable_Primary_Falls_Through_To_The_Next_Image()
    {
        // Legacy uploaded video as primary: an mp4 can never be the card image.
        var detail = Detail(media: new[] { UploadedVideo(1, 111, isPrimary: true), UploadedImage(2, 456) });

        Assert.Equal("https://www.pems-fpt.site/api/public/visit-fptu/media/456/content", ImageUrlOf(detail));
    }

    [Fact]
    public void YouTube_Thumbnail_Is_Used_When_No_Image_Media_Exists()
    {
        var detail = Detail(media: new[]
        {
            UploadedVideo(1, 111, isPrimary: true),
            YouTube(2, "https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg"),
        });

        Assert.Equal("https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg", ImageUrlOf(detail));
    }

    [Fact]
    public void Non_Https_YouTube_Thumbnail_Is_Ignored()
    {
        var detail = Detail(media: new[] { YouTube(1, "http://i.ytimg.com/vi/x/hqdefault.jpg", isPrimary: true) });

        Assert.Equal("https://www.pems-fpt.site/og/gallery-default.jpg", ImageUrlOf(detail));
    }

    [Fact]
    public void Item_Without_Any_Usable_Image_Falls_Back_To_The_Static_Site_Image()
    {
        var detail = Detail(media: new[] { UploadedVideo(1, 111, isPrimary: true), YouTube(2, null) });

        Assert.Equal("https://www.pems-fpt.site/og/gallery-default.jpg", ImageUrlOf(detail));
    }

    [Fact]
    public void Image_Url_Is_Rebuilt_From_The_Trusted_FileId_Not_The_Dto_Url_String()
    {
        var detail = Detail(media: new[]
        {
            new PublicGalleryMediaDto
            {
                MediaId = 1,
                FileId = 123,
                MediaType = "IMAGE",
                SourceType = GalleryMediaSourceTypes.UploadedFile,
                Url = "https://drive.google.com/uc?id=leaked",
                IsPrimary = true,
            },
        });

        Assert.Equal("https://www.pems-fpt.site/api/public/visit-fptu/media/123/content", ImageUrlOf(detail));
    }
}
