using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;

namespace PEMS.Application.Accounts.Queries.ViewAccountList;

/// <summary>
/// UC-95 View Account List. Delegates to <see cref="AccountListQueryExecutor"/> so it
/// shares one scoped, paged, filtered read model with UC-99.
/// </summary>
public sealed class ViewAccountListQueryHandler
    : IRequestHandler<ViewAccountListQuery, PaginatedResult<AccountListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;

    public ViewAccountListQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public Task<PaginatedResult<AccountListItemDto>> Handle(
        ViewAccountListQuery request, CancellationToken cancellationToken)
        => AccountListQueryExecutor.ExecuteAsync(_db, _currentUser, _accessPolicy, request, cancellationToken);
}
