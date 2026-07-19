using System;
using System.Collections.Generic;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Language codes for the bilingual gallery content. Exactly two, both mandatory on every item.
/// </summary>
public static class GalleryLanguages
{
    public const string Vietnamese = "vi";
    public const string English = "en";

    public static bool IsValid(string? code) =>
        code is Vietnamese or English;
}

/// <summary>
/// Shared validation for a gallery item's bilingual content (descriptions + the two audio recordings).
/// Centralises the "all four fields required" rule so Add / Edit stay in lock-step. Descriptions have no
/// length cap (the <c>description_vi/en</c> columns are TEXT). Audio binary rules (mp3/wav, ≤20 MB, magic
/// bytes) are enforced by the shared upload foundation via <c>FilePurpose.GalleryAudio</c>; here we only
/// guard presence + a non-blank description.
/// </summary>
public static class GalleryContentRules
{
    /// <summary>Trims a description and rejects a blank value with a field-specific code (no length cap).</summary>
    public static string NormalizeDescription(string? raw, bool vietnamese)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0)
            throw new BusinessRuleException(
                vietnamese ? "Vui lòng nhập mô tả tiếng Việt." : "Vui lòng nhập mô tả tiếng Anh.",
                vietnamese ? GalleryErrorCodes.DescriptionViRequired : GalleryErrorCodes.DescriptionEnRequired);
        return value;
    }

    /// <summary>Rejects a missing/empty audio upload for the given language with a field-specific code.</summary>
    public static GalleryUploadFileCommandDto RequireAudio(GalleryUploadFileCommandDto? audio, bool vietnamese)
    {
        if (audio is null || audio.Content is null || audio.Content.Length == 0 || audio.FileSize <= 0)
            throw new BusinessRuleException(
                vietnamese ? "Vui lòng chọn bản ghi âm tiếng Việt." : "Vui lòng chọn bản ghi âm tiếng Anh.",
                vietnamese ? GalleryErrorCodes.AudioViRequired : GalleryErrorCodes.AudioEnRequired);
        return audio;
    }
}
