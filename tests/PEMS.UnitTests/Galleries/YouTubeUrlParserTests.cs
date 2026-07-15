using System;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Galleries.Common;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Unit tests for <see cref="YouTubeUrlParser"/> — the pure URL → canonical video-id logic behind the
/// gallery YouTube feature (host allow-list, id extraction from every supported URL form, rejection of
/// spoofed / malformed input). Covers the backend cases in the YouTube spec §28.1 (1-8).
/// </summary>
public class YouTubeUrlParserTests
{
    private const string Id = "dQw4w9WgXcQ"; // canonical 11-char id

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("  https://youtu.be/dQw4w9WgXcQ  ")]
    public void Parse_Accepts_Supported_Forms_And_Extracts_Id(string url)
    {
        var result = YouTubeUrlParser.Parse(url);

        Assert.Equal(Id, result.VideoId);
        Assert.Equal($"https://www.youtube.com/watch?v={Id}", result.WatchUrl);
        Assert.Equal($"https://www.youtube-nocookie.com/embed/{Id}", result.EmbedUrl);
        Assert.Equal($"https://i.ytimg.com/vi/{Id}/hqdefault.jpg", result.ThumbnailUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Rejects_Empty(string? url)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => YouTubeUrlParser.Parse(url!));
        Assert.Equal(GalleryErrorCodes.YoutubeUrlRequired, ex.ErrorCode);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    public void Parse_Rejects_Invalid_Url(string url)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => YouTubeUrlParser.Parse(url));
        Assert.Equal(GalleryErrorCodes.YoutubeUrlInvalid, ex.ErrorCode);
    }

    [Theory]
    [InlineData("https://vimeo.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com.attacker.com/watch?v=dQw4w9WgXcQ")] // spoofed subdomain
    [InlineData("https://notyoutube.com/watch?v=dQw4w9WgXcQ")]
    public void Parse_Rejects_Non_YouTube_Host(string url)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => YouTubeUrlParser.Parse(url));
        Assert.Equal(GalleryErrorCodes.YoutubeHostNotAllowed, ex.ErrorCode);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=short")]       // too short
    [InlineData("https://www.youtube.com/watch?v=waytoolongid1234")] // too long
    [InlineData("https://www.youtube.com/results?search_query=x")]   // no id
    [InlineData("https://youtu.be/")]                                // empty id
    public void Parse_Rejects_Bad_Video_Id(string url)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => YouTubeUrlParser.Parse(url));
        Assert.Equal(GalleryErrorCodes.YoutubeVideoIdInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Parse_Rejects_HtmlIframe_Input()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            YouTubeUrlParser.Parse("<iframe src=\"https://youtube.com/embed/dQw4w9WgXcQ\"></iframe>"));
        Assert.Equal(GalleryErrorCodes.YoutubeUrlInvalid, ex.ErrorCode);
    }
}
