namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// One row of <c>minute_action_items</c> (đầu mục công việc). Mapped 1:1 to the real SQL columns —
/// the table intentionally has NO assignee column ("Không gán người phụ trách"), so this DTO has none.
/// </summary>
public sealed class MinuteActionItemDto
{
    public ulong ActionItemId { get; set; }
    public ulong MinutesId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    /// <summary>SQL DATE column; serialized as a datetime at midnight.</summary>
    public DateTime? DueDate { get; set; }
    /// <summary>TODO | IN_PROGRESS | DONE | CANCELLED (matches the SQL enum).</summary>
    public string Status { get; set; } = "TODO";
    public DateTime? CompletedAt { get; set; }
    public int DisplayOrder { get; set; }
}
