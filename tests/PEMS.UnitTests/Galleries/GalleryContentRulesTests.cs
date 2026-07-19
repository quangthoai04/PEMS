using PEMS.Application.Common.Exceptions;
using PEMS.Application.Galleries.Common;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Unit tests for <see cref="GalleryContentRules"/> — the shared "all four bilingual fields are
/// mandatory" logic that replaced the EverAI TTS narration. Covers the acceptance-criteria validation
/// matrix: each description is required + trimmed + length-capped with a field-specific error code, and
/// each audio recording must be a non-empty upload. Also checks <see cref="GalleryLanguages"/>.
/// </summary>
public class GalleryContentRulesTests
{
    // ── Description (VI) ──

    [Fact]
    public void NormalizeDescription_Vi_Trims_And_Returns_Value()
    {
        Assert.Equal("Xin chào", GalleryContentRules.NormalizeDescription("  Xin chào  ", vietnamese: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void NormalizeDescription_Vi_Blank_Throws_DescriptionViRequired(string? raw)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => GalleryContentRules.NormalizeDescription(raw, vietnamese: true));
        Assert.Equal(GalleryErrorCodes.DescriptionViRequired, ex.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeDescription_En_Blank_Throws_DescriptionEnRequired(string? raw)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => GalleryContentRules.NormalizeDescription(raw, vietnamese: false));
        Assert.Equal(GalleryErrorCodes.DescriptionEnRequired, ex.ErrorCode);
    }

    [Fact]
    public void NormalizeDescription_Has_No_Length_Cap()
    {
        // Descriptions are TEXT columns — a very long value is accepted (no 1000-char limit).
        var veryLong = new string('a', 5000);
        Assert.Equal(veryLong, GalleryContentRules.NormalizeDescription(veryLong, vietnamese: true));
    }

    // ── Audio presence ──

    [Fact]
    public void RequireAudio_Vi_Null_Throws_AudioViRequired()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => GalleryContentRules.RequireAudio(null, vietnamese: true));
        Assert.Equal(GalleryErrorCodes.AudioViRequired, ex.ErrorCode);
    }

    [Fact]
    public void RequireAudio_En_Null_Throws_AudioEnRequired()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => GalleryContentRules.RequireAudio(null, vietnamese: false));
        Assert.Equal(GalleryErrorCodes.AudioEnRequired, ex.ErrorCode);
    }

    [Fact]
    public void RequireAudio_Empty_Content_Throws()
    {
        var empty = new GalleryUploadFileCommandDto(System.Array.Empty<byte>(), "a.mp3", "audio/mpeg", 0, null, null);
        var ex = Assert.Throws<BusinessRuleException>(() => GalleryContentRules.RequireAudio(empty, vietnamese: true));
        Assert.Equal(GalleryErrorCodes.AudioViRequired, ex.ErrorCode);
    }

    [Fact]
    public void RequireAudio_Valid_Upload_Is_Returned()
    {
        var audio = new GalleryUploadFileCommandDto(new byte[] { 1, 2, 3 }, "vi.mp3", "audio/mpeg", 3, null, null);
        Assert.Same(audio, GalleryContentRules.RequireAudio(audio, vietnamese: true));
    }

    // ── Language codes ──

    [Theory]
    [InlineData("vi", true)]
    [InlineData("en", true)]
    [InlineData("fr", false)]
    [InlineData("VI", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void GalleryLanguages_IsValid(string? code, bool expected)
    {
        Assert.Equal(expected, GalleryLanguages.IsValid(code));
    }
}
