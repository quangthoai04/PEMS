using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Queries.GetFaceScanDetail;

public sealed class GetFaceScanDetailQueryHandler
    : IRequestHandler<GetFaceScanDetailQuery, VisitPhotoFaceScanDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetFaceScanDetailQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<VisitPhotoFaceScanDto> Handle(
        GetFaceScanDetailQuery request, CancellationToken cancellationToken)
    {
        var scan = await _db.VisitPhotoFaceScans.AsNoTracking()
            .FirstOrDefaultAsync(s => s.FaceScanId == request.FaceScanId, cancellationToken)
            ?? throw new NotFoundException("VisitPhotoFaceScan", request.FaceScanId);

        await VisitPhotoFaceScanAccess.ResolveStaffAsync(_db, _currentUser, scan.VisitInstanceId, cancellationToken);

        return await FaceScanMapper.ToDtoAsync(_db, scan.FaceScanId, cancellationToken);
    }
}
