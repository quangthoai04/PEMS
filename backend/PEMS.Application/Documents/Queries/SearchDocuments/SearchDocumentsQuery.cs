using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Documents.Queries.SearchDocuments;

public class SearchDocumentsQuery : IRequest<PaginatedResult<SearchDocumentsDto>>
{
    public string? Q { get; init; }
    public string? Status { get; init; }
    public string? OwnerType { get; init; }
    public string? DocumentCategory { get; init; }
    public string? StorageProvider { get; init; }
    public string? UploadedFrom { get; init; }
    public string? UploadedTo { get; init; }
    /// <summary>Optional — chỉ HO gửi lên để lọc còn tài liệu của đúng 1 campus.</summary>
    public ulong? CampusId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
}