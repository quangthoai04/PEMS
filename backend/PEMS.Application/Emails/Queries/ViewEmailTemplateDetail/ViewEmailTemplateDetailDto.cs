using System;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateDetail;

public sealed class ViewEmailTemplateDetailDto
{
    public ulong EmailTemplateId { get; set; }
    public string TemplateCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public ulong? CampusId { get; set; }
    public string? Description { get; set; }
    public string? SubjectVi { get; set; }
    public string? BodyVi { get; set; }
    public string? SubjectEn { get; set; }
    public string? BodyEn { get; set; }
    public string BodyFormat { get; set; } = null!;
    public string? VariablesText { get; set; }
    public string Status { get; set; } = null!;

    /// <summary>
    /// The optimistic-concurrency token the editor must send back on save or restore. Read it here, not
    /// from <see cref="UpdatedAt"/>: a DATETIME with second resolution cannot distinguish two saves
    /// inside the same second, and this can.
    /// </summary>
    public uint Revision { get; set; }

    /// <summary>Whether PEMS ships a default for this code — i.e. whether restore is offered at all.</summary>
    public bool HasShippedDefault { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}