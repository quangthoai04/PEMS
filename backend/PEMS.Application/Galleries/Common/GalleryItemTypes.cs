using System;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// The content type of a gallery item — distinct from <c>media_kind</c> (IMAGE/VIDEO/MIXED, computed
/// from the files). <see cref="Media"/> = ảnh/video giới thiệu vị trí; <see cref="VisitDelegation"/> =
/// ảnh/video đoàn khách đã tới thăm. Stored in <c>gallery_items.item_type</c>. This phase does NOT link
/// VISIT_DELEGATION items to any visit instance.
/// </summary>
public static class GalleryItemTypes
{
    public const string Media = "MEDIA";
    public const string VisitDelegation = "VISIT_DELEGATION";

    /// <summary>
    /// Normalizes a client-supplied item type to its canonical DB value, throwing a controlled 422 when
    /// it is missing (<see cref="GalleryErrorCodes.ItemTypeRequired"/>) or not one of the two allowed
    /// values (<see cref="GalleryErrorCodes.ItemTypeInvalid"/>).
    /// </summary>
    public static string Normalize(string? itemType)
    {
        if (string.IsNullOrWhiteSpace(itemType))
            throw new BusinessRuleException("Vui lòng chọn loại nội dung.", GalleryErrorCodes.ItemTypeRequired);

        var value = itemType.Trim().ToUpperInvariant();
        if (value is not (Media or VisitDelegation))
            throw new BusinessRuleException(
                "Vui lòng chọn Media hoặc Đoàn khách.", GalleryErrorCodes.ItemTypeInvalid);
        return value;
    }

    /// <summary>Vietnamese UI label for a stored item type value (defaults to "Media" for legacy rows).</summary>
    public static string Label(string? itemType) =>
        string.Equals(itemType, VisitDelegation, StringComparison.OrdinalIgnoreCase) ? "Đoàn khách" : "Media";
}
