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

    /// <summary>True when the body must keep <c>{{actionBlock}}</c>.</summary>
    public bool RequiresActionBlock { get; set; }

    /// <summary>True when the message carries a one-time code or a personal action link.</summary>
    public bool CarriesSecret { get; set; }

    public bool AllowCc { get; set; }
    public bool AllowBcc { get; set; }

    public string SecurityClassification { get; set; } = null!;

    /// <summary>The only fields an operator may change. The screen disables everything else.</summary>
    public IReadOnlyList<string> EditableFields { get; set; } = new List<string>();
}
