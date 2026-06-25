using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.AgendaTemplates.Common;

/// <summary>
/// Resolves the default agenda template for a (campus scope, visit type) pair. Precedence:
/// campus-specific default first, then GLOBAL fallback. Only ACTIVE, non-deleted templates count.
/// </summary>
public static class AgendaDefaultResolver
{
    public static async Task<(ulong? TemplateId, string? Scope)> ResolveAsync(
        IApplicationDbContext db, ulong? campusId, string visitType, CancellationToken ct)
    {
        if (campusId is not null)
        {
            var scopeKey = AgendaScope.KeyFor(campusId);
            var campusDefault = await (
                from d in db.AgendaTemplateDefaults
                join t in db.AgendaTemplates on d.AgendaTemplateId equals t.AgendaTemplateId
                where d.CampusScopeKey == scopeKey && d.VisitType == visitType
                      && t.Status == AgendaTemplateStatuses.Active && t.DeletedAt == null
                select (ulong?)t.AgendaTemplateId).FirstOrDefaultAsync(ct);
            if (campusDefault is not null)
                return (campusDefault, "CAMPUS");
        }

        var globalDefault = await (
            from d in db.AgendaTemplateDefaults
            join t in db.AgendaTemplates on d.AgendaTemplateId equals t.AgendaTemplateId
            where d.CampusScopeKey == AgendaScope.Global && d.VisitType == visitType
                  && t.Status == AgendaTemplateStatuses.Active && t.DeletedAt == null
            select (ulong?)t.AgendaTemplateId).FirstOrDefaultAsync(ct);
        if (globalDefault is not null)
            return (globalDefault, "GLOBAL");

        return (null, null);
    }

    /// <summary>Load a template header + ordered items as a read DTO, or null when not found.</summary>
    public static async Task<AgendaTemplateView?> LoadViewAsync(
        IApplicationDbContext db, ulong templateId, CancellationToken ct)
    {
        var t = await db.AgendaTemplates.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.AgendaTemplateId == templateId, ct);
        if (t is null)
            return null;

        var items = t.Items
            .OrderBy(i => i.StartOffsetMinutes).ThenBy(i => i.DisplayOrder)
            .Select(i => new AgendaTemplateItemView(
                i.AgendaTemplateItemId,
                (int)i.DisplayOrder,
                (int)i.StartOffsetMinutes,
                (int)i.DurationMinutes,
                i.Title,
                i.Description,
                i.Location,
                i.ResponsibleRoleLabel))
            .ToList();

        return new AgendaTemplateView(
            t.AgendaTemplateId, t.CampusId, t.CampusScopeKey, t.VisitType,
            t.Name, t.Description, t.Status, items);
    }
}
