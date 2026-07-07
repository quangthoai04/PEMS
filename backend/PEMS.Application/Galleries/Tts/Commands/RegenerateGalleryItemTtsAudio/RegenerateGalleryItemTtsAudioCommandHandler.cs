using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Tts.Commands.RegenerateGalleryItemTtsAudio;

/// <summary>
/// Guards the Staff Leader scope (campus from the JWT, never the request), validates the description,
/// then delegates to <see cref="IGalleryItemTtsService.EnsureAudioAsync"/> with MANUAL_REGENERATE +
/// cooldown bypass. Unlike the anonymous endpoints this one surfaces real errors (422) so the Staff
/// Leader knows why a regenerate is impossible.
/// </summary>
public sealed class RegenerateGalleryItemTtsAudioCommandHandler
    : IRequestHandler<RegenerateGalleryItemTtsAudioCommand, GalleryItemTtsAudioResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IGalleryItemTtsService _tts;
    private readonly IGalleryTtsHashService _hash;
    private readonly EverAiTtsOptions _options;

    public RegenerateGalleryItemTtsAudioCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IGalleryItemTtsService tts,
        IGalleryTtsHashService hash,
        IOptions<EverAiTtsOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _tts = tts;
        _hash = hash;
        _options = options.Value;
    }

    public async Task<GalleryItemTtsAudioResponse> Handle(
        RegenerateGalleryItemTtsAudioCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var itemId = (ulong)request.GalleryItemId;

        var head = await _db.GalleryItems.AsNoTracking()
            .Where(i => i.GalleryItemId == itemId && i.DeletedAt == null)
            .Select(i => new { i.Location.Area.CampusId, i.Description })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("GalleryItem", request.GalleryItemId);

        if (head.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.GalleryScopeForbidden,
                "Bạn không có quyền tạo lại giọng đọc cho gallery item này.", 403);

        var text = _hash.NormalizeDescription(head.Description);
        if (text.Length == 0 || text.Length > _options.MaxInputCharacters)
            throw new BusinessRuleException(
                $"Mô tả phải khác rỗng và không vượt quá {_options.MaxInputCharacters} ký tự để tạo giọng đọc.",
                GalleryTtsErrorCodes.InvalidDescription);

        // Decide whether a regenerate is warranted from the current status. If the current description
        // already matches a READY audio (same source_text_hash) we do NOT create a new job — there is
        // nothing to regenerate; regenerating would only waste EverAI credits on identical audio.
        var status = await _tts.GetManagementStatusAsync(request.GalleryItemId, cancellationToken);

        if (status.Status == TtsManagementStatuses.Disabled)
            throw new BusinessRuleException(
                "Tính năng giọng đọc chưa được bật hoặc chưa cấu hình đầy đủ.",
                GalleryTtsErrorCodes.Disabled);

        if (status.Status == TtsManagementStatuses.Ready)
            return new GalleryItemTtsAudioResponse
            {
                Status = TtsAudioStatuses.UpToDate,
                Message = GalleryTtsMessages.UpToDate,
            };

        if (status.Status == TtsManagementStatuses.Processing)
            return new GalleryItemTtsAudioResponse
            {
                Status = TtsAudioStatuses.Processing,
                Message = GalleryTtsMessages.Regenerating,
            };

        // FAILED / STALE / NOT_CREATED → queue a fresh MANUAL_REGENERATE job (bypasses failed cooldown).
        await _tts.EnsureAudioAsync(
            request.GalleryItemId,
            TtsTriggerSources.ManualRegenerate,
            (long)actorId,
            requirePublicVisible: false,
            bypassFailedCooldown: true,
            cancellationToken);

        return new GalleryItemTtsAudioResponse
        {
            Status = TtsAudioStatuses.Processing,
            Message = GalleryTtsMessages.Regenerating,
        };
    }
}
