using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// Returns the read-only detail of a Visitor account for the Staff Leader tab, re-enforcing the
/// campus scope (BR-03) through the SAME <see cref="RelatedVisitorScope.VisibleInstances"/>
/// predicate the list uses — the caller having obtained an id from the list is never taken as
/// proof of access. A Visitor not related to the caller's campus is surfaced as Not Found (chosen
/// policy over a 403, to avoid leaking the account's existence — AF-02).
/// </summary>
public sealed class GetRelatedVisitorDetailsQueryHandler
    : IRequestHandler<GetRelatedVisitorDetailsQuery, RelatedVisitorAccountDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRelatedVisitorDetailsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<RelatedVisitorAccountDetailDto> Handle(
        GetRelatedVisitorDetailsQuery request, CancellationToken cancellationToken)
    {
        var campusId = RelatedVisitorScope.EnsureStaffLeaderCampus(_currentUser);

        // Visitor must have the BR-02 shape AND be related to this campus via a visible request.
        var visitor = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == request.VisitorUserId
                        && u.Role.RoleCode == RoleCodes.Visitor
                        && u.PrimaryCampusId == null
                        && u.DepartmentId == null
                        && u.SubRole == null)
            .Where(u => RelatedVisitorScope.VisibleInstances(_db, campusId)
                .Any(vrc => vrc.OperationalContactUserId == u.UserId))
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.Nationality,
                u.Gender,
                u.Status,
                u.CreatedVia,
                u.CreatedAt,
                u.LastLoginAt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Account", request.VisitorUserId);

        var relatedRequests = await RelatedVisitorScope.VisibleInstances(_db, campusId)
            .Where(vrc => vrc.OperationalContactUserId == request.VisitorUserId)
            .OrderByDescending(vrc => vrc.VisitRequest.SubmittedAt)
            .Select(vrc => new RelatedVisitorRequestDto
            {
                VisitRequestId = vrc.VisitRequestId,
                VisitInstanceId = vrc.VisitInstanceId,
                RequestCode = vrc.VisitRequest.RequestCode,
                // Instance row: mixed v2 shows THIS instance's detail name.
                DelegationName = vrc.FormDetail != null ? vrc.FormDetail.DelegationName : null,
                VisitScope = vrc.VisitRequest.VisitScope,
                RequestStatus = vrc.VisitRequest.Status,
                CampusInstanceStatus = vrc.Status,
                PlannedStartAt = vrc.PlannedStartAt,
                PlannedEndAt = vrc.PlannedEndAt,
            })
            .ToListAsync(cancellationToken);

        return new RelatedVisitorAccountDetailDto
        {
            UserId = visitor.UserId,
            FullName = visitor.FullName,
            Email = visitor.Email,
            Phone = visitor.Phone,
            Nationality = visitor.Nationality,
            Gender = visitor.Gender,
            Status = visitor.Status,
            CreatedVia = visitor.CreatedVia,
            CreatedAt = visitor.CreatedAt,
            LastLoginAt = visitor.LastLoginAt,
            RelatedRequests = relatedRequests,
            CanManageStatus = false,
            CanUpdateRole = false,
            CanResetPassword = false,
        };
    }
}
