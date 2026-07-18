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
        var scope = await VisitPhotoStudentScope.ResolveAcceptedStudentAsync(
            _db, _currentUser, request.VisitInstanceId, cancellationToken);
        var instance = scope.Instance;
        var visit = instance.VisitRequest;

        // Delegation name via the central dual-read path — a v2 (per-campus) request must show the
        // TARGET instance's own name, never the global compatibility projection.
        var delegationName = visit.DelegationName;
        if (visit.FormSchemaVersion >= FormSchemaVersions.PerCampus)
        {
            var content = await _formReadService.ResolveCampusFormContentAsync(
                visit, new[] { instance.VisitInstanceId }, cancellationToken);
            delegationName = content[instance.VisitInstanceId].DelegationName;
        }

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
