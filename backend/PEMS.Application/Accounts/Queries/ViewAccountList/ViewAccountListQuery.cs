using System;
using MediatR;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Accounts.Queries.ViewAccountList;

/// <summary>
/// UC-95 View Account List. Returns a paged, scoped list of accounts. All search /
/// filter / sort inputs are optional; the same criteria power UC-99
/// (Search and Filter Accounts).
/// </summary>
public sealed class ViewAccountListQuery : IRequest<PaginatedResult<AccountListItemDto>>, IAccountListCriteria
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
