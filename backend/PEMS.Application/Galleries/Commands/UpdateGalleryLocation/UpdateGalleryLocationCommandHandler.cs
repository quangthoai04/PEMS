using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryLocation;

/// <summary>
/// "Chỉnh sửa khu vực và vị trí" — updates the location and, via COPY-ON-WRITE, its area.
///
/// Renaming the area from one location must affect ONLY that location. Because the name lives on
/// <c>gallery_areas</c>, that is done by splitting: when the area name changes and the area still has
/// sibling locations, a NEW area is inserted and this location is moved onto it; the old area (and its
/// siblings) keep the old name and the old cover. When the area has no sibling there is nothing to
/// protect, so it is renamed in place — no gratuitous duplicate area.
///
/// The area cover VIDEO keeps its original shared semantics: a video-only edit (area name unchanged)
/// still replaces the current area's cover and therefore applies to every sibling location. On a split,
/// the new area takes the newly uploaded video if one was supplied, otherwise it INHERITS the old
/// area's <c>cover_file_id</c> — the same file row is referenced by both areas and is never duplicated
/// on Drive and never deleted here.
///
/// Status and gallery items are never touched. Covers are replaced only when a new file is supplied
/// (upload first, swap the FK only after success; a DB failure rolls the whole write back and cleans up
/// ONLY the freshly uploaded files, never the inherited/shared old cover).
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

        // Copy-on-write decision, taken BEFORE any upload so a rejected edit never orphans a file.
        // Split only when the rename produces a genuinely DIFFERENT area identity (normalized key) AND
        // a sibling location would otherwise be dragged along. A cosmetic rename that keeps the same key
        // ("Tòa B" → "TÒA B") cannot be split — (campus_id, area_key) is UNIQUE — so it stays an
        // in-place rename of the one shared area, exactly as before.
        var areaKeyChanged = !string.Equals(areaKey, area.AreaKey, StringComparison.Ordinal);
        var siblingCount = areaNameChanged && areaKeyChanged
            ? await _db.GalleryLocations.CountAsync(l => l.AreaId == area.AreaId, cancellationToken)
            : 1;
        var splitArea = areaNameChanged && areaKeyChanged && siblingCount > 1;

        // The location keeps its own key unique inside the area it will END UP in. On a split that area
        // is brand new and empty, so there is nothing to collide with; checking the old area there would
        // reject a legitimate edit (renaming the area AND reusing a sibling's location name).
        if (!splitArea)
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
            // The split needs two SaveChanges (INSERT area → read its generated id → move the location),
            // so the whole write runs inside ONE transaction: a failure must never leave a new area
            // behind with the location still on the old one, or a moved location on an incomplete area.
            await using var tx = await _db.BeginTransactionAsync(cancellationToken);
            try
            {
                var areaChanged = false;
                GalleryArea? newArea = null;

                if (splitArea)
                {
                    // Re-check inside the transaction (same as CreateGalleryLocation): two concurrent
                    // renames to the same new name must lose here, not on the (campus_id, area_key)
                    // unique index as an opaque 500.
                    await GalleryLocationWriteGuard.EnsureAreaKeyFreeAsync(
                        _db, campusId, areaKey, cancellationToken);

                    // ── Copy-on-write: the rename belongs to THIS location only. ──
                    // The old area is left completely untouched (name AND cover) — its sibling locations
                    // must keep seeing exactly what they saw before this edit.
                    newArea = new GalleryArea
                    {
                        CampusId = area.CampusId,
                        AreaName = areaName,
                        AreaKey = areaKey,
                        // A newly uploaded video belongs to the NEW area only; with no new video the new
                        // area inherits the old file row (shared reference, no Drive copy).
                        CoverFileId = newAreaCoverFileId ?? area.CoverFileId,
                        Status = area.Status,
                        DisplayOrder = area.DisplayOrder,
                        CreatedAt = now,
                        CreatedBy = actorId,
                    };
                    if (resolvedArea is not null)
                        GalleryTranslationApplier.Apply(newArea, resolvedArea.Result, now, resolvedArea.TranslationSource);
                    else
                        GalleryTranslationApplier.Apply(newArea, areaProviderResult!, now);

                    _db.GalleryAreas.Add(newArea);
                    await _db.SaveChangesAsync(cancellationToken); // materializes newArea.AreaId

                    location.AreaId = newArea.AreaId;
                }
                else
                {
                    // ── Area: update the EXISTING row (AreaId untouched — every sibling follows). ──
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
                        // Video-only (or in-place rename) swap — intentionally shared by every sibling
                        // location of this area. Translation metadata untouched.
                        area.CoverFileId = areaCover;
                        areaChanged = true;
                    }
                    if (areaChanged)
                    {
                        area.UpdatedAt = now;
                        area.UpdatedBy = actorId;
                    }
                }

                // ── Location: update the EXISTING row (LocationId untouched; AreaId only on a split). ──
                var locationFieldsChanged = false;
                if (locationNameChanged)
                {
                    location.LocationName = locationName;
                    location.LocationKey = locationKey;
                    if (resolvedLocation is not null)
                        GalleryTranslationApplier.Apply(location, resolvedLocation.Result, now, resolvedLocation.TranslationSource);
                    else
                        GalleryTranslationApplier.Apply(location, locationProviderResult!, now);
                    locationFieldsChanged = true;
                }
                else if (resolvedLocation is not null)
                {
                    GalleryTranslationApplier.Apply(location, resolvedLocation.Result, now, resolvedLocation.TranslationSource);
                    locationFieldsChanged = true;
                }
                if (newLocationCoverFileId is { } locationCover)
                {
                    location.CoverFileId = locationCover;
                    locationFieldsChanged = true;
                }
                if (locationFieldsChanged || splitArea)
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
                if (splitArea)
                {
                    // Two rows so the history reads unambiguously: an area was born out of a location
                    // edit, and that location moved onto it. The old area shows NO audit row — nothing
                    // about it changed.
                    var splitPayload = JsonSerializer.Serialize(new
                    {
                        locationId = location.LocationId,
                        oldAreaId = area.AreaId,
                        oldAreaName,
                        newAreaId = newArea!.AreaId,
                        newAreaName = areaName,
                        areaVideoChanged = newAreaCoverFileId is not null,
                        newAreaCoverFileId = newArea.CoverFileId,
                        inheritedCover = newAreaCoverFileId is null,
                        translationStatus = newArea.TranslationStatus,
                    });
                    _db.AuditLogs.Add(new AuditLog
                    {
                        ActorUserId = actorId,
                        CampusId = campusId,
                        Action = "CREATE_GALLERY_AREA_FROM_LOCATION_EDIT",
                        EntityType = "GalleryArea",
                        EntityId = newArea.AreaId,
                        Changes = new List<AuditLogChange>
                        {
                            new AuditLogChange { FieldName = "GalleryArea", NewValueText = splitPayload },
                        },
                        CreatedAt = now,
                    });
                    _db.AuditLogs.Add(new AuditLog
                    {
                        ActorUserId = actorId,
                        CampusId = campusId,
                        Action = "MOVE_GALLERY_LOCATION_TO_NEW_AREA",
                        EntityType = "GalleryLocation",
                        EntityId = location.LocationId,
                        Changes = new List<AuditLogChange>
                        {
                            new AuditLogChange
                            {
                                FieldName = "AreaId",
                                OldValueText = area.AreaId.ToString(),
                                NewValueText = newArea.AreaId.ToString(),
                            },
                            new AuditLogChange { FieldName = "GalleryLocation", NewValueText = splitPayload },
                        },
                        CreatedAt = now,
                    });
                }
                if (locationFieldsChanged)
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

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Compensation: the DB kept the old covers (and, on a rolled-back split, the old area
            // relations), so drop ONLY the files this request freshly uploaded. An inherited cover is
            // never in `uploadedFileIds`, so a still-referenced shared video can never be removed here.
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
