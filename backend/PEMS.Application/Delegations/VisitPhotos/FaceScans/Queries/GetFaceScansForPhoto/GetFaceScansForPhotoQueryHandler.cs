using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Queries.GetFaceScansForPhoto;

public sealed class GetFaceScansForPhotoQueryHandler
    : IRequestHandler<GetFaceScansForPhotoQuery, List<VisitPhotoFaceScanDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetFaceScansForPhotoQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<VisitPhotoFaceScanDto>> Handle(
        GetFaceScansForPhotoQuery request, CancellationToken cancellationToken)
    {
        var photo = await _db.VisitPhotos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.VisitPhotoId == request.VisitPhotoId, cancellationToken)
            ?? throw new NotFoundException("VisitPhoto", request.VisitPhotoId);

        await VisitPhotoFaceScanAccess.ResolveStaffAsync(_db, _currentUser, photo.VisitInstanceId, cancellationToken);

        var scans = await _db.VisitPhotoFaceScans.AsNoTracking()
            .Where(s => s.VisitPhotoId == photo.VisitPhotoId)
            .OrderByDescending(s => s.RequestedAt).ThenByDescending(s => s.FaceScanId)
            .ToListAsync(cancellationToken);

        return await FaceScanMapper.ToDtosAsync(_db, scans, cancellationToken);
    }
}
