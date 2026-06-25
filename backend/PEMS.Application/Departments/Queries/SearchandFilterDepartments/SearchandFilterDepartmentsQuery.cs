using MediatR;
using PEMS.Application.Common.Models;
using PEMS.Application.Departments.Common;

namespace PEMS.Application.Departments.Queries.SearchandFilterDepartments;

/// <summary>
/// UC-103 Search and Filter Departments (Staff Leader). Same scoped, paged read model as UC-104
/// (<see cref="IDepartmentListCriteria"/>), with keyword (name + head full name) and status filter
/// on top of the campus scope. Campus is never selectable by the client.
/// </summary>
public sealed class SearchandFilterDepartmentsQuery
    : IRequest<PaginatedResult<DepartmentListItemDto>>, IDepartmentListCriteria
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; } = "name";
    public string? SortDirection { get; set; } = "asc";
}
