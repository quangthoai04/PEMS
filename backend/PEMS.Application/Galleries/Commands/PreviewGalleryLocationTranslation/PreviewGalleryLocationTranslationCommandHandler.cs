using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.PreviewGalleryLocationTranslation;

/// <summary>
/// Read-only VI → EN preview for the Staff Leader area/location modals. Guards the Staff Leader campus
/// scope (this is NOT public), normalizes and validates the requested field(s), then makes exactly ONE
/// batched provider request. A provider failure (or a blank/over-cap result) surfaces as a retryable 422
/// — never a silent empty EN — and nothing is ever persisted here.
/// </summary>
public sealed class PreviewGalleryLocationTranslationCommandHandler
    : IRequestHandler<PreviewGalleryLocationTranslationCommand, GalleryLocationTranslationPreviewDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGalleryTranslationCoordinator _translator;

    public PreviewGalleryLocationTranslationCommandHandler(
        ICurrentUserService currentUser,
        IGalleryTranslationCoordinator translator)
    {
        _currentUser = currentUser;
        _translator = translator;
    }

    public async Task<GalleryLocationTranslationPreviewDto> Handle(
        PreviewGalleryLocationTranslationCommand request, CancellationToken cancellationToken)
    {
        StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);

        var mode = (request.Mode ?? string.Empty).Trim().ToUpperInvariant();
        bool includeArea, includeLocation;
        switch (mode)
        {
            case GalleryLocationModes.ExistingArea:
                includeArea = false;
                includeLocation = true;
                break;
            case GalleryLocationModes.NewArea:
                includeArea = true;
                includeLocation = true;
                break;
            case GalleryTranslationPreviewModes.Edit:
                includeArea = request.IncludeArea ?? false;
                includeLocation = request.IncludeLocation ?? false;
                break;
            default:
                throw new BusinessRuleException("Chế độ dịch không hợp lệ.", GalleryErrorCodes.InvalidMode);
        }

        if (!includeArea && !includeLocation)
            throw new BusinessRuleException(
                "Không có nội dung nào cần dịch.", GalleryErrorCodes.TranslationPreviewEmpty);

        var areaName = TranslationSourceNormalizer.Normalize(request.AreaNameVi);
        var locationName = TranslationSourceNormalizer.Normalize(request.LocationNameVi);

        if (includeArea && areaName.Length == 0)
            throw new BusinessRuleException(
                "Vui lòng nhập tên khu vực/tòa.", GalleryErrorCodes.AreaNameRequired);
        if (includeLocation && locationName.Length == 0)
            throw new BusinessRuleException(
                "Vui lòng nhập vị trí cụ thể.", GalleryErrorCodes.LocationNameRequired);

        // One batched provider request for everything this preview needs (area first, location second).
        var requests = new List<GalleryTranslationRequest>();
        if (includeArea)
            requests.Add(new GalleryTranslationRequest(areaName, GalleryTranslationLimits.NameEnMaxLength));
        if (includeLocation)
            requests.Add(new GalleryTranslationRequest(locationName, GalleryTranslationLimits.NameEnMaxLength));

        var results = await _translator.TranslateAsync(requests, cancellationToken);

        // Preview must return a usable EN for every requested field — a failure is a retryable error
        // (the modal keeps the VI/manual EN and offers retry), never an empty translation.
        foreach (var result in results)
        {
            if (!result.Success)
                throw new BusinessRuleException(
                    "Không thể dịch sang tiếng Anh lúc này. Vui lòng thử lại hoặc tự nhập bản tiếng Anh.",
                    GalleryErrorCodes.TranslationPreviewFailed);
        }

        var index = 0;
        var area = includeArea ? ToField(results[index++]) : null;
        var location = includeLocation ? ToField(results[index]) : null;

        return new GalleryLocationTranslationPreviewDto { Area = area, Location = location };
    }

    private static TranslationPreviewFieldDto ToField(GalleryTranslationResult result) => new()
    {
        SourceText = result.SourceText,
        SourceHash = result.SourceHash,
        TranslatedText = result.TranslatedText!,
    };
}
