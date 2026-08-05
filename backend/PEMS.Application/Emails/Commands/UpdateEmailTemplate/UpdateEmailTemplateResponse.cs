using System;
using System.Collections.Generic;

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

/// <summary>One refused edit, addressed to a field and — where it applies — a variable.</summary>
public sealed class EmailTemplateIssueDto
{
    public string Field { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? VariableName { get; set; }
    public string MessageVi { get; set; } = null!;
    public string MessageEn { get; set; } = null!;
    public string Severity { get; set; } = null!;
}

/// <summary>
/// What was saved, in full.
///
/// <para>
/// The response used to carry only the id, the new revision and a timestamp, which was enough while the
/// screen kept its own copy of everything else. It is not enough now: the editor re-baselines its dirty
/// check from this object, and a baseline assembled from "what I sent" instead of "what was stored" would
/// be wrong wherever the two differ — headings are trimmed and stripped of markup on the way in, an empty
/// description is stored as NULL, and a heading equal to the shipped wording is stored as "inherit". Each
/// of those would leave the screen reporting an unsaved change immediately after a successful save.
/// </para>
/// </summary>
public sealed class UpdateEmailTemplateResponse
{
    public ulong EmailTemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }

    /// <summary>
    /// The new concurrency token — always exactly one more than the revision the caller sent, however many
    /// groups of fields the save touched. The editor keeps it so its own next save is not refused as a
    /// conflict with the change it just made.
    /// </summary>
    public uint Revision { get; set; }

    /// <summary>When the save landed. Displayed only; <see cref="Revision"/> is the concurrency token.</summary>
    public DateTime? UpdatedAt { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? SubjectVi { get; set; }
    public string? BodyVi { get; set; }
    public string? SubjectEn { get; set; }
    public string? BodyEn { get; set; }
}
