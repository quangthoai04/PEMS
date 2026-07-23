using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.BackfillGalleryTranslations;

/// <summary>
/// Backfills gallery_areas / gallery_locations / gallery_items translations for the caller's campus.
/// Row selection (BR §22.3): EN NULL, or translation_status in (PENDING, FAILED, OUTDATED), or the
/// stored source hash is missing / does not match the current Vietnamese source. Rows that are READY +
/// hash-matched + EN present are skipped (idempotent). Sources are deduplicated and sent to the provider
/// in chunks of at most <see cref="ProviderBatchSize"/> strings per request; results are saved per chunk
/// so a mid-run provider failure keeps everything translated so far. Only success/failure COUNTS are
/// logged — never credentials or payloads.
/// </summary>
public sealed class BackfillGalleryTranslationsCommandHandler
    : IRequestHandler<BackfillGalleryTranslationsCommand, BackfillGalleryTranslationsResponse>
{
    private const int ProviderBatchSize = 50;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IGalleryTranslationCoordinator _translator;
    private readonly IDateTimeService _clock;
    private readonly ILogger<BackfillGalleryTranslationsCommandHandler> _logger;

    public BackfillGalleryTranslationsCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IGalleryTranslationCoordinator translator,
        IDateTimeService clock,
        ILogger<BackfillGalleryTranslationsCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _translator = translator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BackfillGalleryTranslationsResponse> Handle(
        BackfillGalleryTranslationsCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var now = _clock.VietnamNow;

        // Tracked loads — the selected rows are updated in place. Campus-scoped only.
        var areas = await _db.GalleryAreas
            .Where(a => a.CampusId == campusId)
            .ToListAsync(cancellationToken);
        var locations = await _db.GalleryLocations
            .Where(l => l.Area.CampusId == campusId)
            .ToListAsync(cancellationToken);
        var items = await _db.GalleryItems
            .Where(i => i.DeletedAt == null && i.Location.Area.CampusId == campusId)
            .ToListAsync(cancellationToken);

        // One flat unit list so areas/locations/items share the chunking + apply loop.
        var units = new List<BackfillUnit>();
        units.AddRange(areas.Select(a => new BackfillUnit(
            TranslationSourceNormalizer.Normalize(a.AreaName), GalleryTranslationLimits.NameEnMaxLength,
            a.TranslationStatus, a.TranslationSourceHash, a.AreaNameEn,
            result => GalleryTranslationApplier.Apply(a, result, now))));
        units.AddRange(locations.Select(l => new BackfillUnit(
            TranslationSourceNormalizer.Normalize(l.LocationName), GalleryTranslationLimits.NameEnMaxLength,
            l.TranslationStatus, l.TranslationSourceHash, l.LocationNameEn,
            result => GalleryTranslationApplier.Apply(l, result, now))));
        units.AddRange(items.Select(i => new BackfillUnit(
            TranslationSourceNormalizer.Normalize(i.Title), GalleryTranslationLimits.TitleEnMaxLength,
            i.TranslationStatus, i.TranslationSourceHash, i.TitleEn,
            result => GalleryTranslationApplier.Apply(i, result, now))));

        var scanned = units.Count;

        // Idempotency: skip everything already READY + hash-matched + EN present (BR §22.5).
        var pending = units
            .Where(u => u.NormalizedSource.Length > 0
                        && !GalleryTranslationApplier.IsUpToDate(
                            u.TranslationStatus, u.StoredHash, u.StoredEn, u.NormalizedSource))
            .ToList();

        var translated = 0;
        var failed = 0;

        // Chunk by DISTINCT source so one provider request never exceeds ProviderBatchSize strings;
        // every unit whose source is in the chunk gets its result applied, then the chunk is saved.
        var distinctSources = pending.Select(p => p.NormalizedSource)
            .Distinct(System.StringComparer.Ordinal)
            .ToList();

        for (var offset = 0; offset < distinctSources.Count; offset += ProviderBatchSize)
        {
            var chunkSources = distinctSources.Skip(offset).Take(ProviderBatchSize)
                .ToHashSet(System.StringComparer.Ordinal);
            var chunkUnits = pending.Where(p => chunkSources.Contains(p.NormalizedSource)).ToList();

            var requests = chunkUnits
                .Select(u => new GalleryTranslationRequest(u.NormalizedSource, u.MaxLength))
                .ToList();
            var results = await _translator.TranslateAsync(requests, cancellationToken);

            for (var i = 0; i < chunkUnits.Count; i++)
            {
                chunkUnits[i].Apply(results[i]);
                if (results[i].Success) translated++;
                else failed++;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campusId,
            Action = "BACKFILL_GALLERY_TRANSLATIONS",
            EntityType = "GalleryTranslation",
            EntityId = campusId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "Summary",
                    NewValueText = $"scanned={scanned};skipped={scanned - pending.Count};translated={translated};failed={failed}",
                },
            },
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Gallery translation backfill (campus {CampusId}): scanned={Scanned} skipped={Skipped} translated={Translated} failed={Failed}",
            campusId, scanned, scanned - pending.Count, translated, failed);

        return new BackfillGalleryTranslationsResponse
        {
            Scanned = scanned,
            Skipped = scanned - pending.Count,
            Translated = translated,
            Failed = failed,
            Message = failed == 0
                ? $"Đã backfill bản dịch: {translated} mục được dịch, {scanned - pending.Count} mục bỏ qua."
                : $"Backfill hoàn tất: {translated} mục được dịch, {failed} mục lỗi (có thể chạy lại sau).",
        };
    }

    /// <summary>One entity's translatable field flattened for the shared chunk/apply loop.</summary>
    private sealed class BackfillUnit
    {
        public BackfillUnit(
            string normalizedSource, int maxLength,
            string? translationStatus, string? storedHash, string? storedEn,
            System.Action<GalleryTranslationResult> apply)
        {
            NormalizedSource = normalizedSource;
            MaxLength = maxLength;
            TranslationStatus = translationStatus;
            StoredHash = storedHash;
            StoredEn = storedEn;
            Apply = apply;
        }

        public string NormalizedSource { get; }
        public int MaxLength { get; }
        public string? TranslationStatus { get; }
        public string? StoredHash { get; }
        public string? StoredEn { get; }
        public System.Action<GalleryTranslationResult> Apply { get; }
    }
}
