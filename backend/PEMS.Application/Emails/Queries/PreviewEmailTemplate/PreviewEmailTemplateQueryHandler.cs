using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Queries.PreviewEmailTemplate;

/// <summary>
/// Renders a system template for the "Xem trước email" modal through the SAME renderer the send uses,
/// then hands back the editable content with the action block removed and a disabled copy of that block
/// for read-only display. No persistence, no tokens, no SMTP.
///
/// <para>
/// It goes through <see cref="IEmailTemplateRenderer"/> for one reason: a preview that renders by
/// different rules is not a preview. The previous implementation had its own regex renderer with a
/// silent fallback table — a missing variable became the text "Chưa có thông tin" here while the real
/// send refused to go out at all — plus a cross-language fallback the renderer deliberately does not
/// have. An operator could therefore approve a preview that could never be sent, or edit a template and
/// see a preview that no recipient would ever receive.
/// </para>
/// </summary>
public sealed class PreviewEmailTemplateQueryHandler
    : IRequestHandler<PreviewEmailTemplateQuery, PreviewEmailTemplateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly Contact.IEmailContactPolicyStore? _contactPolicies;
    private readonly Contact.IEmailContactResolver? _contacts;

    public PreviewEmailTemplateQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailTemplateRenderer renderer,
        Contact.IEmailContactPolicyStore? contactPolicies = null,
        Contact.IEmailContactResolver? contacts = null)
    {
        _db = db;
        _currentUser = currentUser;
        _renderer = renderer;
        _contactPolicies = contactPolicies;
        _contacts = contacts;
    }

    public async Task<PreviewEmailTemplateResponse> Handle(
        PreviewEmailTemplateQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } actorId)
            throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(request.TemplateCode))
            throw new ValidationException("Thiếu mã template email.");

        var code = request.TemplateCode.Trim();
        var language = EmailLanguages.Normalize(request.Language);
        var spec = EmailActionTemplates.For(code);

        // What a preview shows for the contact block, decided by the policy the SEND would use rather
        // than by capability alone.
        //
        // Three states, three answers. A template that cannot carry the block, and one whose level is
        // NONE, both render nothing — because that is what a recipient would get, and a preview that
        // showed a contact card over a policy of "Không hiển thị" would tell an operator their setting
        // had not taken effect. OPTIONAL and REQUIRED get the stand-in card: a preview has no visit, so
        // there is no Host to resolve and no campus to fall back to, and inventing a plausible name and
        // address would show a person who does not exist and invite the operator to "correct" contact
        // details the template has no control over.
        //
        // Empty is still SUPPLIED rather than omitted, so a body that still carries the placeholder
        // previews as the mail a recipient would see instead of failing closed on an unresolved
        // placeholder — which would report the wrong fault. The RIGHT fault, that the body and the policy
        // disagree, is reported by the content validator on the editing screen, and refused by the save.
        var previewContactRequirement = await Contact.EffectiveContactRequirement
            .ResolveAsync(_contactPolicies, code, cancellationToken);

        var showsContactBlock =
            Contact.EmailContactCapabilities.Supports(code)
            && previewContactRequirement != Domain.Enums.EmailContactRequirement.NONE;

        // …and the OTHER kind of preview, which the paragraph above does not describe.
        //
        // An OPERATIONAL preview belongs to a real message: there IS a visit, so there IS a Host, and the
        // stand-in card is not a cautious choice there but a wrong one. It was also actively harmful,
        // because this body goes into an editor and comes back as authored content — so the disabled card
        // was being SENT, with the real one appended beneath it.
        //
        // The block is therefore resolved for real and returned SEPARATELY (see the Contact field on the
        // response). The placeholder is substituted with empty string so the editable body carries no
        // trace of it: no stand-in to edit, no real card to duplicate, and nothing about the contact that
        // the client could send back.
        var operational = request.IsOperational && _contacts is not null;

        Contact.EmailContactPreviewResult? contactPreview = null;

        if (operational)
        {
            contactPreview = await Contact.EmailContactPreview.BuildAsync(
                _contacts!,
                _contactPolicies,
                new Contact.EmailContactRequest(
                    code, language,
                    request.VisitInstanceId, request.CampusId, request.DepartmentId,
                    // Always the signed-in account: "Sent by" and a SENDER Reply-To must name whoever is
                    // actually about to press send, never a value that travelled in the request body.
                    actorId),
                request.ContactOverride,
                actorId,
                cancellationToken);
        }

        var trustedBlocks = new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ContactInformationBlock] = operational || !showsContactBlock
                ? string.Empty
                : Contact.EmailContactHtmlRenderer.DisabledBlock(language),

            // Supplied unconditionally because a template that does not use the placeholder never
            // substitutes it, while a template that does would otherwise fail the preview closed on an
            // unresolved variable.
            [EmailTrustedBlocks.SetupSummaryBlock] =
                EmailComposition.DisabledSetupSummaryBlock(language),
        };

        string? disabledActionBlock = null;

        if (spec is not null)
        {
            // The buttons a preview shows are dead on purpose: real tokens are minted only by a real send.
            // They are still passed as a trusted block so the body is assembled exactly as it will be, and
            // so the strip below has a well-formed block to remove.
            disabledActionBlock = EmailActionTemplates.DisabledBlockFor(code, language);
            trustedBlocks[EmailTrustedBlocks.ActionBlock] =
                EmailComposition.ActionBlockStart + disabledActionBlock + EmailComposition.ActionBlockEnd;
        }

        // Sample values come from the backend contract — never from a dictionary compiled into a
        // screen, which is how preview and send ended up substituting from two different tables (G11-J).
        // They are layered UNDER the caller's context, so a caller that supplies real data always wins.
        //
        // Only when the caller asked for them. See UseSampleData on the query: filling gaps silently is
        // right for an operator editing wording and wrong for a host about to send a real message.
        var context = request.UseSampleData
            ? new Dictionary<string, string>(
                EmailTemplateContracts.PreviewSample(code, language), StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in request.Context ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;

            // A caller may not supply a trusted block: those are the only route by which markup, and
            // therefore a live action URL, enters a rendered message.
            if (EmailTrustedBlocks.All.Contains(pair.Key)) continue;

            context[pair.Key] = pair.Value;
        }

        var rendered = await _renderer.RenderAsync(
            new EmailRenderRequest(code, language, context, trustedBlocks)
            {
                // Operational preview asserts what the SEND asserts. A stored body that has lost its
                // contact placeholder under a REQUIRED policy, or kept one under NONE, makes the message
                // unsendable — and a preview that rendered happily and then failed on send would tell the
                // host their message was fine right up to the moment it was not. The template-management
                // preview keeps the looser flags: an operator mid-edit is expected to be in that state,
                // and the content validator reports it on the screen where it can be repaired.
                ContactBlockRequired = operational
                    && previewContactRequirement == Domain.Enums.EmailContactRequirement.REQUIRED,
                ContactBlockForbidden = operational
                    && previewContactRequirement == Domain.Enums.EmailContactRequirement.NONE,
            },
            cancellationToken);

        // Whether this template actually HAS an action area is read off the rendered body rather than
        // guessed: the block was substituted only if the body asked for it. A registered template whose
        // stored body has lost the placeholder therefore previews as a plain one — the drift is reported
        // by the content validator, which is where it can be repaired, rather than faked here.
        var usesActionBlock = spec is not null
            && rendered.Body.Contains(EmailComposition.ActionBlockStart, StringComparison.Ordinal);

        if (!usesActionBlock)
        {
            // Plain template: the whole body is editable, no system action block.
            return new PreviewEmailTemplateResponse(
                rendered.TemplateCode, rendered.Subject, rendered.Body,
                EmailComposition.HtmlToPlainText(rendered.Body),
                false, null, null, Array.Empty<string>(), true, rendered.BodyFormat.ToString(),
                contactPreview);
        }

        // Action template: editable content is the body WITHOUT the action artifacts; the block itself
        // is returned separately so the modal can show it as read-only.
        var editableContent = EmailComposition.StripActionArtifacts(rendered.Body);

        return new PreviewEmailTemplateResponse(
            rendered.TemplateCode, rendered.Subject, editableContent,
            EmailComposition.HtmlToPlainText(editableContent),
            true, spec!.SystemActionDescription, disabledActionBlock,
            spec.RequiredActionPlaceholders, true, rendered.BodyFormat.ToString(),
            contactPreview);
    }
}
