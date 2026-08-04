using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Everything the compose screen needs to show — and to reason about — the reply contact of ONE message.
///
/// <para>
/// <see cref="LockedContactBlockHtml"/> is the block the send would produce, and it is returned SEPARATELY
/// from the editable body on purpose. Putting it inside the body is what broke preview/send parity in the
/// first place: the modal handed the whole rendered body to a rich-text editor, the host sent it back as
/// authored content, and the dispatcher appended the real block underneath the disabled stand-in the
/// preview had drawn. Keeping it out of the editable area means it cannot be edited, cannot be deleted,
/// and cannot be sent back — the client never returns this field.
/// </para>
/// <para>
/// <see cref="ErrorCode"/> exists so a contact that cannot be resolved is a state of this panel rather
/// than a failed request. The alternative — a 400 from the preview endpoint — would discard the subject,
/// the body and the attachments the host had already written, to report a fault in a part of the screen
/// they can fix in place.
/// </para>
/// </summary>
/// <param name="Supported">False for a template that can never carry the block. No panel, no button.</param>
/// <param name="Requirement">NONE | OPTIONAL | REQUIRED — the RESOLVED level, after the cascade.</param>
/// <param name="Mode">Which of <see cref="EmailContactOverrideModes"/> produced what is shown.</param>
/// <param name="Source">The resolver branch, for the "Nguồn hiện tại" line. Null when nothing rendered.</param>
/// <param name="Hidden">The sender asked for this message to go without the block.</param>
/// <param name="CanOverride">Whether the "Thay đổi thông tin liên hệ" button may be offered at all.</param>
/// <param name="CanHide">Whether "Không hiển thị trong email này" may be offered. OPTIONAL only.</param>
public sealed record EmailContactPreviewResult(
    bool Supported,
    string Requirement,
    string Mode,
    string? Source,
    string? LockedContactBlockHtml,
    string? ContactDisplayName,
    string? ContactEmail,
    string? ContactPhone,
    string? ReplyToDisplay,
    bool Hidden,
    bool CanOverride,
    bool CanHide,
    IReadOnlyList<string> AvailableModes,
    IReadOnlyList<string> AvailableReplyToModes,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    /// <summary>True when the panel is in a state the sender must resolve before sending.</summary>
    public bool HasError => ErrorCode is not null;
}

/// <summary>
/// Builds <see cref="EmailContactPreviewResult"/> from the same resolver the send uses.
///
/// <para>
/// It is a builder rather than a query handler because three callers need it and they arrive by different
/// routes: the full preview (first open), the contact-only refresh (the sender changed their mind), and
/// the tests that assert the two agree. A second implementation for any of them would be a second set of
/// rules about when the button appears.
/// </para>
/// </summary>
public static class EmailContactPreview
{
    /// <summary>
    /// Resolves the contact for a real message and describes it.
    /// </summary>
    /// <param name="resolver">The send's resolver. Not a preview-shaped copy of it.</param>
    /// <param name="policies">
    /// Read for the requirement so the panel can still describe itself when resolution fails — a REQUIRED
    /// template with no resolvable Host must report "required, and here is why it is empty", not "no
    /// contact block".
    /// </param>
    public static async Task<EmailContactPreviewResult> BuildAsync(
        IEmailContactResolver resolver,
        IEmailContactPolicyStore? policies,
        EmailContactRequest request,
        EmailContactOverrideInput? overrideInput,
        ulong? actorUserId,
        CancellationToken cancellationToken)
    {
        var capability = EmailContactCapabilities.For(request.TemplateCode);
        var requirement = await EffectiveContactRequirement.ResolveAsync(
            policies, request.TemplateCode, cancellationToken);

        if (!capability.Supported || requirement == EmailContactRequirement.NONE)
            return Unavailable(requirement);

        try
        {
            var resolution = await resolver.ResolveAsync(
                request, overrideInput, actorUserId, cancellationToken);

            return Describe(resolution, requirement);
        }
        // A refused override, a REQUIRED template with nobody to name, a chosen account outside the
        // sender's reach: all three are answers this panel can show and the sender can act on, so none of
        // them takes the preview down with it. Anything else — a broken template, an unreadable database
        // — is not about the contact and is left to propagate.
        catch (ValidationException ex)
        {
            return Failed(requirement, capability, ex.ErrorCode ?? EmailErrorCodes.ContactOverrideInvalid, ex.Message);
        }
        catch (BusinessRuleException ex) when (
            ex.ErrorCode == EmailErrorCodes.ContactRequiredButNotFound
            || ex.ErrorCode == EmailErrorCodes.ContactOverrideInvalid
            || ex.ErrorCode == EmailErrorCodes.ReplyToInvalid)
        {
            return Failed(requirement, capability, ex.ErrorCode!, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            return Failed(
                requirement, capability, EmailErrorCodes.ContactOverrideUserNotAllowed, ex.Message);
        }
    }

    private static EmailContactPreviewResult Unavailable(EmailContactRequirement requirement)
        => new(
            Supported: false,
            Requirement: requirement.ToString(),
            Mode: EmailContactOverrideModes.TemplateDefault,
            Source: null,
            LockedContactBlockHtml: null,
            ContactDisplayName: null,
            ContactEmail: null,
            ContactPhone: null,
            ReplyToDisplay: null,
            Hidden: false,
            CanOverride: false,
            CanHide: false,
            AvailableModes: Array.Empty<string>(),
            AvailableReplyToModes: Array.Empty<string>());

    private static EmailContactPreviewResult Describe(
        EmailContactResolution resolution, EmailContactRequirement requirement)
    {
        var hasBlock = !string.IsNullOrEmpty(resolution.BlockHtml);

        return new EmailContactPreviewResult(
            Supported: true,
            Requirement: requirement.ToString(),
            Mode: resolution.Mode,
            Source: resolution.Contact?.Source.ToString(),
            LockedContactBlockHtml: hasBlock ? resolution.BlockHtml : null,
            ContactDisplayName: resolution.Contact?.DisplayName,
            // The email shown in the panel is the one the block prints AND the one Reply-To would use.
            // Reporting a different value here — the record's address while the block hides it, say —
            // would tell the sender their reply goes somewhere the recipient cannot see.
            ContactEmail: resolution.Policy.ShowEmail ? resolution.Contact?.Email : null,
            ContactPhone: resolution.Policy.ShowPhone ? resolution.Contact?.Phone : null,
            ReplyToDisplay: resolution.ReplyTo?.Email,
            Hidden: resolution.HiddenForThisEmail,
            CanOverride: true,
            CanHide: requirement == EmailContactRequirement.OPTIONAL,
            AvailableModes: EmailContactOverrideModes.All,
            AvailableReplyToModes: EmailContactReplyToModes.All);
    }

    private static EmailContactPreviewResult Failed(
        EmailContactRequirement requirement,
        EmailContactCapabilityInfo capability,
        string errorCode,
        string message)
        => new(
            Supported: capability.Supported,
            Requirement: requirement.ToString(),
            Mode: EmailContactOverrideModes.TemplateDefault,
            Source: null,
            LockedContactBlockHtml: null,
            ContactDisplayName: null,
            ContactEmail: null,
            ContactPhone: null,
            ReplyToDisplay: null,
            Hidden: false,
            // Still overridable, and deliberately so: for a REQUIRED template with no resolvable Host,
            // naming somebody is the ONLY way the sender can get the message out, and a panel that
            // reported the failure and then withheld the button would be a dead end.
            CanOverride: capability.Supported,
            CanHide: requirement == EmailContactRequirement.OPTIONAL,
            AvailableModes: capability.Supported ? EmailContactOverrideModes.All : Array.Empty<string>(),
            AvailableReplyToModes: capability.Supported
                ? EmailContactReplyToModes.All
                : Array.Empty<string>(),
            ErrorCode: errorCode,
            ErrorMessage: message);
}
