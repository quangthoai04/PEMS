using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;

namespace PEMS.Application.Campuses.Queries.ViewCampusList;

/// <summary>
/// UC-82 handler. Delegates to <see cref="CampusListQueryExecutor"/> so it shares one
/// scoped, paged, filtered read model with UC-83 (Search and Filter Campus).
/// </summary>
public sealed class ViewCampusListQueryHandler
    : IRequestHandler<ViewCampusListQuery, PaginatedResult<CampusListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;

    public ViewCampusListQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public Task<PaginatedResult<CampusListItemDto>> Handle(
        ViewCampusListQuery request, CancellationToken cancellationToken)
        => CampusListQueryExecutor.ExecuteAsync(_db, _currentUser, _accessPolicy, request, cancellationToken);
}
