using System;
using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetDepartmentPersonnelDetail;

/// <summary>
/// Spec §11 — one personnel record inside the caller's department. The only client input is the target
/// id; the department is the caller's verified scope, and a target outside it answers 404 exactly like
/// a non-existent id (spec §11 anti-enumeration).
/// </summary>
public sealed record GetDepartmentPersonnelDetailQuery(ulong UserId)
    : IRequest<GetDepartmentPersonnelDetailResponse>;

/// <summary>
/// The detail modal's data source. Deliberately omits <c>password_hash</c>, raw/hashed confirmation
/// tokens, refresh tokens and auth-provider subjects — none of them belong in a management screen
/// (spec §11).
/// </summary>
public sealed class GetDepartmentPersonnelDetailResponse
{
    public required ulong UserId { get; init; }
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>MALE / FEMALE / OTHER, or null when not recorded.</summary>
    public string? Gender { get; init; }

    public required string Status { get; init; }
    public required string RoleCode { get; init; }
    public string? SubRole { get; init; }
    public required string Position { get; init; }
    public string? AvatarUrl { get; init; }

    public required ulong DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public required ulong CampusId { get; init; }
    public required string CampusName { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    public bool CanEdit { get; init; }
    public bool CanDisable { get; init; }
    public bool CanEnable { get; init; }
    public bool CanTransferLeadershipTo { get; init; }
    public bool CanResendEmailConfirmation { get; init; }

    /// <summary>True when this row is the department's seated head.</summary>
    public bool IsCurrentDepartmentLeader { get; init; }
}
