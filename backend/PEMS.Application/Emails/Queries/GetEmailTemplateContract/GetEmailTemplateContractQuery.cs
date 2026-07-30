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

    public IReadOnlyList<string> AllowedVariables { get; set; } = new List<string>();
    public IReadOnlyList<string> RequiredVariables { get; set; } = new List<string>();
    public IReadOnlyList<string> OptionalVariables { get; set; } = new List<string>();
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
