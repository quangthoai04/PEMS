using MediatR;

namespace PEMS.Application.Emails.Commands.RestoreEmailTemplate;

/// <summary>
/// Puts one system email template's content back to the wording PEMS ships (G11-I).
///
/// <para>
/// The catalog is fixed, so an operator who has edited a template into an unusable state has no
/// create-a-new-one escape and no delete-and-reseed escape — without this command the only way back was
/// for somebody with database access to re-run a SQL script, which is not a feature an HO operator can
/// use. Content is the only thing restored: the code, module, classification, variable contract and
/// status are not the operator's to change, so they cannot have drifted and are not touched.
/// </para>
/// <para>
/// Deliberately carries no content of its own. A restore that let the caller supply the "default" would
/// be an update wearing a different name.
/// </para>
/// </summary>
public sealed class RestoreEmailTemplateCommand : IRequest<RestoreEmailTemplateResponse>
{
    public ulong EmailTemplateId { get; set; }

    /// <summary>
    /// The <c>revision</c> the screen was showing. Restore is a full content overwrite, so it needs the
    /// same conditional write as an edit: an operator who restores a template a colleague changed while
    /// the confirmation dialog was open would otherwise discard that change without either of them
    /// seeing it happen.
    /// </summary>
    public uint? ExpectedRevision { get; set; }
}
