namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// One row of <c>minute_action_items</c> (đầu mục công việc). Mapped 1:1 to the real SQL columns.
/// </summary>
public sealed class MinuteActionItemDto
{
    public ulong ActionItemId { get; set; }
    public ulong MinutesId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    /// <summary>Người phụ trách — Host hoặc participant ACCEPTED (IC_SUPPORT/DEPT_SUPPORT/STUDENT).
    /// Null = chưa gán.</summary>
    public ulong? AssignedToUserId { get; set; }
    /// <summary>Tên hiển thị của người phụ trách (join Users tại thời điểm đọc) — null khi chưa gán.</summary>
    public string? AssignedToUserName { get; set; }
    /// <summary>SQL DATE column; serialized as a datetime at midnight.</summary>
    public DateTime? DueDate { get; set; }
    /// <summary>TODO | IN_PROGRESS | DONE | CANCELLED (matches the SQL enum).</summary>
    public string Status { get; set; } = "TODO";
    public DateTime? CompletedAt { get; set; }
    public int DisplayOrder { get; set; }
}
