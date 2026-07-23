using MediatR;

namespace PEMS.Application.Galleries.Commands.RetryGalleryTranslation;

/// <summary>
/// "Dịch lại" — re-runs the VI → EN auto-translation for ONE gallery entity's translated field only
/// (AREA → area_name, LOCATION → location_name, ITEM → title). Never touches covers, media, audio,
/// descriptions, status, display order or relations. Staff Leader, campus-scoped.
/// </summary>
public sealed record RetryGalleryTranslationCommand(string EntityType, long EntityId)
    : IRequest<RetryGalleryTranslationResponse>;

/// <summary>Allowed <see cref="RetryGalleryTranslationCommand.EntityType"/> values.</summary>
public static class GalleryTranslationEntityTypes
{
    public const string Area = "AREA";
    public const string Location = "LOCATION";
    public const string Item = "ITEM";
}

public sealed class RetryGalleryTranslationResponse
{
    public string EntityType { get; init; } = string.Empty;
    public ulong EntityId { get; init; }
    /// <summary>READY or FAILED after this retry.</summary>
    public string TranslationStatus { get; init; } = string.Empty;
    /// <summary>The persisted English text (null when the retry failed).</summary>
    public string? TranslatedText { get; init; }
    public string Message { get; init; } = string.Empty;
}
