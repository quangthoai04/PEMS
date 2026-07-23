using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.RetryGalleryTranslation;

/// <summary>
/// Explicit "Dịch lại" retry. Loads the target entity inside the caller's campus (Staff Leader scope),
/// re-translates the current Vietnamese source (area_name / location_name / title only) and persists the
/// fresh EN + metadata. An explicit retry ALWAYS calls the provider (the user asked for it) — a failure
/// leaves the entity FAILED with EN = NULL, never throws 500, and the operation is audited.
/// </summary>
public sealed class RetryGalleryTranslationCommandHandler
    : IRequestHandler<RetryGalleryTranslationCommand, RetryGalleryTranslationResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IGalleryTranslationCoordinator _translator;
    private readonly IDateTimeService _clock;

    public RetryGalleryTranslationCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IGalleryTranslationCoordinator translator,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _translator = translator;
        _clock = clock;
    }

    public async Task<RetryGalleryTranslationResponse> Handle(
        RetryGalleryTranslationCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var now = _clock.VietnamNow;

        var entityType = (request.EntityType ?? string.Empty).Trim().ToUpperInvariant();
        var entityId = (ulong)request.EntityId;

        string source;
        int maxLength;
        switch (entityType)
        {
            case GalleryTranslationEntityTypes.Area:
            {
                var area = await _db.GalleryAreas
                    .FirstOrDefaultAsync(a => a.AreaId == entityId && a.CampusId == campusId, cancellationToken)
                    ?? throw new NotFoundException("GalleryArea", entityId);

                source = TranslationSourceNormalizer.Normalize(area.AreaName);
                maxLength = GalleryTranslationLimits.NameEnMaxLength;
                var result = (await _translator.TranslateAsync(
                    new[] { new GalleryTranslationRequest(source, maxLength) }, cancellationToken))[0];
                GalleryTranslationApplier.Apply(area, result, now);
                area.UpdatedAt = now;
                area.UpdatedBy = actorId;
                await SaveWithAuditAsync(entityType, entityId, result.Status, campusId, actorId, now, cancellationToken);
                return BuildResponse(entityType, entityId, result);
            }
            case GalleryTranslationEntityTypes.Location:
            {
                var location = await _db.GalleryLocations
                    .Include(l => l.Area)
                    .FirstOrDefaultAsync(l => l.LocationId == entityId, cancellationToken);
                if (location is null || location.Area is null || location.Area.CampusId != campusId)
                    throw new NotFoundException("GalleryLocation", entityId);

                source = TranslationSourceNormalizer.Normalize(location.LocationName);
                maxLength = GalleryTranslationLimits.NameEnMaxLength;
                var result = (await _translator.TranslateAsync(
                    new[] { new GalleryTranslationRequest(source, maxLength) }, cancellationToken))[0];
                GalleryTranslationApplier.Apply(location, result, now);
                location.UpdatedAt = now;
                location.UpdatedBy = actorId;
                await SaveWithAuditAsync(entityType, entityId, result.Status, campusId, actorId, now, cancellationToken);
                return BuildResponse(entityType, entityId, result);
            }
            case GalleryTranslationEntityTypes.Item:
            {
                var item = await _db.GalleryItems
                    .Include(i => i.Location).ThenInclude(l => l.Area)
                    .FirstOrDefaultAsync(i => i.GalleryItemId == entityId && i.DeletedAt == null, cancellationToken);
                if (item is null || item.Location?.Area is null || item.Location.Area.CampusId != campusId)
                    throw new NotFoundException("GalleryItem", entityId);

                source = TranslationSourceNormalizer.Normalize(item.Title);
                maxLength = GalleryTranslationLimits.TitleEnMaxLength;
                var result = (await _translator.TranslateAsync(
                    new[] { new GalleryTranslationRequest(source, maxLength) }, cancellationToken))[0];
                GalleryTranslationApplier.Apply(item, result, now);
                item.UpdatedAt = now;
                item.UpdatedBy = actorId;
                await SaveWithAuditAsync(entityType, entityId, result.Status, campusId, actorId, now, cancellationToken);
                return BuildResponse(entityType, entityId, result);
            }
            default:
                throw new BusinessRuleException(
                    "Loại đối tượng dịch không hợp lệ (AREA / LOCATION / ITEM).", "GALLERY_TRANSLATION_ENTITY_INVALID");
        }
    }

    private async Task SaveWithAuditAsync(
        string entityType, ulong entityId, string status,
        ulong campusId, ulong actorId, System.DateTime now, CancellationToken ct)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campusId,
            Action = "RETRY_GALLERY_TRANSLATION",
            EntityType = entityType switch
            {
                GalleryTranslationEntityTypes.Area => "GalleryArea",
                GalleryTranslationEntityTypes.Location => "GalleryLocation",
                _ => "GalleryItem",
            },
            EntityId = entityId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange { FieldName = "TranslationStatus", NewValueText = status },
            },
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);
    }

    private static RetryGalleryTranslationResponse BuildResponse(
        string entityType, ulong entityId, GalleryTranslationResult result)
        => new()
        {
            EntityType = entityType,
            EntityId = entityId,
            TranslationStatus = result.Status,
            TranslatedText = result.TranslatedText,
            Message = result.Success
                ? "Đã dịch lại thành công."
                : GalleryTranslationMessages.TranslationFailedWarning,
        };
}
