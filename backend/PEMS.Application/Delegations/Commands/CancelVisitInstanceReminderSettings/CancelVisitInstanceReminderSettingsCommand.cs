using MediatR;

namespace PEMS.Application.Delegations.Commands.CancelVisitInstanceReminderSettings;

/// <summary>
/// Cancels every still-PENDING reminder for a campus instance (host-only). SENT rows are never
/// touched. Used by the "tắt cảnh báo" action on VisitProcess.
/// </summary>
public sealed record CancelVisitInstanceReminderSettingsCommand(ulong VisitInstanceId)
    : IRequest<CancelVisitInstanceReminderSettingsResponse>;

public sealed record CancelVisitInstanceReminderSettingsResponse(int CancelledCount, string Message);
