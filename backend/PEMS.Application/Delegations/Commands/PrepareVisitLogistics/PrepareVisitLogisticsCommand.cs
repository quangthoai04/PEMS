using MediatR;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.Commands.PrepareVisitLogistics;

/// <summary>
/// Host sends a logistics/resource request for a campus instance to a GENERAL department. Creates a
/// visit_logistics_items row (status REQUESTED), notifies the department's active leader and emails
/// them a login-required "view detail" link (no public accept/decline token). Host-only, prep window.
/// </summary>
public sealed record PrepareVisitLogisticsCommand(
    ulong VisitInstanceId,
    ulong DepartmentId,
    string ItemType,            // ROOM | TRANSPORT | MEAL | EQUIPMENT | BANNER | LED | OTHER
    string Title,
    string? Description,
    int? Quantity,
    string? UsageStartAt,       // "yyyy-MM-ddTHH:mm[:ss]" wall-clock
    string? UsageEndAt,
    string? Priority,           // LOW | MEDIUM | HIGH | URGENT (default MEDIUM)
    string? DueAt,
    EmailOverride? EmailOverride = null) : IRequest<PrepareVisitLogisticsResponse>;

public static class LogisticsItemTypes
{
    public static readonly string[] All = { "ROOM", "TRANSPORT", "MEAL", "EQUIPMENT", "BANNER", "LED", "OTHER" };
}

public static class LogisticsPriorities
{
    public static readonly string[] All = { "LOW", "MEDIUM", "HIGH", "URGENT" };
}
