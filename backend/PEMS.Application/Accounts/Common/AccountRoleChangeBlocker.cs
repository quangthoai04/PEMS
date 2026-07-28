using System;
using System.Collections.Generic;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// One active responsibility group that prevents an account's role from being changed.
/// Serialized verbatim into the 409 response under <c>data.blockers</c> (spec §11.4), so it must
/// never carry PII — ids and counts only.
/// </summary>
public sealed class AccountRoleChangeBlocker
{
    /// <summary>Stable machine-readable group (see <see cref="AccountRoleChangeDependencyRule"/>).</summary>
    public string Type { get; init; } = default!;

    /// <summary>Responsibility rows in this group (participant rows, logistics items, …).</summary>
    public int Count { get; init; }

    /// <summary>Distinct visit instances behind <see cref="Count"/> (0 for department-head).</summary>
    public int AffectedVisitCount { get; init; }

    /// <summary>A few visit instance ids so the UI can deep-link; never the full list.</summary>
    public IReadOnlyList<ulong> SampleVisitInstanceIds { get; init; } = Array.Empty<ulong>();

    /// <summary>Vietnamese, user-facing explanation of this one group.</summary>
    public string Message { get; init; } = default!;
}

/// <summary>
/// Aggregated result of the role-change dependency check. <see cref="CanChangeRole"/> is the single
/// gate the handler consults — an empty blocker list is the only way a structural role change proceeds.
/// </summary>
public sealed class AccountRoleChangeImpact
{
    public IReadOnlyList<AccountRoleChangeBlocker> Blockers { get; init; }
        = Array.Empty<AccountRoleChangeBlocker>();

    public bool CanChangeRole => Blockers.Count == 0;

    /// <summary>Distinct visit instances across every blocker (never the sum of per-blocker counts).</summary>
    public int AffectedVisitCount { get; init; }
}
