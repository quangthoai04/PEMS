namespace PEMS.Application.Accounts.Common;

/// <summary>
/// The full set of paging / search / filter / sort inputs shared by UC-95
/// (View Account List) and UC-99 (Search and Filter Accounts). Both query objects
/// implement this so the two endpoints run through a single executor with no
/// duplicated logic.
/// </summary>
public interface IAccountListCriteria
{
    int Page { get; }
    int PageSize { get; }

    string? Keyword { get; }
    string? RoleCode { get; }
    string? SubRole { get; }
    string? Status { get; }
    string? CampusId { get; }
    string? DepartmentId { get; }
    string? ProviderType { get; }
    string? CreatedVia { get; }

    /// <summary>INTERNAL | VISITOR | ALL.</summary>
    string? AccountType { get; }
    bool? HasCampus { get; }

    DateTime? FromDate { get; }
    DateTime? ToDate { get; }
    DateTime? LastLoginFrom { get; }
    DateTime? LastLoginTo { get; }

    string? SortBy { get; }
    string? SortDirection { get; }
}
