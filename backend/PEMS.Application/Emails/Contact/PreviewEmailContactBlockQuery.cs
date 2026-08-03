using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Renders <c>{{contactInformationBlock}}</c> as a given policy would produce it, with sample data, so
/// the template screen can show the operator what their toggles do before they save them.
///
/// <para>
/// It takes the DRAFT policy from the screen rather than reading the stored one. That is the requirement:
/// an operator who unticks "Số điện thoại" must see the telephone row leave the preview immediately,
/// and the stored policy still says it is shown until they press save.
/// </para>
/// <para>
/// Rendering happens on the backend, through <see cref="EmailContactHtmlRenderer"/> — the same class the
/// send uses. A preview assembled in the browser would be a second implementation of the block's markup
/// and its visibility rules, and the two would drift; the first symptom would be an operator approving a
/// layout no recipient ever receives.
/// </para>
/// </summary>
public sealed class PreviewEmailContactBlockQuery : IRequest<EmailContactBlockPreviewDto>
{
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>VI or EN — the preview follows the language tab the operator is editing.</summary>
    public string? Language { get; set; }

    public string Requirement { get; set; } = nameof(EmailContactRequirement.OPTIONAL);
    public string ContactSource { get; set; } = nameof(EmailContactSource.SUPPORT_CONTACT);

    public bool ShowEmail { get; set; } = true;
    public bool ShowPhone { get; set; } = true;
    public bool ShowDepartment { get; set; }
    public bool ShowCampus { get; set; }
    public bool ShowSender { get; set; }

    public string? HeadingVi { get; set; }
    public string? HeadingEn { get; set; }
}

/// <param name="Html">
/// The rendered block, or an empty string when this policy renders nothing — NONE, or a policy with both
/// channels hidden. Empty is a valid answer the screen displays as "no block", not a failure.
/// </param>
/// <param name="RendersBlock">Whether anything was produced, so the screen need not test the string.</param>
public sealed record EmailContactBlockPreviewDto(string Html, bool RendersBlock);

public sealed class PreviewEmailContactBlockQueryHandler
    : IRequestHandler<PreviewEmailContactBlockQuery, EmailContactBlockPreviewDto>
{
    public Task<EmailContactBlockPreviewDto> Handle(
        PreviewEmailContactBlockQuery request, CancellationToken cancellationToken)
    {
        var code = request.TemplateCode?.Trim() ?? string.Empty;

        _ = SystemEmailTemplates.Find(code)
            ?? throw new NotFoundException(
                $"Mã template email '{code}' không nằm trong danh mục hệ thống.",
                EmailErrorCodes.TemplateNotFound);

        // A template that cannot carry the block previews as nothing at all — the same answer NONE gives,
        // because that is what a recipient would get. Rendering the sample card here would show an
        // operator a block this template can never send, which is the mismatch between screen and send
        // that the capability split exists to close.
        if (!EmailContactCapabilities.Supports(code))
            return Task.FromResult(new EmailContactBlockPreviewDto(string.Empty, false));

        var language = EmailLanguages.Normalize(request.Language);

        var policy = new EmailContactPolicyResolution(
            Requirement: ParseEnum<EmailContactRequirement>(request.Requirement, "mức bắt buộc"),
            ContactSource: ParseEnum<EmailContactSource>(request.ContactSource, "nguồn đầu mối"),
            ShowEmail: request.ShowEmail,
            ShowPhone: request.ShowPhone,
            ShowDepartment: request.ShowDepartment,
            ShowCampus: request.ShowCampus,
            ShowSender: request.ShowSender,
            HeadingVi: Heading(request.HeadingVi, EmailContactPolicyDefaults.DefaultHeadingVi),
            HeadingEn: Heading(request.HeadingEn, EmailContactPolicyDefaults.DefaultHeadingEn),
            // A preview never sets Reply-To on anything, so the value cannot matter here and is not
            // accepted from the client — one less field that could be made to disagree with the block.
            ReplyToSource: EmailReplyToSource.NONE);

        // Deliberately NOT validated the way the store validates a resolved policy. An operator halfway
        // through a change — both channels momentarily unticked — should see an empty preview telling
        // them so, not a 422 that clears the pane while they are still typing. The refusal belongs at
        // save, where UpdateEmailContactSettingsCommandHandler already applies it.
        var html = EmailContactHtmlRenderer.SampleBlock(policy, language);

        return Task.FromResult(new EmailContactBlockPreviewDto(html, html.Length > 0));
    }

    private static T ParseEnum<T>(string? value, string label) where T : struct, Enum
        => Enum.TryParse<T>(value?.Trim(), ignoreCase: false, out var parsed)
            ? parsed
            : throw new BusinessRuleException(
                $"Giá trị '{value}' không hợp lệ cho {label}.",
                EmailErrorCodes.ContactConfigurationInvalid);

    /// <summary>
    /// Markup is stripped rather than escaped, matching the save path: a heading is text, and an operator
    /// who pastes a tag should watch it vanish in the preview exactly as it will when stored.
    /// </summary>
    private static string Heading(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var text = System.Text.RegularExpressions.Regex
            .Replace(value, "<[^>]*>", string.Empty)
            .Replace("\r", " ").Replace("\n", " ")
            .Trim();

        return text.Length == 0 ? fallback : text;
    }
}
