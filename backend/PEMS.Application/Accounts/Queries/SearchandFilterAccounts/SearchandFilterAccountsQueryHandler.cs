using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Accounts.Queries.SearchandFilterAccounts;

/// <summary>
/// UC-99 Search and Filter Accounts. Shares one scoped/paged/filtered read model with
/// UC-95 via <see cref="AccountListQueryExecutor"/> — no duplicated query logic.
/// </summary>
public sealed class SearchandFilterAccountsQueryHandler
    : IRequestHandler<SearchandFilterAccountsQuery, PaginatedResult<AccountListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionChecker _permissionChecker;

    public SearchandFilterAccountsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermissionChecker permissionChecker)
    {
        _db = db;
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
    }

    public Task<PaginatedResult<AccountListItemDto>> Handle(
        SearchandFilterAccountsQuery request, CancellationToken cancellationToken)
        => AccountListQueryExecutor.ExecuteAsync(_db, _currentUser, _permissionChecker, request, cancellationToken);
}
