using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Tts.Queries.GetGalleryItemTtsStatus;

/// <summary>
/// Guards the Staff Leader scope (campus from the JWT, never the request) and the item's campus, then
/// returns the management narration status. READY audio is served through the authenticated
/// <c>/api/files/{id}/content</c> route (the dashboard already carries a session token).
/// </summary>
public sealed class GetGalleryItemTtsStatusQueryHandler
    : IRequestHandler<GetGalleryItemTtsStatusQuery, GalleryItemTtsStatusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IGalleryItemTtsService _tts;

    public GetGalleryItemTtsStatusQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IGalleryItemTtsService tts)
    {
        _db = db;
        _currentUser = currentUser;
        _tts = tts;
    }

    public async Task<GalleryItemTtsStatusResponse> Handle(
        GetGalleryItemTtsStatusQuery request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var itemId = (ulong)request.GalleryItemId;

        var campusOfItem = await _db.GalleryItems.AsNoTracking()
            .Where(i => i.GalleryItemId == itemId && i.DeletedAt == null)
            .Select(i => (ulong?)i.Location.Area.CampusId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("GalleryItem", request.GalleryItemId);

        if (campusOfItem != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.GalleryScopeForbidden,
                "Bạn không có quyền xem trạng thái giọng đọc của gallery item này.", 403);

        var status = await _tts.GetManagementStatusAsync(request.GalleryItemId, cancellationToken);

        return new GalleryItemTtsStatusResponse
        {
            Status = status.Status,
            CanRegenerate = status.CanRegenerate,
            AudioUrl = status.AudioFileId is { } fileId ? $"/api/files/{fileId}/content" : null,
            VoiceCode = status.VoiceCode,
            AudioType = status.AudioType,
            ErrorMessage = status.ErrorMessage,
        };
    }
}
