using System;

namespace PEMS.Application.Delegations.Commands.SendVisitAgendaEmail;

/// <param name="Status">SENT / SKIPPED / FAILED — same vocabulary as SystemEmailDispatchResult.NotificationStatus.</param>
public sealed record SendVisitAgendaEmailResponse(
    string Status,
    DateTime SentAt,
    string Message);
