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

public sealed class UpdateEmailTemplateResponse
{
    public ulong EmailTemplateId { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }

    /// <summary>
    /// The new concurrency token — always exactly one more than the revision the caller sent. The editor
    /// keeps it so its own next save is not refused as a conflict with the change it just made.
    /// </summary>
    public uint Revision { get; set; }

    /// <summary>When the save landed. Displayed only; <see cref="Revision"/> is the concurrency token.</summary>
    public DateTime? UpdatedAt { get; set; }
}
