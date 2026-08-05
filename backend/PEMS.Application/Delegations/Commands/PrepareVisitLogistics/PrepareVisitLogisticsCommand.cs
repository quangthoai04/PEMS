using MediatR;
using PEMS.Application.Emails.Preview;

namespace PEMS.Application.Delegations.Commands.PrepareVisitLogistics;

/// <summary>
/// Host sends a logistics/resource request for a campus instance to a GENERAL department. Creates a
/// visit_logistics_items row (status REQUESTED), notifies the department's active leader and emails
/// them a login-required "view detail" link (no public accept/decline token). Host-only, prep window.
/// </summary>
public sealed record PrepareVisitLogisticsCommand(
    ulong VisitInstanceId,
    ulong? DepartmentId,        // required for SYSTEM_REQUEST; optional for OFFLINE_COORDINATED
    string ItemType,            // ROOM | TRANSPORT | MEAL | EQUIPMENT | BANNER | LED | OTHER
    string Title,
    string? Description,
    int? Quantity,
    string? UsageStartAt,       // "yyyy-MM-ddTHH:mm[:ss]" wall-clock
    string? UsageEndAt,
    string? CoordinationMode = null,        // SYSTEM_REQUEST (default) | OFFLINE_COORDINATED
    string? OfflineCoordinationNote = null, // required when OFFLINE_COORDINATED
    /// <summary>
    /// The message the Host edited and approved in the FINAL preview, or null to send the template.
    /// Ignored for an OFFLINE_COORDINATED item, which sends no email at all.
    /// </summary>
    ApprovedEmailContent? ApprovedContent = null) : IRequest<PrepareVisitLogisticsResponse>;

public static class LogisticsItemTypes
{
    public static readonly string[] All = { "ROOM", "TRANSPORT", "MEAL", "EQUIPMENT", "BANNER", "LED", "OTHER" };
}

public static class LogisticsCoordinationModes
{
    public const string SystemRequest = "SYSTEM_REQUEST";
    public const string OfflineCoordinated = "OFFLINE_COORDINATED";
    public static readonly string[] All = { SystemRequest, OfflineCoordinated };
}
