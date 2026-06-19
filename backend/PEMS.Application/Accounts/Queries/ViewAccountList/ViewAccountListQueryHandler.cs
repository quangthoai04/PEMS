using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

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
    private readonly IPermissionChecker _permissionChecker;

    public ViewAccountListQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermissionChecker permissionChecker)
    {
        _db = db;
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
    }

    public Task<PaginatedResult<AccountListItemDto>> Handle(
        ViewAccountListQuery request, CancellationToken cancellationToken)
        => AccountListQueryExecutor.ExecuteAsync(_db, _currentUser, _permissionChecker, request, cancellationToken);
}
