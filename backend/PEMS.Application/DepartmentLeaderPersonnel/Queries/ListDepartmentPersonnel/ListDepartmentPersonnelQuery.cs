using System;
using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.ListDepartmentPersonnel;

/// <summary>
/// Spec §9 — paged personnel list for the caller's own department.
///
/// There is deliberately NO <c>departmentId</c> parameter: the department comes from the verified
/// scope, which is what closes the IDOR the previous screen had (spec §5.1). Search, filter, sort and
/// paging are all applied in the database — the client never receives an unfiltered set.
/// </summary>
public sealed class ListDepartmentPersonnelQuery : IRequest<ListDepartmentPersonnelResponse>
{
    /// <summary>Free text matched against full name, email and phone (trimmed, ≤100 chars).</summary>
    public string? Keyword { get; init; }

    /// <summary>ALL / ACTIVE / INACTIVE / PENDING_EMAIL_CONFIRMATION / LOCKED. Null = ALL.</summary>
    public string? Status { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;

    /// <summary>fullName / email / status / createdAt. Null = the default leader-first ordering.</summary>
    public string? SortBy { get; init; }

    /// <summary>asc / desc. Ignored for the default ordering.</summary>
    public string? SortDirection { get; init; }
}

/// <summary>One personnel row. Security internals (hashes, tokens, provider subjects) never appear here.</summary>
public sealed class DepartmentPersonnelListItem
{
    public required ulong UserId { get; init; }
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>MALE / FEMALE / OTHER, or null when not recorded — never a display label.</summary>
    public string? Gender { get; init; }

    public required string Status { get; init; }
    public string? SubRole { get; init; }

    /// <summary>Display title derived from <see cref="SubRole"/> server-side.</summary>
    public required string Position { get; init; }

    public string? AvatarUrl { get; init; }
    public required string DepartmentName { get; init; }
    public required string CampusName { get; init; }
    public DateTime CreatedAt { get; init; }

    // ── Rendering hints; every command re-checks them server-side (spec §17). ──
    public bool CanView { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDisable { get; init; }
    public bool CanEnable { get; init; }
    public bool CanTransferLeadershipTo { get; init; }
    public bool CanResendEmailConfirmation { get; init; }
}

/// <summary>Standard paged envelope.</summary>
public sealed class ListDepartmentPersonnelResponse
{
    public IReadOnlyList<DepartmentPersonnelListItem> Items { get; init; }
        = Array.Empty<DepartmentPersonnelListItem>();

    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1 && TotalPages > 0;
}
