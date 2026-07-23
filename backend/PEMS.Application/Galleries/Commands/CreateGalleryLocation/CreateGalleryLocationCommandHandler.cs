using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.CreateGalleryLocation;

/// <summary>
/// UC-LOC-04 / UC-LOC-05 handler. Validates role/scope, then either inserts a location under an existing
/// ACTIVE area in the caller's campus, or creates a brand-new area plus its first location atomically.
/// Names are normalized to keys so near-duplicates ("TÒA DELTA" vs "toa delta") are rejected (HTTP 409).
/// A location always requires a cover image; a brand-new area requires its own cover image too. New
/// areas/locations default to ACTIVE; no gallery item is created.
/// Translation: an EN carried by the payload (a "Dịch sang EN" preview with a matching source hash, or a
/// MANUAL edit) is persisted WITHOUT calling the provider again; a stale preview fails with
/// GALLERY_TRANSLATION_PREVIEW_STALE. Names with no usable EN are auto-translated in ONE batch (new area
/// + location = one provider request); a provider failure never blocks the create — the entity is saved
/// with EN = NULL / FAILED and the response carries a translation warning.
/// </summary>
public sealed class CreateGalleryLocationCommandHandler
    : IRequestHandler<CreateGalleryLocationCommand, GalleryLocationDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IGalleryTranslationCoordinator _translator;
    private readonly IDateTimeService _clock;

    public CreateGalleryLocationCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IGalleryTranslationCoordinator translator,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _translator = translator;
        _clock = clock;
    }

    public async Task<GalleryLocationDetailDto> Handle(
        CreateGalleryLocationCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var now = _clock.VietnamNow;

        var mode = (request.Mode ?? string.Empty).Trim().ToUpperInvariant();
        var locationName = GalleryKeyNormalizer.CleanName(request.LocationName);
        if (locationName.Length == 0)
            throw new BusinessRuleException("Vui lòng nhập vị trí cụ thể.", GalleryErrorCodes.LocationNameRequired);
        var locationKey = GalleryKeyNormalizer.ToKey(locationName);

        // Every location needs exactly one cover image (BR-LOCATION-COVER-01).
        if (request.LocationCoverImage is null)
            throw new BusinessRuleException(
                "Vui lòng upload ảnh đại diện vị trí.", GalleryErrorCodes.LocationCoverRequired);

        ulong areaId;
        string? auditNewArea = null;
        ulong? auditAreaCoverFileId = null;
        var translationFailed = false;
        string? auditTranslationStatus = null;

        // A previewed (AUTO_PREVIEW + matching hash) or manual EN is reused as-is — the provider is
        // NOT called again for that field. A stale preview throws PREVIEW_STALE before any upload.
        var resolvedLocation = GalleryPreviewedTranslation.TryResolve(
            locationName, request.LocationNameEn, request.LocationTranslationOrigin,
            request.LocationTranslationSourceHash, GalleryTranslationLimits.NameEnMaxLength);

        if (mode == GalleryLocationModes.ExistingArea)
        {
            if (request.AreaId is not { } rawAreaId || rawAreaId <= 0)
                throw new BusinessRuleException("Vui lòng chọn khu vực/tòa.", GalleryErrorCodes.AreaRequired);

            var area = await GalleryLocationWriteGuard.LoadAreaInCampusAsync(_db, (ulong)rawAreaId, campusId, cancellationToken);
            if (area.Status != EntityStatuses.Active)
                throw new BusinessRuleException("Khu vực này đang ngừng hoạt động.", GalleryErrorCodes.AreaInactive);

            areaId = area.AreaId;
            await GalleryLocationWriteGuard.EnsureLocationKeyFreeAsync(_db, areaId, locationKey, null, cancellationToken);

            var locationCoverId = await GalleryCoverImage.UploadAsync(
                _fileUpload, request.LocationCoverImage, isArea: false, actorId, cancellationToken);

            // Translate ONLY the new location name (never re-translate the existing area) — and only
            // when no previewed/manual EN was supplied. The coordinator never throws for provider
            // errors — a failure saves VI with EN = NULL / FAILED.
            var locationTranslation = resolvedLocation?.Result
                ?? (await _translator.TranslateAsync(
                    new[] { new GalleryTranslationRequest(locationName, GalleryTranslationLimits.NameEnMaxLength) },
                    cancellationToken))[0];
            translationFailed = !locationTranslation.Success;
            auditTranslationStatus = locationTranslation.Status;

            var newLocation = new GalleryLocation
            {
                AreaId = areaId,
                LocationName = locationName,
                LocationKey = locationKey,
                CoverFileId = locationCoverId,
                Status = EntityStatuses.Active,
                DisplayOrder = 0,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            GalleryTranslationApplier.Apply(newLocation, locationTranslation, now,
                resolvedLocation?.TranslationSource ?? GalleryTranslationSources.Auto);
            _db.GalleryLocations.Add(newLocation);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (mode == GalleryLocationModes.NewArea)
        {
            var areaName = GalleryKeyNormalizer.CleanName(request.NewAreaName);
            if (areaName.Length == 0)
                throw new BusinessRuleException("Vui lòng nhập tên khu vực/tòa mới.", GalleryErrorCodes.NewAreaNameRequired);
            var areaKey = GalleryKeyNormalizer.ToKey(areaName);
            auditNewArea = areaName;

            // A brand-new area needs its own MP4 cover video (the Area Showcase background).
            if (request.AreaCoverVideo is null)
                throw new BusinessRuleException(
                    "Vui lòng chọn một video đại diện cho khu vực.", GalleryErrorCodes.AreaCoverVideoRequired);

            // Reject a duplicate area key BEFORE uploading so the common case never orphans a Drive file.
            await GalleryLocationWriteGuard.EnsureAreaKeyFreeAsync(_db, campusId, areaKey, cancellationToken);

            // Resolve the previewed/manual area EN BEFORE uploading (a stale preview must fail fast).
            var resolvedArea = GalleryPreviewedTranslation.TryResolve(
                areaName, request.NewAreaNameEn, request.AreaTranslationOrigin,
                request.AreaTranslationSourceHash, GalleryTranslationLimits.NameEnMaxLength);

            var areaCoverId = await GalleryAreaCoverVideo.UploadAsync(
                _fileUpload, request.AreaCoverVideo, actorId, cancellationToken);
            auditAreaCoverFileId = areaCoverId;
            var locationCoverId = await GalleryCoverImage.UploadAsync(
                _fileUpload, request.LocationCoverImage, isArea: false, actorId, cancellationToken);

            // Names with no usable previewed/manual EN → ONE batched provider request (deduplicated
            // when equal). Failure never rolls anything back — FAILED metadata is saved.
            var providerRequests = new List<GalleryTranslationRequest>();
            if (resolvedArea is null)
                providerRequests.Add(new GalleryTranslationRequest(areaName, GalleryTranslationLimits.NameEnMaxLength));
            if (resolvedLocation is null)
                providerRequests.Add(new GalleryTranslationRequest(locationName, GalleryTranslationLimits.NameEnMaxLength));
            var providerResults = providerRequests.Count > 0
                ? await _translator.TranslateAsync(providerRequests, cancellationToken)
                : System.Array.Empty<GalleryTranslationResult>();
            var providerIndex = 0;
            var areaTranslation = resolvedArea?.Result ?? providerResults[providerIndex++];
            var locationTranslation = resolvedLocation?.Result ?? providerResults[providerIndex];
            translationFailed = !areaTranslation.Success || !locationTranslation.Success;
            auditTranslationStatus = translationFailed
                ? GalleryTranslationStatuses.Failed : GalleryTranslationStatuses.Ready;

            // Area + first location must commit together (UC §19.4).
            await using var tx = await _db.BeginTransactionAsync(cancellationToken);
            try
            {
                await GalleryLocationWriteGuard.EnsureAreaKeyFreeAsync(_db, campusId, areaKey, cancellationToken);

                var area = new GalleryArea
                {
                    CampusId = campusId,
                    AreaName = areaName,
                    AreaKey = areaKey,
                    CoverFileId = areaCoverId,
                    Status = EntityStatuses.Active,
                    DisplayOrder = 0,
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                GalleryTranslationApplier.Apply(area, areaTranslation, now,
                    resolvedArea?.TranslationSource ?? GalleryTranslationSources.Auto);
                _db.GalleryAreas.Add(area);
                await _db.SaveChangesAsync(cancellationToken);

                var newLocation = new GalleryLocation
                {
                    AreaId = area.AreaId,
                    LocationName = locationName,
                    LocationKey = locationKey,
                    CoverFileId = locationCoverId,
                    Status = EntityStatuses.Active,
                    DisplayOrder = 0,
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                GalleryTranslationApplier.Apply(newLocation, locationTranslation, now,
                    resolvedLocation?.TranslationSource ?? GalleryTranslationSources.Auto);
                _db.GalleryLocations.Add(newLocation);
                await _db.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
                areaId = area.AreaId;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            throw new BusinessRuleException("Chế độ tạo không hợp lệ.", GalleryErrorCodes.InvalidMode);
        }

        var created = await _db.GalleryLocations.AsNoTracking()
            .Where(l => l.AreaId == areaId && l.LocationKey == locationKey)
            .OrderByDescending(l => l.LocationId)
            .Select(l => l.LocationId)
            .FirstAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campusId,
            Action = "CREATE_GALLERY_LOCATION",
            EntityType = "GalleryLocation",
            EntityId = created,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "GalleryLocation",
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        mode,
                        areaId,
                        newArea = auditNewArea,
                        locationName,
                        areaCoverFileId = auditAreaCoverFileId,
                        areaCoverMediaType = auditAreaCoverFileId is null ? null : GalleryCoverMediaType.Video,
                        areaCoverFilePurpose = auditAreaCoverFileId is null ? null : FilePurposeDbValues.GalleryAreaCoverVideo,
                        translationStatus = auditTranslationStatus,
                    }),
                },
            },
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return await GalleryLocationDetailBuilder.BuildAsync(
            _db, created, cancellationToken, "Đã tạo vị trí mới.",
            translationFailed ? GalleryTranslationMessages.TranslationFailedWarning : null);
    }
}
