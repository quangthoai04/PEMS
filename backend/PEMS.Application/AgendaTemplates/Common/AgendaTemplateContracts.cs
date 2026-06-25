using System.Collections.Generic;

namespace PEMS.Application.AgendaTemplates.Common;

/// <summary>Template timeline item as submitted by the client (create/update).
/// Uses relative <c>StartOffsetMinutes</c> + <c>DurationMinutes</c>; never absolute TIME.</summary>
public sealed class AgendaTemplateItemInput
{
    public int DisplayOrder { get; set; }
    public int StartOffsetMinutes { get; set; }
    public int DurationMinutes { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ResponsibleRoleLabel { get; set; }
}

/// <summary>Template timeline item returned to the client (detail / default lookup / setup).</summary>
public sealed record AgendaTemplateItemView(
    ulong AgendaTemplateItemId,
    int DisplayOrder,
    int StartOffsetMinutes,
    int DurationMinutes,
    string Title,
    string? Description,
    string? Location,
    string? ResponsibleRoleLabel);

/// <summary>Resolved template header + ordered items (detail / default lookup / setup preview).</summary>
public sealed record AgendaTemplateView(
    ulong AgendaTemplateId,
    ulong? CampusId,
    string CampusScopeKey,
    string VisitType,
    string Name,
    string? Description,
    string Status,
    IReadOnlyList<AgendaTemplateItemView> Items);

/// <summary>Lightweight template row for lists / selectable dropdowns.</summary>
public sealed record AgendaTemplateSummary(
    ulong AgendaTemplateId,
    ulong? CampusId,
    string CampusScopeKey,
    string VisitType,
    string Name,
    string? Description,
    string Status,
    int ItemCount,
    bool IsDefault);

/// <summary>A concrete visit_agendas row (absolute DATETIME). Returned by the setup view and apply
/// command so the UI can render the real agenda with both date and time.</summary>
public sealed record AgendaRowView(
    ulong AgendaId,
    int SequenceOrder,
    string Title,
    DateTime StartTime,
    DateTime? EndTime,
    string? Location,
    string? Description,
    ulong? SourceTemplateItemId);
