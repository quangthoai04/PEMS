using MediatR;
using PEMS.Shared;

namespace PEMS.Application.Delegations.VisitPhotos.Queries.GetMyVisitPhotoFolders;

/// <summary>
/// The Student's "Quản lý ảnh đoàn khách" table: one row per campus instance the caller has an
/// ACCEPTED STUDENT participation in. Paged + searchable by delegation name.
/// </summary>
public sealed class GetMyVisitPhotoFoldersQuery : IRequest<PagedResult<MyVisitPhotoFolderItemDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortDirection { get; set; } // "ASC" or "DESC" (default)
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class MyVisitPhotoFolderItemDto
{
    public ulong VisitInstanceId { get; init; }
    public string DelegationName { get; init; } = string.Empty;
    public string? CampusName { get; init; }
    public string? FolderName { get; init; }
    public int ActivePhotoCount { get; init; }
    public string InstanceStatus { get; init; } = string.Empty;
    public DateTime PlannedStartAt { get; init; }
}
