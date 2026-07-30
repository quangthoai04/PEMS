using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Minutes.Queries.GetMyActionItems;

/// <summary>
/// "Quản lý việc sau tiếp khách" — the action items (minute_action_items) assigned to the CALLER
/// only, never a team/leader-wide view. Filters/paginates/sorts server-side so the list page never
/// has to fetch everything.
/// </summary>
public sealed class GetMyActionItemsQuery : IRequest<GetMyActionItemsDto>
{
    /// <summary>Search theo tên công việc hoặc tên đoàn khách.</summary>
    public string? Q { get; set; }
    /// <summary>
    /// Deep-link từ chuông thông báo (giao việc/nhắc hạn) — khi có giá trị, trả về ĐÚNG 1 item này
    /// (bỏ qua Q/Status/FromDate/ToDate) bất kể trạng thái/hạn hiện tại của nó.
    /// </summary>
    public ulong? ActionItemId { get; set; }
    /// <summary>ALL (mặc định) | TODO (gộp cả IN_PROGRESS — "chưa làm") | DONE | CANCELLED.</summary>
    public string? Status { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    /// <summary>asc | desc theo due_date. Mặc định asc (gần hạn nhất lên trước).</summary>
    public string? SortDir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class GetMyActionItemsDto
{
    public List<MyActionItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public sealed class MyActionItemDto
{
    public ulong ActionItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    public System.DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public ulong VisitInstanceId { get; set; }
    public ulong? VisitRequestId { get; set; }
    public string DelegationName { get; set; } = string.Empty;
}
