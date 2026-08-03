using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// Assembles the <see cref="VisitSetupSnapshot"/> around an already-rendered report artifact.
///
/// <para>
/// The report DTO carried by the artifact is the bulk of it and is reused verbatim, so the agenda in
/// the email body and the agenda in the attached PDF are literally the same list. Only the few extra
/// shareable fields the PDF does not carry are read here.
/// </para>
/// </summary>
public static class VisitSetupSnapshotBuilder
{
    /// <summary>
    /// Preparation items whose existence is safe to confirm to the guest. Everything else on the row —
    /// who was asked, who accepted, the coordination notes between departments — stays behind.
    /// </summary>
    public static async Task<VisitSetupSnapshot> BuildAsync(
        IApplicationDbContext db,
        VisitRequestCampus instance,
        ScheduleReportArtifact artifact,
        CancellationToken cancellationToken)
    {
        var report = artifact.Data
            ?? throw new InvalidOperationException(
                "The report artifact carries no data; the email body and the PDF would be built from different reads.");

        var campusName = await db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var detail = await db.VisitInstanceFormDetails
            .Where(d => d.VisitInstanceId == instance.VisitInstanceId)
            .Select(d => new { d.WorkingContent, d.TransportationNote })
            .FirstOrDefaultAsync(cancellationToken);

        // Projected to exactly the shareable columns. Selecting the entity and mapping afterwards
        // would put the internal notes in memory next to the code that writes the email, which is
        // the shape this whole flow is trying not to have.
        var logistics = await db.VisitLogisticsItems
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .OrderBy(l => l.LogisticsItemId)
            .Select(l => new VisitSetupLogisticsRow(
                l.ItemType, l.Title, l.Quantity, l.UsageStartAt, l.UsageEndAt, l.Status))
            .ToListAsync(cancellationToken);

        return new VisitSetupSnapshot(
            report,
            campusName,
            detail?.WorkingContent,
            detail?.TransportationNote,
            logistics,
            artifact.GeneratedAt);
    }
}
