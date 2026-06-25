using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.AgendaTemplates;

/// <summary>
/// Maps a (campus scope, visit type) pair to its default agenda template. Lookup precedence when
/// a host opens agenda setup: campus-specific scope first, then GLOBAL fallback.
/// </summary>
[Table("agenda_template_defaults")]
public class AgendaTemplateDefault
{
    [Key]
    [Column("agenda_template_default_id")]
    public ulong AgendaTemplateDefaultId { get; set; }

    [Column("campus_id")]
    public ulong? CampusId { get; set; }

    /// <summary>"GLOBAL" when <see cref="CampusId"/> is null, otherwise the campus id as a string
    /// (normalised by the DB trigger trg_agenda_template_defaults_scope_*).</summary>
    [Column("campus_scope_key")]
    public string CampusScopeKey { get; set; } = "GLOBAL";

    /// <summary>One of <see cref="PEMS.Domain.Constants.VisitTypes"/>.</summary>
    [Column("visit_type")]
    public string VisitType { get; set; } = default!;

    [Column("agenda_template_id")]
    public ulong AgendaTemplateId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }
}
