namespace PEMS.Application.Accounts.Common;

/// <summary>
/// One row in the Account List (UC-95) / Search &amp; Filter (UC-99) result.
/// Deliberately omits all sensitive columns (password_hash, provider_subject,
/// tokens, security stamps). Profile fields included here are the same ones the
/// admin UI already displays.
/// </summary>
public sealed class AccountListItemDto
{
    public ulong UserId { get; init; }
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string? Phone { get; init; }
    public PEMS.Domain.Enums.Gender? Gender { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Nationality { get; init; }
    public string? StudentCode { get; init; }

    public string RoleCode { get; init; } = default!;
    public string RoleName { get; init; } = default!;
    public string? SubRole { get; init; }

    public ulong? CampusId { get; init; }
    public string? CampusCode { get; init; }
    public string? CampusName { get; init; }

    public ulong? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }

    public string Status { get; init; } = default!;
    public string? CreatedVia { get; init; }

    public IReadOnlyList<string> Providers { get; init; } = Array.Empty<string>();

    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    /// <summary>True when the caller may open this row's details (UC-98).</summary>
    public bool CanViewDetails { get; init; }

    /// <summary>True when the caller may change this row's role (UC-100).</summary>
    public bool CanUpdateRole { get; init; }

    /// <summary>True when the caller may lock/unlock this row (UC-97).</summary>
    public bool CanManageStatus { get; init; }
}
