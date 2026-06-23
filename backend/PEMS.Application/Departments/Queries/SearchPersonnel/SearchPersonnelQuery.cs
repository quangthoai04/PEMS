using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.Departments.Queries.SearchPersonnel;

public class SearchPersonnelQuery : IRequest<SearchPersonnelResult>
{
    public ulong DepartmentId { get; set; }
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class SearchPersonnelResult
{
    public List<SearchPersonnelDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
