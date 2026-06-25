using PEMS.Application.AgendaTemplates.Common;

namespace PEMS.Application.AgendaTemplates.Queries.GetDefaultAgendaTemplate;

/// <summary><see cref="Resolved"/> is false (and <see cref="Template"/> null) when no ACTIVE default
/// exists for the scope/visit type. <see cref="ResolvedScope"/> is "CAMPUS" or "GLOBAL" when resolved.</summary>
public sealed record GetDefaultAgendaTemplateDto(
    bool Resolved,
    string? ResolvedScope,
    AgendaTemplateView? Template);
