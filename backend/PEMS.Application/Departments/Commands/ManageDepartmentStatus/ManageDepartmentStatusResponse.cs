using System;

namespace PEMS.Application.Departments.Commands.ManageDepartmentStatus;

public sealed class ManageDepartmentStatusResponse
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;
    public string OldStatus { get; init; } = default!;
    public string NewStatus { get; init; } = default!;

    /// <summary>Current status after the operation (kept for backward compatibility).</summary>
    public string Status { get; init; } = default!;

    /// <summary>False when the request was a no-op (target status == current status).</summary>
    public bool Changed { get; init; }

    /// <summary>UC-106 disable: DEPARTMENT LEADER/STAFF accounts that lose system access.</summary>
    public int AffectedAccountCount { get; init; }

    /// <summary>UC-106 disable: active sessions revoked in the same transaction.</summary>
    public int RevokedSessionCount { get; init; }

    public DateTime? UpdatedAt { get; init; }
    public string Message { get; init; } = default!;
}
