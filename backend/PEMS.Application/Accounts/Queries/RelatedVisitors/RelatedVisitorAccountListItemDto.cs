namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// One row in the Staff Leader "Related Visitor Accounts" tab. Read-only: every management
/// capability flag is hard false so the UI (and a direct API caller) can never act on a
/// Visitor from here. Deliberately omits all sensitive columns (password/tokens/stamps).
/// </summary>
public sealed class RelatedVisitorAccountListItemDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Phone { get; init; }
    public string? Nationality { get; init; }

    public string RoleCode { get; init; } = "VISITOR";
    public string Status { get; init; } = default!;
    public string? CreatedVia { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    /// <summary>How many visible visit requests link this Visitor to the Staff Leader's campus.</summary>
    public int RelatedRequestCount { get; init; }

    /// <summary>Most recent submission time across the visible related requests.</summary>
    public DateTime? LastRelatedRequestAt { get; init; }

    /// <summary>Latest planned visit start across the visible campus instances.</summary>
    public DateTime? LatestPlannedStartAt { get; init; }

    public bool CanViewDetails { get; init; } = true;
    public bool CanManageStatus { get; init; }
    public bool CanUpdateRole { get; init; }
    public bool CanResetPassword { get; init; }
}
