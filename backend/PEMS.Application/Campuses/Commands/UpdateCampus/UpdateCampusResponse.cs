using System;

namespace PEMS.Application.Campuses.Commands.UpdateCampus;

/// <summary>UC-85 §7 response — the updated campus master data + audit stamps.</summary>
public sealed class UpdateCampusResponse
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string City { get; init; } = null!;
    public string Address { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTime UpdatedAt { get; init; }
    public ulong? UpdatedBy { get; init; }
}
