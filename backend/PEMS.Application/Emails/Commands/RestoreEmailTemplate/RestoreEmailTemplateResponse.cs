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

    /// <summary>
    /// The contact settings after the restore, or null on a template that cannot carry the block.
    ///
    /// <para>
    /// Null is the correct answer for an unsupported template and is NOT reported as a failure: there is
    /// no contact configuration to put back, so restoring one would be inventing a policy the send path
    /// ignores. Refusing the whole restore with <c>CONTACT_NOT_SUPPORTED</c> — which the standalone
    /// contact-restore endpoint does, correctly, because that endpoint has nothing else to do — would
    /// block the CONTENT restore on those four templates for a reason that has nothing to do with content.
    /// </para>
    /// </summary>
    public PEMS.Application.Emails.Contact.EmailContactSettingsDto? ContactSettings { get; set; }

    /// <summary>
    /// True when the contact policy was part of what this restore replaced. False on an unsupported
    /// template, so the screen can say what was and was not put back instead of implying it did more.
    /// </summary>
    public bool ContactSettingsRestored { get; set; }
}
