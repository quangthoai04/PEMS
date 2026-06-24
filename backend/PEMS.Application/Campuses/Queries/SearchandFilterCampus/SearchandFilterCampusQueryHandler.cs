using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;

namespace PEMS.Application.Campuses.Queries.SearchandFilterCampus;

/// <summary>
/// UC-83 handler. Shares <see cref="CampusListQueryExecutor"/> with UC-82, so search +
/// filters (keyword/city/campus/status) combine with AND logic over the same read model.
/// </summary>
public sealed class SearchandFilterCampusQueryHandler
    : IRequestHandler<SearchandFilterCampusQuery, PaginatedResult<CampusListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;

    public SearchandFilterCampusQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public Task<PaginatedResult<CampusListItemDto>> Handle(
        SearchandFilterCampusQuery request, CancellationToken cancellationToken)
        => CampusListQueryExecutor.ExecuteAsync(_db, _currentUser, _accessPolicy, request, cancellationToken);
}
