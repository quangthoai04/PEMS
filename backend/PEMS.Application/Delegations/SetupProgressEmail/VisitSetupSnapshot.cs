using System;
using System.Collections.Generic;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// One preparation item as the GUEST may see it: what was asked for, how much, when it is needed and
/// where it has got to. Deliberately none of the operational detail around it.
/// </summary>
/// <param name="Status">
/// The workflow status (REQUESTED / ACCEPTED / …). It is shareable because it answers the guest's
/// actual question — is this arranged — without naming who is arranging it.
/// </param>
public sealed record VisitSetupLogisticsRow(
    string ItemType,
    string Title,
    int? Quantity,
    DateTime? UsageStartAt,
    DateTime? UsageEndAt,
    string Status);

/// <summary>
/// Everything the setup-progress email is allowed to tell the guest, read ONCE.
///
/// <para>
/// The point of a single object is that the PDF and the HTML tables in the message body cannot
/// describe different states of the world. Both are produced from this instance: if they disagree,
/// the disagreement is a rendering bug rather than two reads that happened at different moments.
/// <see cref="GeneratedAt"/> is the moment that read happened, and it is printed in the email so the
/// guest knows how current it is.
/// </para>
///
/// <para>
/// <b>What is deliberately NOT here, and why.</b> This record is the allow-list — a field that never
/// reaches it cannot leak, whatever a future renderer does.
/// </para>
/// <list type="bullet">
/// <item><c>preparation_note</c> — the Host's internal briefing (staffing, talking points).</item>
/// <item><c>offline_coordination_note</c>, assignee / requester / receiver identities — internal ops.</item>
/// <item><c>notes</c> — the guest's general remark to FPTU, a handling note rather than a status they asked to be told.</item>
/// <item>Audit rows, incident/issue records, and any FPT-only commentary about the delegation.</item>
/// </list>
/// </summary>
/// <param name="Report">The same DTO the PDF is drawn from — delegation, times, both people lists, agenda.</param>
/// <param name="CampusName">Receiving campus, for the overview table.</param>
/// <param name="WorkingContent">The guest's own stated working content, echoed back for confirmation.</param>
/// <param name="TransportationNote">
/// The guest's own transport request. Guest-authored, so echoing it back reveals nothing they did not
/// write; it is included because "yêu cầu bổ sung" is exactly what they want confirmed.
/// </param>
public sealed record VisitSetupSnapshot(
    ScheduleReportDto Report,
    string CampusName,
    string? WorkingContent,
    string? TransportationNote,
    IReadOnlyList<VisitSetupLogisticsRow> Logistics,
    DateTime GeneratedAt);
