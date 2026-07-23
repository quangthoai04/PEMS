using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Commands.PreviewGalleryLocationTranslation;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Galleries;

/// <summary>
/// Unit tests for the "Dịch sang EN" preview endpoint handler: Staff Leader scope (never public),
/// mode → field mapping (EXISTING_AREA = location only, NEW_AREA = area + location in ONE batch,
/// EDIT = per include flags), input validation, and provider failure → retryable 422 (never an empty
/// EN). The handler must write NOTHING — it has no DbContext dependency at all.
/// </summary>
public class PreviewGalleryLocationTranslationCommandHandlerTests
{
    private static Mock<ICurrentUserService> StaffLeader()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.UserId).Returns(10UL);
        user.SetupGet(u => u.RoleCode).Returns(RoleCodes.Staff);
        user.SetupGet(u => u.SubRole).Returns(UserSubRoles.Leader);
        user.SetupGet(u => u.PrimaryCampusId).Returns(1UL);
        return user;
    }

    private static Mock<IGalleryTranslationCoordinator> TranslatorReturning(
        System.Func<IReadOnlyList<GalleryTranslationRequest>, IReadOnlyList<GalleryTranslationResult>> map)
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

    [Fact]
    public async Task ExistingArea_Translates_Location_Only()
    {
        IReadOnlyList<GalleryTranslationRequest>? seen = null;
        var translator = TranslatorReturning(reqs => { seen = reqs; return SuccessResults(reqs); });
        var sut = new PreviewGalleryLocationTranslationCommandHandler(StaffLeader().Object, translator.Object);

        var result = await sut.Handle(
            new PreviewGalleryLocationTranslationCommand("EXISTING_AREA", null, "  Trước   tòa ", null, null),
            CancellationToken.None);

        Assert.NotNull(seen);
        Assert.Single(seen!);
        Assert.Equal("Trước tòa", seen![0].NormalizedSource); // normalized before the provider
        Assert.Null(result.Area);
        Assert.NotNull(result.Location);
        Assert.Equal("Trước tòa", result.Location!.SourceText);
        Assert.Equal("Trước tòa EN", result.Location.TranslatedText);
        Assert.Equal(TranslationSourceHasher.ComputeHash("Trước tòa"), result.Location.SourceHash);
    }

    [Fact]
    public async Task NewArea_Batches_Area_And_Location_In_One_Call()
    {
        var calls = 0;
        var translator = TranslatorReturning(reqs => { calls++; return SuccessResults(reqs); });
        var sut = new PreviewGalleryLocationTranslationCommandHandler(StaffLeader().Object, translator.Object);

        var result = await sut.Handle(
            new PreviewGalleryLocationTranslationCommand("NEW_AREA", "Tòa Alpha", "Trước tòa", null, null),
            CancellationToken.None);

        Assert.Equal(1, calls); // ONE batched provider request
        Assert.Equal("Tòa Alpha EN", result.Area!.TranslatedText);
        Assert.Equal("Trước tòa EN", result.Location!.TranslatedText);
    }

    [Fact]
    public async Task Edit_Respects_Include_Flags()
    {
        IReadOnlyList<GalleryTranslationRequest>? seen = null;
        var translator = TranslatorReturning(reqs => { seen = reqs; return SuccessResults(reqs); });
        var sut = new PreviewGalleryLocationTranslationCommandHandler(StaffLeader().Object, translator.Object);

        var result = await sut.Handle(
            new PreviewGalleryLocationTranslationCommand("EDIT", "Tòa nhà Alpha", null, true, false),
            CancellationToken.None);

        Assert.Single(seen!);
        Assert.Equal("Tòa nhà Alpha EN", result.Area!.TranslatedText);
        Assert.Null(result.Location);
    }

    [Fact]
    public async Task Edit_With_No_Includes_Throws_Preview_Empty()
    {
        var translator = new Mock<IGalleryTranslationCoordinator>(MockBehavior.Strict);
        var sut = new PreviewGalleryLocationTranslationCommandHandler(StaffLeader().Object, translator.Object);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.Handle(
            new PreviewGalleryLocationTranslationCommand("EDIT", "Tòa Alpha", "Trước tòa", false, false),
            CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.TranslationPreviewEmpty, ex.ErrorCode);
    }

    [Fact]
    public async Task Invalid_Mode_Throws()
    {
        var translator = new Mock<IGalleryTranslationCoordinator>(MockBehavior.Strict);
        var sut = new PreviewGalleryLocationTranslationCommandHandler(StaffLeader().Object, translator.Object);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.Handle(
            new PreviewGalleryLocationTranslationCommand("SOMETHING", "a", "b", null, null),
            CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.InvalidMode, ex.ErrorCode);
    }

    [Fact]
    public async Task Missing_Location_Vi_Throws()
    {
        var translator = new Mock<IGalleryTranslationCoordinator>(MockBehavior.Strict);
        var sut = new PreviewGalleryLocationTranslationCommandHandler(StaffLeader().Object, translator.Object);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.Handle(
            new PreviewGalleryLocationTranslationCommand("EXISTING_AREA", null, "   ", null, null),
            CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.LocationNameRequired, ex.ErrorCode);
    }

    [Fact]
    public async Task Missing_Area_Vi_On_NewArea_Throws()
    {
        var translator = new Mock<IGalleryTranslationCoordinator>(MockBehavior.Strict);
        var sut = new PreviewGalleryLocationTranslationCommandHandler(StaffLeader().Object, translator.Object);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.Handle(
            new PreviewGalleryLocationTranslationCommand("NEW_AREA", "", "Trước tòa", null, null),
            CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.AreaNameRequired, ex.ErrorCode);
    }

    [Fact]
    public async Task Provider_Failure_Throws_Retryable_Preview_Failed()
    {
        // The coordinator NEVER throws — it reports FAILED results; the preview must surface that
        // as a retryable error instead of returning an empty EN.
        var translator = TranslatorReturning(reqs => reqs.Select(r => new GalleryTranslationResult
        {
            SourceText = r.NormalizedSource,
            SourceHash = TranslationSourceHasher.ComputeHash(r.NormalizedSource),
            TranslatedText = null,
            Success = false,
        }).ToList());
        var sut = new PreviewGalleryLocationTranslationCommandHandler(StaffLeader().Object, translator.Object);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.Handle(
            new PreviewGalleryLocationTranslationCommand("EXISTING_AREA", null, "Trước tòa", null, null),
            CancellationToken.None));

        Assert.Equal(GalleryErrorCodes.TranslationPreviewFailed, ex.ErrorCode);
    }

    [Fact]
    public async Task Non_Staff_Leader_Is_Forbidden()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.RoleCode).Returns(RoleCodes.Student);
        var translator = new Mock<IGalleryTranslationCoordinator>(MockBehavior.Strict);
        var sut = new PreviewGalleryLocationTranslationCommandHandler(user.Object, translator.Object);

        await Assert.ThrowsAsync<AuthBusinessException>(() => sut.Handle(
            new PreviewGalleryLocationTranslationCommand("EXISTING_AREA", null, "Trước tòa", null, null),
            CancellationToken.None));
    }
}
