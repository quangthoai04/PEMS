using System;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Reports.Common;

/// <summary>
/// Which slice of a person's work a performance report covers. The two senders that use
/// <c>REPORT_PERSONNEL_PERFORMANCE</c> measure different things — a campus report covers visits the
/// person joined or hosted, a department report covers the assignments they were given — and the
/// template says so through its <c>scopeLabel</c> variable rather than through a second template.
/// </summary>
public enum PersonnelReportScope
{
    /// <summary>A Student who joined visits as support (C-27).</summary>
    VisitSupport,

    /// <summary>Staff who hosted delegations (C-27).</summary>
    DelegationHosting,

    /// <summary>Department personnel working the assignments they received (C-28).</summary>
    VisitAssignments,
}

/// <summary>
/// The words behind <c>scopeLabel</c>.
///
/// <para>
/// They live here, beside the callers that choose the scope, and NOT in the renderer: the renderer
/// substitutes whatever variable it is handed and must stay ignorant of what any particular value means.
/// Keeping both languages together is what lets a caller send an EN report later without either
/// hard-coding a second phrase at the call site or teaching the template engine about Vietnamese.
/// </para>
/// </summary>
public static class PersonnelReportScopes
{
    public static string Label(PersonnelReportScope scope, string language)
    {
        var en = string.Equals(EmailLanguages.Normalize(language), EmailLanguages.En, StringComparison.Ordinal);

        return scope switch
        {
            PersonnelReportScope.VisitSupport => en ? "visit support" : "tham gia tiếp khách",
            PersonnelReportScope.DelegationHosting => en ? "delegation hosting" : "phụ trách đoàn khách",
            PersonnelReportScope.VisitAssignments => en ? "visit assignments" : "nhiệm vụ tiếp khách",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };
    }
}
