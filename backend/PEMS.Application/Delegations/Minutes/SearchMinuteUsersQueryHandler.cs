using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Minutes;

public sealed class SearchMinuteUsersQueryHandler
    : IRequestHandler<SearchMinuteUsersQuery, List<MinuteUserSearchDto>>
{
    private const int MaxResults = 20;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SearchMinuteUsersQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<MinuteUserSearchDto>> Handle(SearchMinuteUsersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var (inScope, canEdit) = MinuteAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!inScope || !canEdit)
            throw new ForbiddenException("Bạn không có quyền chỉnh sửa biên bản chuyến thăm này.");

        var term = (request.Query ?? string.Empty).Trim();
        if (term.Length < 2)
            return new List<MinuteUserSearchDto>();

        var users = await _db.Users
            .Include(u => u.Department).Include(u => u.PrimaryCampus)
            .Where(u => u.Status == "ACTIVE"
                && (EF.Functions.Like(u.FullName, $"%{term}%") || EF.Functions.Like(u.Email, $"%{term}%")))
            .OrderBy(u => u.FullName)
            .Take(MaxResults)
            .ToListAsync(cancellationToken);

        return users.Select(u => new MinuteUserSearchDto
        {
            UserId = u.UserId,
            FullName = u.FullName,
            Email = u.Email,
            Organization = u.Department?.Name ?? u.PrimaryCampus?.Name,
        }).ToList();
    }
}
