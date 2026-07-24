using System.Collections.Generic;
using System.Linq;
using FluentValidation.TestHelper;
using PEMS.Application.Delegations.VisitPhotos.Commands.UploadVisitInstancePhotos;
using Xunit;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// The per-request count cap is part of the canonical visit-photo contract: at most 10 images per
/// upload. The validator is the authority the frontend mirrors — it must reject 11 and accept 10, and
/// reject an empty selection.
/// </summary>
public sealed class UploadVisitInstancePhotosCommandValidatorTests
{
    private readonly UploadVisitInstancePhotosCommandValidator _validator = new();

    private static UploadVisitInstancePhotoFile Photo(int i)
        => new(new byte[] { 0xFF, 0xD8, 0xFF }, $"p{i}.jpg", "image/jpeg");

    private static UploadVisitInstancePhotosCommand Command(int fileCount) => new()
    {
        VisitInstanceId = 1,
        Files = Enumerable.Range(0, fileCount).Select(Photo).ToList(),
    };

    [Fact]
    public void Eleven_files_is_rejected()
    {
        var result = _validator.TestValidate(Command(11));
        result.ShouldHaveValidationErrorFor(c => c.Files);
    }

    [Fact]
    public void Ten_files_is_accepted()
    {
        var result = _validator.TestValidate(Command(10));
        result.ShouldNotHaveValidationErrorFor(c => c.Files);
    }

    [Fact]
    public void An_empty_selection_is_rejected()
    {
        var result = _validator.TestValidate(new UploadVisitInstancePhotosCommand
        {
            VisitInstanceId = 1,
            Files = new List<UploadVisitInstancePhotoFile>(),
        });
        result.ShouldHaveValidationErrorFor(c => c.Files);
    }

    [Fact]
    public void The_cap_is_ten()
    {
        Assert.Equal(10, UploadVisitInstancePhotosCommandValidator.MaxFilesPerUpload);
    }
}
