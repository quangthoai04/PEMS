using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.VisitPhotos.Queries.GetVisitInstancePhotos;

public sealed class GetVisitInstancePhotosQueryHandler
    : IRequestHandler<GetVisitInstancePhotosQuery, VisitInstancePhotosDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitFormReadService _formReadService;

    public GetVisitInstancePhotosQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
    }

    public async Task<VisitInstancePhotosDto> Handle(
        GetVisitInstancePhotosQuery request, CancellationToken cancellationToken)
    {
        // Shared with News' "pick from đoàn photos" picker — Host/participant Staff can browse (and
        // reuse) the same folder's photos, not just the uploading Student.
        var scope = await VisitInstanceMediaAccessScope.ResolveAsync(
            _db, _currentUser, request.VisitInstanceId, cancellationToken);
        var instance = scope.Instance;
        var visit = instance.VisitRequest;

        // The folder belongs to ONE campus instance, so it shows THAT instance's own name.
        var content = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = content[instance.VisitInstanceId].DelegationName;

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var folder = await _db.VisitPhotoFolders
            .FirstOrDefaultAsync(f => f.VisitRequestId == instance.VisitRequestId, cancellationToken);

        var photos = await _db.VisitPhotos
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.Status == "ACTIVE")
            .OrderByDescending(p => p.UploadedAt).ThenByDescending(p => p.VisitPhotoId)
            .Select(p => new
            {
                p.VisitPhotoId,
                p.FileId,
                p.Caption,
                p.UploadedAt,
                p.UploadedBy,
                FileName = p.File.OriginalFilename,
            })
            .ToListAsync(cancellationToken);

        var uploaderIds = photos.Select(p => p.UploadedBy).Distinct().ToList();
        var uploaderNames = uploaderIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => uploaderIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        return new VisitInstancePhotosDto
        {
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = delegationName,
            CampusName = campusName,
            FolderName = folder?.FolderName,
            FolderWebViewUrl = folder?.WebViewUrl,
            CanUpload = scope.CanUpload,
            Photos = photos.Select(p => new VisitInstancePhotoItemDto
            {
                VisitPhotoId = p.VisitPhotoId,
                FileId = p.FileId,
                FileName = p.FileName,
                Url = $"/api/files/{p.FileId}/content",
                Caption = p.Caption,
                UploadedAt = p.UploadedAt,
                UploadedByName = uploaderNames.TryGetValue(p.UploadedBy, out var n) ? n : string.Empty,
                UploadedByMe = p.UploadedBy == scope.UserId,
                CanRemove = p.UploadedBy == scope.UserId && scope.CanUpload,
            }).ToList(),
        };
    }
}
