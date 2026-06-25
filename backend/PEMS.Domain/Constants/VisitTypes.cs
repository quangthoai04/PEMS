using System.Collections.Generic;

namespace PEMS.Domain.Constants;

/// <summary>
/// Canonical visit-type values. Mirrors the MySQL ENUM visit_requests.visit_type and is reused by
/// the agenda template module (agenda_templates.visit_type, agenda_template_defaults.visit_type).
/// </summary>
public static class VisitTypes
{
    public const string CampusTour = "CAMPUS_TOUR";
    public const string Meeting = "MEETING";
    public const string Workshop = "WORKSHOP";
    public const string SigningCeremony = "SIGNING_CEREMONY";
    public const string Exchange = "EXCHANGE";
    public const string Other = "OTHER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        CampusTour,
        Meeting,
        Workshop,
        SigningCeremony,
        Exchange,
        Other,
    };

    public static bool IsValid(string? value) => value != null && All.Contains(value);
}

/// <summary>Agenda template / default scope helpers. campus_scope_key = "GLOBAL" when campus-less,
/// otherwise the campus id as a string (consistent with the DB scope triggers).</summary>
public static class AgendaScope
{
    public const string Global = "GLOBAL";

    /// <summary>The canonical campus_scope_key for a (nullable) campus id.</summary>
    public static string KeyFor(ulong? campusId) => campusId.HasValue ? campusId.Value.ToString() : Global;
}

/// <summary>agenda_templates.status ENUM values.</summary>
public static class AgendaTemplateStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Active, Inactive };
}
