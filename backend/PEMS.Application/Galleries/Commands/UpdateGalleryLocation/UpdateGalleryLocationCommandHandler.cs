using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryLocation;

/// <summary>
/// "Chỉnh sửa khu vực và vị trí" — direct in-place UPDATE of the location and its CURRENT area. This
/// handler never inserts a new area and never reassigns <c>location.AreaId</c>; renaming the area from
/// one row therefore renames it for every location that shares it. Status and gallery items are never
/// touched. Covers are replaced only when a new file is supplied (upload first, swap the FK only after
/// success; a DB failure cleans up the freshly uploaded files and keeps the old covers).
/// Translation: an EN carried by the payload (preview AUTO_PREVIEW with a matching source hash, or a
/// MANUAL edit) is persisted WITHOUT calling the provider; a stale preview fails with
/// GALLERY_TRANSLATION_PREVIEW_STALE; names that changed with no usable EN go through the legacy
/// translate-during-save path in ONE batched provider request. Cover-only edits never hit the provider.
/// </summary>
public sealed class UpdateGalleryLocationCommandHandler
    : IRequestHandler<UpdateGalleryLocationCommand, GalleryLocationDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IGalleryTranslationCoordinator _translator;
    private readonly IDateTimeService _clock;
    private readonly ILogger<UpdateGalleryLocationCommandHandler> _logger;

    public UpdateGalleryLocationCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IGoogleDriveStorageService drive,
        IGalleryTranslationCoordinator translator,
        IDateTimeService clock,
        ILogger<UpdateGalleryLocationCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _drive = drive;
        _translator = translator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<GalleryLocationDetailDto> Handle(
        UpdateGalleryLocationCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var now = _clock.VietnamNow;

        var areaName = GalleryKeyNormalizer.CleanName(request.AreaName);
        if (areaName.Length == 0)
            throw new BusinessRuleException("Vui lòng nhập tên khu vực/tòa.", GalleryErrorCodes.AreaNameRequired);
        var locationName = GalleryKeyNormalizer.CleanName(request.LocationName);
        if (locationName.Length == 0)
            throw new BusinessRuleException("Vui lòng nhập vị trí cụ thể.", GalleryErrorCodes.LocationNameRequired);

        var location = await GalleryLocationWriteGuard.LoadLocationInCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);
        var area = location.Area!;

        var oldAreaName = area.AreaName;
        var oldLocationName = location.LocationName;

        var areaNameChanged = !string.Equals(
            TranslationSourceNormalizer.Normalize(oldAreaName), areaName, StringComparison.Ordinal);
        var locationNameChanged = !string.Equals(
            TranslationSourceNormalizer.Normalize(oldLocationName), locationName, StringComparison.Ordinal);

        // Duplicate checks BEFORE any upload (the common failure never orphans a Drive file). The row
        // being renamed is excluded — saving an unchanged name must never conflict with itself.
        var areaKey = GalleryKeyNormalizer.ToKey(areaName);
        var locationKey = GalleryKeyNormalizer.ToKey(locationName);
        if (areaNameChanged)
            await GalleryLocationWriteGuard.EnsureAreaKeyFreeAsync(
                _db, campusId, areaKey, cancellationToken, excludeAreaId: area.AreaId);
        await GalleryLocationWriteGuard.EnsureLocationKeyFreeAsync(
            _db, area.AreaId, locationKey, location.LocationId, cancellationToken);

        // Resolve the client-supplied EN (preview reuse / manual) BEFORE uploads: a stale preview must
        // fail fast. Only a name that actually changed re-translates; an unchanged name keeps its stored
        // EN + metadata — with ONE exception: a MANUAL EN fix on an unchanged name is applied as-is.
        var resolvedArea = areaNameChanged || IsManualEnEdit(request.AreaTranslationOrigin, request.AreaNameEn, area.AreaNameEn)
            ? GalleryPreviewedTranslation.TryResolve(
                areaName, request.AreaNameEn, request.AreaTranslationOrigin,
                request.AreaTranslationSourceHash, GalleryTranslationLimits.NameEnMaxLength)
            : null;
        var resolvedLocation = locationNameChanged || IsManualEnEdit(request.LocationTranslationOrigin, request.LocationNameEn, location.LocationNameEn)
            ? GalleryPreviewedTranslation.TryResolve(
                locationName, request.LocationNameEn, request.LocationTranslationOrigin,
                request.LocationTranslationSourceHash, GalleryTranslationLimits.NameEnMaxLength)
            : null;

        // Names that changed but carry no usable EN → ONE batched provider request (never per keystroke,
        // never re-translating a reused preview). Failure never blocks the save (FAILED metadata + warning).
        var providerRequests = new List<GalleryTranslationRequest>();
        var areaNeedsProvider = areaNameChanged && resolvedArea is null;
        var locationNeedsProvider = locationNameChanged && resolvedLocation is null;
        if (areaNeedsProvider)
            providerRequests.Add(new GalleryTranslationRequest(areaName, GalleryTranslationLimits.NameEnMaxLength));
        if (locationNeedsProvider)
            providerRequests.Add(new GalleryTranslationRequest(locationName, GalleryTranslationLimits.NameEnMaxLength));
        var providerResults = providerRequests.Count > 0
            ? await _translator.TranslateAsync(providerRequests, cancellationToken)
            : Array.Empty<GalleryTranslationResult>();
        var providerIndex = 0;
        var areaProviderResult = areaNeedsProvider ? providerResults[providerIndex++] : null;
        var locationProviderResult = locationNeedsProvider ? providerResults[providerIndex] : null;
        var translationFailed = (areaProviderResult is { Success: false })
                                || (locationProviderResult is { Success: false });

        // Optional cover replacements — upload the new files first, swap the FKs only after success.
        var uploadedFileIds = new List<ulong>();
        ulong? oldAreaCoverFileId = null, newAreaCoverFileId = null;
        ulong? oldLocationCoverFileId = null, newLocationCoverFileId = null;
        if (request.AreaCoverVideo is not null)
        {
            oldAreaCoverFileId = area.CoverFileId;
            newAreaCoverFileId = await GalleryAreaCoverVideo.UploadAsync(
                _fileUpload, request.AreaCoverVideo, actorId, cancellationToken);
            uploadedFileIds.Add(newAreaCoverFileId.Value);
        }
        if (request.LocationCoverImage is not null)
        {
            oldLocationCoverFileId = location.CoverFileId;
            newLocationCoverFileId = await GalleryCoverImage.UploadAsync(
                _fileUpload, request.LocationCoverImage, isArea: false, actorId, cancellationToken);
            uploadedFileIds.Add(newLocationCoverFileId.Value);
        }

        try
        {
            // ── Area: update the EXISTING row (AreaId untouched — every sibling location follows). ──
            var areaChanged = false;
            if (areaNameChanged)
            {
                area.AreaName = areaName;
                area.AreaKey = areaKey;
                if (resolvedArea is not null)
                    GalleryTranslationApplier.Apply(area, resolvedArea.Result, now, resolvedArea.TranslationSource);
                else
                    GalleryTranslationApplier.Apply(area, areaProviderResult!, now);
                areaChanged = true;
            }
            else if (resolvedArea is not null)
            {
                // MANUAL EN fix while the VI stayed the same.
                GalleryTranslationApplier.Apply(area, resolvedArea.Result, now, resolvedArea.TranslationSource);
                areaChanged = true;
            }
            if (newAreaCoverFileId is { } areaCover)
            {
                area.CoverFileId = areaCover; // cover-only swap: translation metadata untouched
                areaChanged = true;
            }
            if (areaChanged)
            {
                area.UpdatedAt = now;
                area.UpdatedBy = actorId;
            }

            // ── Location: update the EXISTING row (LocationId + AreaId untouched). ──
            var locationChanged = false;
            if (locationNameChanged)
            {
                location.LocationName = locationName;
                location.LocationKey = locationKey;
                if (resolvedLocation is not null)
                    GalleryTranslationApplier.Apply(location, resolvedLocation.Result, now, resolvedLocation.TranslationSource);
                else
                    GalleryTranslationApplier.Apply(location, locationProviderResult!, now);
                locationChanged = true;
            }
            else if (resolvedLocation is not null)
            {
                GalleryTranslationApplier.Apply(location, resolvedLocation.Result, now, resolvedLocation.TranslationSource);
                locationChanged = true;
            }
            if (newLocationCoverFileId is { } locationCover)
            {
                location.CoverFileId = locationCover;
                locationChanged = true;
            }
            if (locationChanged)
            {
                location.UpdatedAt = now;
                location.UpdatedBy = actorId;
            }

            // Audit — one row per entity that actually changed (no area audit when the area is untouched).
            if (areaChanged)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = actorId,
                    CampusId = campusId,
                    Action = "UPDATE_GALLERY_AREA",
                    EntityType = "GalleryArea",
                    EntityId = area.AreaId,
                    Changes = BuildAreaAuditChanges(
                        oldAreaName, areaName, areaNameChanged, oldAreaCoverFileId, newAreaCoverFileId,
                        areaNameChanged || resolvedArea is not null ? area.TranslationStatus : null),
                    CreatedAt = now,
                });
            }
            if (locationChanged)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = actorId,
                    CampusId = campusId,
                    Action = "UPDATE_GALLERY_LOCATION",
                    EntityType = "GalleryLocation",
                    EntityId = location.LocationId,
                    Changes = BuildLocationAuditChanges(
                        oldLocationName, locationName, locationNameChanged,
                        oldLocationCoverFileId, newLocationCoverFileId,
                        locationNameChanged || resolvedLocation is not null ? location.TranslationStatus : null),
                    CreatedAt = now,
                });
            }

            // Entities + audit commit atomically (one SaveChanges = one DB transaction).
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Compensation: the DB kept the old covers, so drop the freshly uploaded Drive files.
            await GalleryFileCleanup.RemoveUploadedFilesAsync(_db, _drive, _logger, uploadedFileIds, cancellationToken);
            throw;
        }

        return await GalleryLocationDetailBuilder.BuildAsync(
            _db, location.LocationId, cancellationToken, "Đã cập nhật khu vực và vị trí.",
            translationFailed ? GalleryTranslationMessages.TranslationFailedWarning : null);
    }

    /// <summary>True when the payload carries a MANUAL EN that differs from the stored EN — the only
    /// case where an unchanged Vietnamese name still updates the translation fields.</summary>
    private static bool IsManualEnEdit(string? origin, string? requestEn, string? storedEn)
    {
        if (!string.Equals((origin ?? string.Empty).Trim(), GalleryTranslationOrigins.Manual,
                StringComparison.OrdinalIgnoreCase))
            return false;
        var en = requestEn?.Trim();
        return !string.IsNullOrEmpty(en) && !string.Equals(en, storedEn, StringComparison.Ordinal);
    }

    private static List<AuditLogChange> BuildAreaAuditChanges(
        string oldName, string newName, bool nameChanged,
        ulong? oldCoverFileId, ulong? newCoverFileId, string? translationStatus)
    {
        var changes = new List<AuditLogChange>();
        if (nameChanged)
            changes.Add(new AuditLogChange { FieldName = "AreaName", OldValueText = oldName, NewValueText = newName });
        if (newCoverFileId is not null)
            changes.Add(new AuditLogChange
            {
                FieldName = "AreaCoverFileId",
                OldValueText = oldCoverFileId?.ToString(),
                NewValueText = newCoverFileId.ToString(),
            });
        if (translationStatus is not null)
            changes.Add(new AuditLogChange { FieldName = "TranslationStatus", NewValueText = translationStatus });
        return changes;
    }

    private static List<AuditLogChange> BuildLocationAuditChanges(
        string oldName, string newName, bool nameChanged,
        ulong? oldCoverFileId, ulong? newCoverFileId, string? translationStatus)
    {
        var changes = new List<AuditLogChange>();
        if (nameChanged)
            changes.Add(new AuditLogChange { FieldName = "LocationName", OldValueText = oldName, NewValueText = newName });
        if (newCoverFileId is not null)
            changes.Add(new AuditLogChange
            {
                FieldName = "LocationCoverFileId",
                OldValueText = oldCoverFileId?.ToString(),
                NewValueText = newCoverFileId.ToString(),
            });
        if (translationStatus is not null)
            changes.Add(new AuditLogChange { FieldName = "TranslationStatus", NewValueText = translationStatus });
        return changes;
    }
}
