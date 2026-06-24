using System;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Read model row for the HO campus list (UC-82 / UC-83). Includes the mandatory
/// <see cref="CampusCode"/> column and the IC Head name resolved via LEFT JOIN.
/// </summary>
public sealed class CampusListItemDto
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? City { get; init; }
    public ulong? IcHeadUserId { get; init; }

    /// <summary>IC Head full name, or null when unassigned (UI shows "Chưa phân công").</summary>
    public string? IcHeadName { get; init; }

    public string Status { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Whether the caller may toggle this campus' status (UC-86). True for HO/ADMIN on any
    /// campus; surfaced so the UI can hide the toggle for unauthorized callers.
    /// </summary>
    public bool CanManageStatus { get; init; }
}
