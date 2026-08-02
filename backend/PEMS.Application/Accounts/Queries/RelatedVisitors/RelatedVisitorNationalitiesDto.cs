using System.Collections.Generic;

namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// The nationality options of the Staff Leader's related-Visitor filter. Values are the real,
/// de-duplicated nationalities of every Visitor related to the caller's campus — never a
/// hardcoded country catalogue, and never limited to one page of the Visitor list.
/// </summary>
public sealed class RelatedVisitorNationalitiesDto
{
    /// <summary>Trimmed, non-empty, case-insensitively distinct, sorted (vi-VN).</summary>
    public IReadOnlyList<string> Items { get; init; } = new List<string>();
}
