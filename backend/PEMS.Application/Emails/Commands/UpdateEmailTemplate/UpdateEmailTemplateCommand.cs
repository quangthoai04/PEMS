using System;
using MediatR;

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

/// <summary>
/// Updates the CONTENT of a system email template (UC-44). The catalog itself is fixed in code (G11-I),
/// so this command carries only the fields an operator owns.
///
/// <para>
/// It used to carry <c>TemplateCode</c>, <c>Purpose</c>, <c>CampusId</c>, <c>BodyFormat</c>,
/// <c>VariablesText</c> and <c>Status</c> as well, and the handler assigned every one of them straight
/// onto the entity. A caller could therefore move a template to another module, rewrite the variable
/// contract the renderer validates against, or deactivate it — all through the "update content"
/// endpoint, and none of it visible in the screen that was meant to be the only way in. Those properties
/// are removed from the contract rather than merely ignored: a field the API accepts and silently drops
/// is a promise nobody keeps.
/// </para>
/// </summary>
public class UpdateEmailTemplateCommand : IRequest<UpdateEmailTemplateResponse>
{
    public ulong EmailTemplateId { get; set; }

    /// <summary>Display name in the management list. Not sent to anybody.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Administrative note. Not sent to anybody.</summary>
    public string? Description { get; set; }

    public string? SubjectVi { get; set; }
    public string? BodyVi { get; set; }
    public string? SubjectEn { get; set; }
    public string? BodyEn { get; set; }

    /// <summary>
    /// The <c>revision</c> the editor loaded. The save is applied only if the database still holds that
    /// revision, in the same statement that bumps it; otherwise it is refused with
    /// <c>EMAIL_TEMPLATE_CONCURRENCY_CONFLICT</c> and nothing is written. Two people editing the same
    /// message is not hypothetical — one HO team, thirty shared templates — and last-write-wins discards
    /// one person's wording with no trace that it existed.
    ///
    /// <para>
    /// This replaced an <c>ExpectedUpdatedAt</c> timestamp. That token could not do the job: the column
    /// is DATETIME with no fractional part, so two saves landing inside the same second stored an
    /// identical stamp, compared equal, and the second one silently overwrote the first — the exact
    /// failure the check existed to prevent, left open at the resolution where concurrent edits actually
    /// collide. A monotonic integer has no such blind spot.
    /// </para>
    /// <para>
    /// Required. A caller who omits it is refused rather than defaulted to "whatever is stored", because
    /// defaulting would turn every scripted call into an unconditional overwrite.
    /// </para>
    /// </summary>
    public uint? ExpectedRevision { get; set; }
}
