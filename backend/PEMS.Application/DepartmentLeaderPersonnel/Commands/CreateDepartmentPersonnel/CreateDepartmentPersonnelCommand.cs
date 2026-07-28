using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.CreateDepartmentPersonnel;

/// <summary>
/// Spec §10 — provisions a new member of the caller's department.
///
/// The payload carries identity fields ONLY. <c>roleCode</c>, <c>subRole</c>, <c>departmentId</c>,
/// <c>campusId</c>, <c>status</c> and <c>createdVia</c> are all assigned by the server from the
/// verified scope, so a crafted request cannot create a Leader, plant an account in another
/// department, or skip the email-confirmation gate.
/// </summary>
public sealed class CreateDepartmentPersonnelCommand : IRequest<CreateDepartmentPersonnelResponse>
{
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>MALE / FEMALE / OTHER.</summary>
    public string? Gender { get; init; }
}

public sealed class CreateDepartmentPersonnelResponse
{
    public bool Success { get; init; }
    public ulong UserId { get; init; }
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public string? Gender { get; init; }

    /// <summary>Always PENDING_EMAIL_CONFIRMATION — a new account is never born active.</summary>
    public required string Status { get; init; }
    public required string SubRole { get; init; }

    /// <summary>SENT / SKIPPED / FAILED — the truthful outcome, never assumed from the HTTP status.</summary>
    public required string EmailNotificationStatus { get; init; }

    public required string Message { get; init; }
}
