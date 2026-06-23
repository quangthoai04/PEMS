using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;

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
    private readonly IRoleAccessPolicy _accessPolicy;

    public SearchandFilterAccountsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public Task<PaginatedResult<AccountListItemDto>> Handle(
        SearchandFilterAccountsQuery request, CancellationToken cancellationToken)
        => AccountListQueryExecutor.ExecuteAsync(_db, _currentUser, _accessPolicy, request, cancellationToken);
}
