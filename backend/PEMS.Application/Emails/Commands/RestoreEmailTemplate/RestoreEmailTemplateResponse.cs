using System;

namespace PEMS.Application.Emails.Commands.RestoreEmailTemplate;

/// <summary>
/// What the template now holds. The restored content comes back with the response so the screen shows
/// the canonical wording without a second round trip — and, more to the point, so it cannot keep
/// displaying the operator's discarded text next to a revision that no longer matches it.
/// </summary>
public sealed class RestoreEmailTemplateResponse
{
    public ulong EmailTemplateId { get; set; }
    public string TemplateCode { get; set; } = null!;
    public bool Success { get; set; } = true;
    public string? Message { get; set; }

    /// <summary>The new concurrency token — one more than the revision the caller sent.</summary>
    public uint Revision { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? SubjectVi { get; set; }
    public string? BodyVi { get; set; }
    public string? SubjectEn { get; set; }
    public string? BodyEn { get; set; }

    // ContactSettings and ContactSettingsRestored were here. "Khôi phục mặc định" restored two things at
    // once — content and a contact policy — because leaving one behind could produce a template neither
    // half would accept. Restore is one thing again, so the response has one shape and the screen has no
    // "what was and was not put back" to explain.
}
