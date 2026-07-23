using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.PreviewGalleryItemTranslation;

/// <summary>
/// Read-only VI → EN preview for the Staff Leader gallery item modals (title only). Guards the Staff
/// Leader campus scope (this is NOT public), normalizes the source, and — when the EDIT modal supplies
/// the item id and the stored translation is still up to date for that exact source — answers from the
/// database with ZERO provider calls (§8: one VI snapshot only ever pays Google once). Otherwise it
/// makes exactly ONE provider request. A provider failure surfaces as a retryable 422 — never a silent
/// empty EN — and nothing is ever persisted here.
/// </summary>
public sealed class PreviewGalleryItemTranslationCommandHandler
    : IRequestHandler<PreviewGalleryItemTranslationCommand, GalleryItemTranslationPreviewDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IGalleryTranslationCoordinator _translator;

    public PreviewGalleryItemTranslationCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IGalleryTranslationCoordinator translator)
    {
        _db = db;
        _currentUser = currentUser;
        _translator = translator;
    }

    public async Task<GalleryItemTranslationPreviewDto> Handle(
        PreviewGalleryItemTranslationCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);

        var entityType = (request.EntityType ?? string.Empty).Trim().ToUpperInvariant();
        if (entityType != GalleryItemTranslationPreviewFields.EntityTypeGalleryItem)
            throw new BusinessRuleException("Chế độ dịch không hợp lệ.", GalleryErrorCodes.InvalidMode);
        var field = (request.Field ?? string.Empty).Trim().ToUpperInvariant();
        if (field != GalleryItemTranslationPreviewFields.FieldTitle)
            throw new BusinessRuleException("Chế độ dịch không hợp lệ.", GalleryErrorCodes.InvalidMode);

        var source = TranslationSourceNormalizer.Normalize(request.SourceText);
        if (source.Length == 0)
            throw new BusinessRuleException(
                "Vui lòng nhập tiêu đề trước khi dịch.", GalleryErrorCodes.TranslationPreviewEmpty);

        // EDIT-modal optimization (§8): when the source equals the stored title snapshot whose EN is
        // already READY, answer from the DB — "Dịch lại" on an unchanged title never pays Google again.
        if (request.EntityId is { } rawItemId && rawItemId > 0)
        {
            var stored = await _db.GalleryItems.AsNoTracking()
                .Where(i => i.GalleryItemId == (ulong)rawItemId && i.DeletedAt == null)
                .Select(i => new
                {
                    CampusId = i.Location.Area.CampusId,
                    i.TitleEn,
                    i.TranslationStatus,
                    i.TranslationSourceHash,
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("GalleryItem", (ulong)rawItemId);

            if (stored.CampusId != campusId)
                throw new AuthBusinessException(
                    GalleryErrorCodes.GalleryScopeForbidden,
                    "Bạn không có quyền thao tác với gallery item này.", 403);

            if (GalleryTranslationApplier.IsUpToDate(
                    stored.TranslationStatus, stored.TranslationSourceHash, stored.TitleEn, source))
            {
                return new GalleryItemTranslationPreviewDto
                {
                    SourceText = source,
                    SourceHash = TranslationSourceHasher.ComputeHash(source),
                    TranslatedText = stored.TitleEn!,
                    ServedFrom = GalleryTranslationPreviewSources.Database,
                };
            }
        }

        // Exactly ONE provider request for the title.
        var results = await _translator.TranslateAsync(
            new[] { new GalleryTranslationRequest(source, GalleryTranslationLimits.TitleEnMaxLength) },
            cancellationToken);
        var result = results[0];

        // Preview must return a usable EN — a failure is a retryable error (the modal keeps the VI and
        // any manual EN and offers retry), never an empty translation.
        if (!result.Success)
            throw new BusinessRuleException(
                "Không thể dịch tiêu đề lúc này. Vui lòng thử lại hoặc nhập tiêu đề tiếng Anh thủ công.",
                GalleryErrorCodes.TranslationPreviewFailed);

        return new GalleryItemTranslationPreviewDto
        {
            SourceText = result.SourceText,
            SourceHash = result.SourceHash,
            TranslatedText = result.TranslatedText!,
            ServedFrom = GalleryTranslationPreviewSources.Google,
        };
    }
}
