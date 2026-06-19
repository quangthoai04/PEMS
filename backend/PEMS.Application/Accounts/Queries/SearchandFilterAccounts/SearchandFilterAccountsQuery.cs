using System;
using MediatR;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Accounts.Queries.SearchandFilterAccounts;

/// <summary>
/// UC-99 Search and Filter Accounts. Same criteria/result as UC-95 (it is the same
/// read model with all filters supplied); kept as a distinct query so it can carry its
/// own RBAC permission (UC-99) on the dedicated search endpoint.
/// </summary>
public sealed class SearchandFilterAccountsQuery : IRequest<PaginatedResult<AccountListItemDto>>, IAccountListCriteria
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }
    public string? RoleCode { get; set; }
    public string? SubRole { get; set; }
    public string? Status { get; set; }
    public ulong? CampusId { get; set; }
    public ulong? DepartmentId { get; set; }
    public string? ProviderType { get; set; }
    public string? CreatedVia { get; set; }
    public string? AccountType { get; set; }   // INTERNAL | VISITOR | ALL
    public bool? HasCampus { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? LastLoginFrom { get; set; }
    public DateTime? LastLoginTo { get; set; }

    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}
