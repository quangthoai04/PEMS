using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Emails.Queries.GetEmailTemplateContract;

/// <summary>
/// Asks the backend what a template's variables actually are (G11-J).
///
/// <para>
/// The template-management screen calls this before it validates anything. It used to validate against
/// a list compiled into the frontend, which belonged to no template in particular, so a canonical
/// template opened with warnings on every variable it legitimately used.
/// </para>
/// </summary>
public sealed class GetEmailTemplateContractQuery : IRequest<EmailTemplateContractDto>
{
    /// <summary>The system template code. Not the database id: the contract is a property of the code.</summary>
    public string TemplateCode { get; set; } = null!;

    /// <summary>VI or EN — decides which labels and preview samples come back.</summary>
    public string? Language { get; set; }
}

public sealed class EmailTemplateContractVariableDto
{
    public string Name { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Sample { get; set; } = null!;
    public bool Required { get; set; }
    public bool Sensitive { get; set; }
    public bool ForbiddenInSubject { get; set; }
}

public sealed class EmailTemplateContractDto
{
    public string TemplateCode { get; set; } = null!;

    /// <summary>Which part of the product the template belongs to (ACCOUNT, LOGISTICS, REPORT, …).</summary>
    public string Module { get; set; } = null!;

    /// <summary>
    /// Whether the code is a registered system template at all. False means the row is historical — it
    /// exists because a sent email or a draft still points at it — and it is not editable.
    /// </summary>
    public bool IsSystemTemplate { get; set; }

    public IReadOnlyList<EmailTemplateContractVariableDto> Variables { get; set; } =
        new List<EmailTemplateContractVariableDto>();

    /// <summary>Data variables only. A trusted block is never listed here — see the two block lists.</summary>
    public IReadOnlyList<string> AllowedVariables { get; set; } = new List<string>();
    public IReadOnlyList<string> RequiredVariables { get; set; } = new List<string>();
    public IReadOnlyList<string> OptionalVariables { get; set; } = new List<string>();

    /// <summary>
    /// System blocks the body must keep. The editor shows these as protected regions rather than as
    /// variables: the backend builds their markup, so an operator may move one but can neither author
    /// its contents nor supply a value for it.
    /// </summary>
    public IReadOnlyList<string> RequiredSystemBlocks { get; set; } = new List<string>();

    /// <summary>System blocks this template may legally carry but does not have to.</summary>
    public IReadOnlyList<string> OptionalSystemBlocks { get; set; } = new List<string>();

    /// <summary>
    /// Inert sample markup for each block this template allows, keyed by block name — what the editor's
    /// preview pane substitutes for the placeholder.
    ///
    /// <para>
    /// Built here, by the same <c>EmailComposition</c> helpers the send and the preview modal use, so the
    /// buttons carry the labels and styling a recipient will actually see. Reproducing them in the screen
    /// would be a second implementation that starts correct and drifts — and the operator would have no
    /// way to know which of the two the recipient gets. Every URL is <c>#</c>: a preview mints no tokens.
    /// </para>
    /// <para>
    /// <c>contactInformationBlock</c> is NOT here. Its appearance depends on the contact policy the
    /// operator is editing, including unsaved toggles, so it is fetched from the contact-block preview
    /// endpoint whenever that draft changes.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string> SystemBlockPreviews { get; set; } =
        new Dictionary<string, string>();

    public IReadOnlyList<string> SensitiveVariables { get; set; } = new List<string>();
    public IReadOnlyList<string> ForbiddenInSubject { get; set; } = new List<string>();

    /// <summary>True when the template has an action spec.</summary>
    public bool ActionSupported { get; set; }
    /// <summary>True when the action block is strictly required.</summary>
    public bool ActionRequired { get; set; }
    /// <summary>The backend-provided description of the system action.</summary>
    public string? SystemActionDescription { get; set; }

    /// <summary>
    /// Whether this template may carry <c>{{contactInformationBlock}}</c> AT ALL.
    ///
    /// <para>
    /// Not the same question as "does it show one today", which is the contact POLICY and moves between
    /// NONE, OPTIONAL and REQUIRED under the operator's hand. This one is fixed by what the message is:
    /// false on mail whose content is a one-time credential, and on mail addressed to the very person the
    /// block would name. The screen shows the reason instead of the settings form when it is false.
    /// </para>
    /// </summary>
    public bool ContactSupported { get; set; }

    /// <summary>True when the EFFECTIVE policy is REQUIRED, so the body may not drop the block.</summary>
    public bool ContactRequired { get; set; }

    /// <summary>
    /// The effective display level in full — NONE / OPTIONAL / REQUIRED.
    ///
    /// <para>
    /// <see cref="ContactRequired"/> alone cannot express the rule that matters at the other end of the
    /// range: under OPTIONAL a body may keep the block or drop it, and under NONE it may not keep it. The
    /// editor needs to tell those two apart to disable its save button, and could not while the only
    /// signal was a boolean for the third state.
    /// </para>
    /// <para>
    /// Empty string on a historical row, where no policy resolves and the block is refused by capability.
    /// </para>
    /// </summary>
    public string ContactRequirement { get; set; } = string.Empty;

    /// <summary>False when there is nothing on the contact card an operator could change.</summary>
    public bool ContactSettingsEditable { get; set; }

    /// <summary>
    /// Stable reason for the capability above — matched by clients; the sentences are for people.
    /// </summary>
    public string? ContactReasonCode { get; set; }
    public string? ContactReasonVi { get; set; }
    public string? ContactReasonEn { get; set; }

    /// <summary>True when the message carries a one-time code or a personal action link.</summary>
    public bool CarriesSecret { get; set; }

    public bool AllowCc { get; set; }
    public bool AllowBcc { get; set; }

    public string SecurityClassification { get; set; } = null!;

    /// <summary>The only fields an operator may change. The screen disables everything else.</summary>
    public IReadOnlyList<string> EditableFields { get; set; } = new List<string>();
}
