namespace PEMS.Application.Accounts.Commands.ReplaceStaffLeader;

public sealed class ReplaceStaffLeaderResponse
{
    public ulong CampusId { get; init; }
    public ulong IcDepartmentId { get; init; }
    public ulong OldLeaderUserId { get; init; }
    public ulong NewLeaderUserId { get; init; }
    public string NewLeaderEmail { get; init; } = default!;

    /// <summary>UC-13-style notification outcome for the new leader email: SENT | FAILED.</summary>
    public string EmailNotificationStatus { get; init; } = default!;

    public string Message { get; init; } = "Thay thế Staff Leader thành công.";
}
