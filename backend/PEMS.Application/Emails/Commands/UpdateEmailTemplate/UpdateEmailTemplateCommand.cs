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
/// <para>
/// <b>Contact settings travel with the content.</b> They used to be a second call to a second endpoint,
/// which meant four things could go wrong that no longer can: an operator could save the wording and
/// forget the policy; the second call could fail and leave a body and a policy contradicting each other;
/// each call bumped its own idea of "current"; and the one rule that spans both — whether the body may
/// carry <c>{{contactInformationBlock}}</c> — had to be judged by each half against the other half's
/// STORED value, so a change to both at once was refused whichever way round it was made.
/// <see cref="ContactSettings"/> is optional: null means "leave the policy alone", which is what a caller
/// editing only wording sends, and what an unsupported template must send.
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

    /// <summary>
    /// The contact configuration to store alongside this content, or null to leave the stored policy
    /// untouched.
    ///
    /// <para>
    /// Null and "all defaults" are deliberately different. A caller that omits the object is saying
    /// nothing about the policy; one that sends an object with every field at its default value is saying
    /// those exact values. Treating a missing object as a default-valued one would let a client that
    /// simply had not implemented the field reset a template's policy on every wording fix.
    /// </para>
    /// </summary>
    public UpdateEmailTemplateContactSettings? ContactSettings { get; set; }
}

/// <summary>
/// The contact configuration as it arrives on the wire — the same field names the standalone
/// <c>PUT /contact-settings</c> endpoint accepts, so the client has one shape to build rather than two.
/// </summary>
public sealed class UpdateEmailTemplateContactSettings
{
    public string Requirement { get; set; } = nameof(PEMS.Domain.Enums.EmailContactRequirement.OPTIONAL);
    public string ContactSource { get; set; } = nameof(PEMS.Domain.Enums.EmailContactSource.SUPPORT_CONTACT);

    public bool ShowEmail { get; set; } = true;
    public bool ShowPhone { get; set; } = true;
    public bool ShowDepartment { get; set; }
    public bool ShowCampus { get; set; }
    public bool ShowSender { get; set; }

    public string? HeadingVi { get; set; }
    public string? HeadingEn { get; set; }

    public string ReplyToSource { get; set; } = nameof(PEMS.Domain.Enums.EmailReplyToSource.NONE);
}
