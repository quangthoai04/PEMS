using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceReminderSettings;

/// <summary>
/// Loads the saved "Cảnh báo & Thông báo" schedule rows for a campus instance. Visible to the
/// instance host, the coordinating Staff Leader of the campus, and HO (read-only).
/// </summary>
public sealed record GetVisitInstanceReminderSettingsQuery(ulong VisitInstanceId)
    : IRequest<GetVisitInstanceReminderSettingsResponse>;
