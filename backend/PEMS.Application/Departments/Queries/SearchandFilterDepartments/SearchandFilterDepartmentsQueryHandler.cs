using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Departments.Common;

namespace PEMS.Application.Departments.Queries.SearchandFilterDepartments;

/// <summary>UC-103 handler. Delegates to the shared executor (one read model with UC-104).</summary>
public sealed class SearchandFilterDepartmentsQueryHandler
    : IRequestHandler<SearchandFilterDepartmentsQuery, PaginatedResult<DepartmentListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SearchandFilterDepartmentsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<PaginatedResult<DepartmentListItemDto>> Handle(
        SearchandFilterDepartmentsQuery request, CancellationToken cancellationToken)
        => DepartmentListQueryExecutor.ExecuteAsync(_db, _currentUser, request, cancellationToken);
}
