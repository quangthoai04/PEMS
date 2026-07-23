using PEMS.Application.Galleries.Public.Common;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Public EN fallback matrix (BR §14.6/§26.11): the anonymous API exposes the English string ONLY when
/// translation_status is READY and the value is non-blank; every other state returns null so the
/// frontend falls back to Vietnamese. Raw translation metadata never reaches the public payload.
/// </summary>
public class PublicGalleryTranslationTests
{
    [Fact]
    public void Ready_With_Value_Returns_Trimmed_Value()
    {
        Assert.Equal("Alpha Building", PublicGalleryTranslation.EnOrNull("READY", "  Alpha Building  "));
    }

    [Theory]
    [InlineData("READY", null)]
    [InlineData("READY", "")]
    [InlineData("READY", "   ")]
    public void Ready_With_Blank_Returns_Null(string status, string? en)
    {
        Assert.Null(PublicGalleryTranslation.EnOrNull(status, en));
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("FAILED")]
    [InlineData("OUTDATED")]
    [InlineData(null)]
    [InlineData("")]
    public void Non_Ready_Status_Returns_Null_Even_With_Value(string? status)
    {
        Assert.Null(PublicGalleryTranslation.EnOrNull(status, "Alpha Building"));
    }
}
