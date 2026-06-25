using MediatR;
using PEMS.Application.Common.Models;
using PEMS.Application.Departments.Common;

namespace PEMS.Application.Departments.Queries.ViewDepartmentList;

/// <summary>
/// UC-104 View Department List (Staff Leader). Paged list of departments for the caller's own
/// campus. Search/filter/sort inputs are optional and shared with UC-103 via
/// <see cref="IDepartmentListCriteria"/>. Campus scope is resolved server-side.
/// </summary>
public sealed class ViewDepartmentListQuery
    : IRequest<PaginatedResult<DepartmentListItemDto>>, IDepartmentListCriteria
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; } = "name";
    public string? SortDirection { get; set; } = "asc";
}
