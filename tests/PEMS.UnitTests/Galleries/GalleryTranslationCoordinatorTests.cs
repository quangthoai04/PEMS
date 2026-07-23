using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Galleries.Common;
using PEMS.Application.Translation;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Unit tests for <see cref="GalleryTranslationCoordinator"/> — the single seam between the Gallery
/// write path and the translation provider. Covers: one batched provider call per business action,
/// deduplication of identical sources, order preservation, provider failure → FAILED (never throws),
/// invalid results (blank / over the EN column cap / count mismatch) → FAILED without truncation, and
/// caller-cancellation propagation.
/// </summary>
public class GalleryTranslationCoordinatorTests
{
    private static GalleryTranslationCoordinator CreateSut(Mock<IContentTranslationService> provider)
        => new(provider.Object, NullLogger<GalleryTranslationCoordinator>.Instance);

    private static Mock<IContentTranslationService> ProviderReturning(
        Func<IReadOnlyList<string>, IReadOnlyList<string>> map)
    {
        var provider = new Mock<IContentTranslationService>(MockBehavior.Strict);
        provider
            .Setup(p => p.TranslateTextAsync(
                It.IsAny<IReadOnlyList<string>>(), "vi", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> contents, string _, string _, CancellationToken _) => map(contents));
        return provider;
    }

    [Fact]
    public async Task Success_Maps_In_Order_With_Ready_Status_And_Hash()
    {
        var provider = ProviderReturning(contents => contents.Select(c => c + " EN").ToList());
        var sut = CreateSut(provider);

        var results = await sut.TranslateAsync(
            new[]
            {
                new GalleryTranslationRequest("Tòa Alpha", 255),
                new GalleryTranslationRequest("Trước tòa", 255),
            },
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Success);
        Assert.Equal("Tòa Alpha EN", results[0].TranslatedText);
        Assert.Equal(GalleryTranslationStatuses.Ready, results[0].Status);
        Assert.Equal(TranslationSourceHasher.ComputeHash("Tòa Alpha"), results[0].SourceHash);
        Assert.Equal("Trước tòa EN", results[1].TranslatedText);
    }

    [Fact]
    public async Task Batch_Makes_Exactly_One_Provider_Call()
    {
        var provider = ProviderReturning(contents => contents.Select(c => c + " EN").ToList());
        var sut = CreateSut(provider);

        await sut.TranslateAsync(
            new[]
            {
                new GalleryTranslationRequest("Tòa Alpha", 255),
                new GalleryTranslationRequest("Trước tòa", 255),
            },
            CancellationToken.None);

        provider.Verify(p => p.TranslateTextAsync(
            It.Is<IReadOnlyList<string>>(c => c.Count == 2 && c[0] == "Tòa Alpha" && c[1] == "Trước tòa"),
            "vi", "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Duplicate_Sources_Are_Deduplicated_And_Share_The_Result()
    {
        var provider = ProviderReturning(contents => contents.Select(c => c + " EN").ToList());
        var sut = CreateSut(provider);

        var results = await sut.TranslateAsync(
            new[]
            {
                new GalleryTranslationRequest("Alpha", 255),
                new GalleryTranslationRequest("Alpha", 255),
            },
            CancellationToken.None);

        // ONE provider request containing ONE deduplicated string…
        provider.Verify(p => p.TranslateTextAsync(
            It.Is<IReadOnlyList<string>>(c => c.Count == 1 && c[0] == "Alpha"),
            "vi", "en", It.IsAny<CancellationToken>()), Times.Once);
        // …mapped back onto BOTH requests.
        Assert.Equal("Alpha EN", results[0].TranslatedText);
        Assert.Equal("Alpha EN", results[1].TranslatedText);
    }

    [Fact]
    public async Task Provider_Exception_Marks_All_Failed_And_Never_Throws()
    {
        var provider = new Mock<IContentTranslationService>();
        provider
            .Setup(p => p.TranslateTextAsync(
                It.IsAny<IReadOnlyList<string>>(), "vi", "en", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider down"));
        var sut = CreateSut(provider);

        var results = await sut.TranslateAsync(
            new[] { new GalleryTranslationRequest("Tòa Alpha", 255) }, CancellationToken.None);

        Assert.False(results[0].Success);
        Assert.Null(results[0].TranslatedText);
        Assert.Equal(GalleryTranslationStatuses.Failed, results[0].Status);
        // The hash of the NEW source is still recorded so a later retry knows what failed.
        Assert.Equal(TranslationSourceHasher.ComputeHash("Tòa Alpha"), results[0].SourceHash);
    }

    [Fact]
    public async Task Result_Count_Mismatch_Marks_All_Failed()
    {
        var provider = ProviderReturning(_ => new List<string>()); // wrong count
        var sut = CreateSut(provider);

        var results = await sut.TranslateAsync(
            new[] { new GalleryTranslationRequest("Tòa Alpha", 255) }, CancellationToken.None);

        Assert.False(results[0].Success);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_Translated_Text_Is_Failed(string blank)
    {
        var provider = ProviderReturning(contents => contents.Select(_ => blank).ToList());
        var sut = CreateSut(provider);

        var results = await sut.TranslateAsync(
            new[] { new GalleryTranslationRequest("Tòa Alpha", 255) }, CancellationToken.None);

        Assert.False(results[0].Success);
        Assert.Null(results[0].TranslatedText);
    }

    [Fact]
    public async Task Over_Column_Cap_Is_Failed_Never_Truncated()
    {
        var provider = ProviderReturning(contents => contents.Select(_ => new string('x', 300)).ToList());
        var sut = CreateSut(provider);

        var results = await sut.TranslateAsync(
            new[] { new GalleryTranslationRequest("Tòa Alpha", 255) }, CancellationToken.None);

        Assert.False(results[0].Success);
        Assert.Null(results[0].TranslatedText); // never a truncated string
    }

    [Fact]
    public async Task Translated_Text_Is_Trimmed()
    {
        var provider = ProviderReturning(contents => contents.Select(c => "  Alpha Building  ").ToList());
        var sut = CreateSut(provider);

        var results = await sut.TranslateAsync(
            new[] { new GalleryTranslationRequest("Tòa Alpha", 255) }, CancellationToken.None);

        Assert.Equal("Alpha Building", results[0].TranslatedText);
    }

    [Fact]
    public async Task Empty_Request_List_Makes_No_Provider_Call()
    {
        var provider = new Mock<IContentTranslationService>(MockBehavior.Strict);
        var sut = CreateSut(provider);

        var results = await sut.TranslateAsync(
            Array.Empty<GalleryTranslationRequest>(), CancellationToken.None);

        Assert.Empty(results);
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Caller_Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var provider = new Mock<IContentTranslationService>();
        provider
            .Setup(p => p.TranslateTextAsync(
                It.IsAny<IReadOnlyList<string>>(), "vi", "en", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var sut = CreateSut(provider);

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.TranslateAsync(
            new[] { new GalleryTranslationRequest("Tòa Alpha", 255) }, cts.Token));
    }
}
