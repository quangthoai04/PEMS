using MediatR;

namespace PEMS.Application.Galleries.Commands.BackfillGalleryTranslations;

/// <summary>
/// One-off, deliberately-triggered backfill of missing/outdated Gallery translations for the CALLER'S
/// campus (Staff Leader scope). Never runs automatically at startup and is never reachable anonymously.
/// Idempotent: rows that are READY with a matching source hash and a non-blank EN value are skipped, so
/// re-running is always safe.
/// </summary>
public sealed record BackfillGalleryTranslationsCommand : IRequest<BackfillGalleryTranslationsResponse>;

public sealed class BackfillGalleryTranslationsResponse
{
    public int Scanned { get; init; }
    public int Skipped { get; init; }
    public int Translated { get; init; }
    public int Failed { get; init; }
    public string Message { get; init; } = string.Empty;
}
