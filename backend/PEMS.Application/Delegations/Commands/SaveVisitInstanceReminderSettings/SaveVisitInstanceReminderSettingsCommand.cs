using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Commands.SaveVisitInstanceReminderSettings;

/// <summary>
/// Upserts the full set of reminder schedule rows for a campus instance (host-only, prep window).
/// Each item is keyed by (channel, target_group); an existing row is updated in place. enabled=false
/// cancels the matching PENDING row. Nothing is sent on save — a background job dispatches later.
/// </summary>
public sealed record SaveVisitInstanceReminderSettingsCommand(
    ulong VisitInstanceId,
    List<SaveVisitReminderSettingItem> Items) : IRequest<SaveVisitInstanceReminderSettingsResponse>;

/// <summary>One desired reminder configuration. channel/targetGroup are SQL ENUM strings; offsetMinutes
/// is minutes before the visit's planned start; enabled=false cancels the row.</summary>
public sealed record SaveVisitReminderSettingItem(
    string Channel,
    string TargetGroup,
    int OffsetMinutes,
    bool Enabled);
