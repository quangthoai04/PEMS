using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Saves the template-level contact settings.
///
/// <para>
/// Everything here is an enum or a boolean except the two headings, and that is the security design
/// rather than a simplification: an operator chooses WHICH fields the block shows, never what it
/// contains. There is no field for an address, a telephone number or a user id, so a template cannot be
/// made to present a hand-typed mailbox as the Host's — the values are read from <c>users</c>,
/// <c>campuses</c> and <c>departments</c> when the mail is sent. The headings are text, so they are
/// length-capped and stripped of markup on the way in.
/// </para>
/// </summary>
public sealed class UpdateEmailContactSettingsCommand : IRequest<EmailContactSettingsDto>
{
    public string TemplateCode { get; set; } = string.Empty;

    public string Requirement { get; set; } = nameof(EmailContactRequirement.OPTIONAL);
    public string ContactSource { get; set; } = nameof(EmailContactSource.SUPPORT_CONTACT);

    public bool ShowEmail { get; set; } = true;
    public bool ShowPhone { get; set; } = true;
    public bool ShowDepartment { get; set; }
    public bool ShowCampus { get; set; }
    public bool ShowSender { get; set; }

    public string? HeadingVi { get; set; }
    public string? HeadingEn { get; set; }

    public string ReplyToSource { get; set; } = nameof(EmailReplyToSource.NONE);
}

public sealed class UpdateEmailContactSettingsCommandHandler
    : IRequestHandler<UpdateEmailContactSettingsCommand, EmailContactSettingsDto>
{
    /// <summary>Long enough for a sentence-length heading, short enough not to become a paragraph.</summary>
    private const int MaxHeadingLength = 150;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public UpdateEmailContactSettingsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IMediator mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<EmailContactSettingsDto> Handle(
        UpdateEmailContactSettingsCommand request, CancellationToken cancellationToken)
    {
        var code = request.TemplateCode?.Trim() ?? string.Empty;

        _ = SystemEmailTemplates.Find(code)
            ?? throw new NotFoundException(
                $"Mã template email '{code}' không nằm trong danh mục hệ thống.",
                EmailErrorCodes.TemplateNotFound);

        var requirement = ParseEnum<EmailContactRequirement>(request.Requirement, "mức bắt buộc");
        var source = ParseEnum<EmailContactSource>(request.ContactSource, "nguồn đầu mối");
        var replyTo = ParseEnum<EmailReplyToSource>(request.ReplyToSource, "nguồn Reply-To");

        // ── Capability, before any value is looked at ────────────────────────
        // Fail-closed and FIRST: a template that cannot carry the block has no combination of the fields
        // below that would make this request meaningful. Accepting it — which is what happened before —
        // wrote a policy the renderer ignores and the content validator contradicts, and the operator
        // discovered the contradiction only when the block they had just been invited to add was refused.
        var capability = EmailContactCapabilities.For(code);

        if (!capability.Supported)
            throw new BusinessRuleException(
                $"Mẫu '{code}' không dùng khối thông tin liên hệ nên không có cấu hình liên hệ để lưu. "
                + capability.ReasonVi,
                EmailErrorCodes.ContactNotSupportedForTemplate);

        if (capability.BlockMandated && requirement == EmailContactRequirement.NONE)
            throw new BusinessRuleException(
                $"Không đặt được mức Không hiển thị cho '{code}': {capability.ReasonVi} "
                + "Hãy chọn Tùy chọn hoặc Bắt buộc.",
                EmailErrorCodes.ContactConfigurationInvalid);

        // The same two rules the resolver enforces, applied at SAVE time so the operator is told which
        // combination is wrong while they are looking at it — rather than discovering it when a send is
        // refused days later.
        if (requirement != EmailContactRequirement.NONE && !request.ShowEmail && !request.ShowPhone)
            throw new BusinessRuleException(
                "Cấu hình không hợp lệ: mức bắt buộc khác NONE nhưng đã tắt cả email lẫn số điện thoại, "
                + "nên khối liên hệ sẽ không có cách liên hệ nào.",
                EmailErrorCodes.ContactConfigurationInvalid);

        if (replyTo == EmailReplyToSource.CONTACT && !request.ShowEmail)
            throw new BusinessRuleException(
                "Cấu hình không hợp lệ: Reply-To trỏ về đầu mối nhưng email của đầu mối bị ẩn, "
                + "nên người nhận không thấy được nơi thư trả lời sẽ đến.",
                EmailErrorCodes.ContactConfigurationInvalid);

        // A REQUIRED policy needs the placeholder in the body, so refuse the SETTINGS change that would
        // make an already-saved body unsendable. The other direction of the same contract the content
        // validator enforces — between them, the two cannot be left disagreeing.
        if (requirement == EmailContactRequirement.REQUIRED)
        {
            var marker = "{{" + EmailTrustedBlocks.ContactInformationBlock + "}}";
            var bodies = await _db.EmailTemplates
                .AsNoTracking()
                .Where(t => t.TemplateCode == code)
                .Select(t => new { t.BodyVi, t.BodyEn })
                .FirstOrDefaultAsync(cancellationToken);

            var viMissing = !(bodies?.BodyVi?.Contains(marker, StringComparison.Ordinal) ?? false);
            var enMissing = !string.IsNullOrWhiteSpace(bodies?.BodyEn)
                            && !bodies!.BodyEn!.Contains(marker, StringComparison.Ordinal);

            if (viMissing || enMissing)
                throw new BusinessRuleException(
                    $"Không đặt được mức BẮT BUỘC cho '{code}': nội dung email "
                    + $"({(viMissing ? "tiếng Việt" : "")}{(viMissing && enMissing ? " và " : "")}"
                    + $"{(enMissing ? "tiếng Anh" : "")}) chưa có {marker}. "
                    + "Hãy thêm khối vào nội dung trước, rồi mới đặt mức bắt buộc.",
                    EmailErrorCodes.TemplateRequiredContactBlockNotInBody);
        }

        var now = VietnamTime.Now();
        var actor = _currentUser.UserId;

        var row = await _db.EmailContactPolicies
            .FirstOrDefaultAsync(
                p => p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == code, cancellationToken);

        if (row is null)
        {
            row = new EmailContactPolicy
            {
                ScopeType = EmailContactScopeType.TEMPLATE,
                ScopeKey = code,
                CreatedAt = now,
                CreatedBy = actor,
            };
            _db.EmailContactPolicies.Add(row);
        }

        row.Requirement = requirement;
        row.ContactSource = source;
        row.ShowEmail = request.ShowEmail;
        row.ShowPhone = request.ShowPhone;
        row.ShowDepartment = request.ShowDepartment;
        row.ShowCampus = request.ShowCampus;
        row.ShowSender = request.ShowSender;
        row.HeadingVi = CleanHeading(request.HeadingVi);
        row.HeadingEn = CleanHeading(request.HeadingEn);
        row.ReplyToSource = replyTo;
        row.UpdatedAt = now;
        row.UpdatedBy = actor;

        await _db.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(
            new GetEmailContactSettingsQuery { TemplateCode = code }, cancellationToken);
    }

    private static T ParseEnum<T>(string? value, string label) where T : struct, Enum
        => Enum.TryParse<T>(value?.Trim(), ignoreCase: false, out var parsed)
            ? parsed
            : throw new BusinessRuleException(
                $"Giá trị '{value}' không hợp lệ cho {label}. Hợp lệ: {string.Join(", ", Enum.GetNames<T>())}.",
                EmailErrorCodes.ContactConfigurationInvalid);

    /// <summary>
    /// A heading is TEXT. Any markup is stripped rather than escaped, because the renderer encodes it
    /// again anyway and an operator who pastes a tag should see it disappear at save time instead of
    /// meeting "&amp;lt;b&amp;gt;" in a preview. Empty means "use the default", not "no heading".
    /// </summary>
    private static string? CleanHeading(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = System.Text.RegularExpressions.Regex
            .Replace(value, "<[^>]*>", string.Empty)
            .Replace("\r", " ").Replace("\n", " ")
            .Trim();

        if (text.Length > MaxHeadingLength) text = text[..MaxHeadingLength].TrimEnd();

        return text.Length == 0 ? null : text;
    }
}
