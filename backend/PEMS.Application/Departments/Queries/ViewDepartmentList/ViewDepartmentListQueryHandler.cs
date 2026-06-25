using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Departments.Common;

namespace PEMS.Application.Departments.Queries.ViewDepartmentList;

/// <summary>UC-104 handler. Delegates to the shared executor (one read model with UC-103).</summary>
public sealed class ViewDepartmentListQueryHandler
    : IRequestHandler<ViewDepartmentListQuery, PaginatedResult<DepartmentListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewDepartmentListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<PaginatedResult<DepartmentListItemDto>> Handle(
        ViewDepartmentListQuery request, CancellationToken cancellationToken)
        => DepartmentListQueryExecutor.ExecuteAsync(_db, _currentUser, request, cancellationToken);
}
