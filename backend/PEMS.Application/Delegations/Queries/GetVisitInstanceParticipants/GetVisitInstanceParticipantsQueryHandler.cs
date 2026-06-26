using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;

public sealed class GetVisitInstanceParticipantsQueryHandler
    : IRequestHandler<GetVisitInstanceParticipantsQuery, IReadOnlyList<VisitParticipantListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInstanceParticipantsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<VisitParticipantListItemDto>> Handle(
        GetVisitInstanceParticipantsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var relation = await VisitInstanceAccess.ResolveRelationAsync(
            _db, _currentUser, instance, instance.VisitRequest, cancellationToken);

        if (!VisitInstanceAccess.CanViewInternal(relation))
            throw new ForbiddenException("Bạn không có quyền xem thành phần tham gia của cơ sở này.");

        return await VisitParticipantListBuilder.BuildAsync(_db, instance.VisitInstanceId, cancellationToken);
    }
}
