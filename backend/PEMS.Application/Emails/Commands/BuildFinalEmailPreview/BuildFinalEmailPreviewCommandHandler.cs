using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Preview;
using PEMS.Application.Emails.Utils;

namespace PEMS.Application.Emails.Commands.BuildFinalEmailPreview;

/// <summary>
/// Validates and assembles the sender's edit into the exact message that will go out, then signs it.
///
/// <para>
/// <b>Assembled the same way the send assembles it.</b> The locked action block is appended here by the
/// same code path the renderer uses in authored mode, and the branded shell is applied by the same helper
/// the dispatcher applies. If this method built the preview any other way — a client-side concatenation,
/// a second copy of the shell — the screen would show something no recipient receives, and the sender
/// would be approving a picture rather than a message.
/// </para>
/// <para>
/// The one difference from a real send, and it is the necessary one: the action block shown here is the
/// DISABLED copy, with no live token in it. A preview must not mint a credential, and a sender does not
/// need to see the real URL to know what the button says.
/// </para>
/// </summary>
public sealed class BuildFinalEmailPreviewCommandHandler
    : IRequestHandler<BuildFinalEmailPreviewCommand, BuildFinalEmailPreviewResponse>
{
    /// <summary>
    /// How long the FINAL token stays usable. Shorter than the prepare window: at this point the sender
    /// is looking at the finished message with a Send button in front of them, so the honest interval is
    /// "long enough to read it once more", not "long enough to go and do something else".
    /// </summary>
    private static readonly TimeSpan FinalLifetime = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IEmailImageLayoutNormalizer _normalizer;
    private readonly IEmailPreviewTokenService _tokens;

    public BuildFinalEmailPreviewCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer,
        IEmailImageLayoutNormalizer normalizer,
        IEmailPreviewTokenService tokens)
    {
        _db = db;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
        _normalizer = normalizer;
        _tokens = tokens;
    }

    public async Task<BuildFinalEmailPreviewResponse> Handle(
        BuildFinalEmailPreviewCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } actorId)
            throw new ForbiddenException();

        var prepared = _tokens.Verify(request.PreviewToken)
            ?? throw new ValidationException(
                "Bản xem trước đã hết hạn hoặc không còn hợp lệ. Vui lòng mở lại email.",
                EmailErrorCodes.PreviewTokenInvalid);

        if (prepared.ActorUserId != actorId)
            throw new ValidationException(
                "Bản xem trước này không thuộc về tài khoản đang đăng nhập.",
                EmailErrorCodes.PreviewTokenInvalid);

        var code = prepared.TemplateCode;

        // Capability, not the presence of a token. A read-only template never receives one, so this is
        // unreachable through the UI — which is exactly why it is worth stating: the rule that decides
        // who may edit lives in one place, and an endpoint that trusted "they had a token" would be
        // deciding it in a second.
        if (!Sender.EmailSenderVariableCapabilities.IsRuntimeEditable(code))
            throw new ValidationException(
                $"Mẫu email '{code}' không cho phép chỉnh sửa nội dung khi gửi.",
                EmailErrorCodes.TemplateNotRuntimeEditable);

        var template = await _db.EmailTemplates
            .AsNoTracking()
            .Where(t => t.TemplateCode == code)
            .Select(t => new { t.Revision, t.BodyFormat })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                $"Không tìm thấy template email '{code}'.", EmailErrorCodes.TemplateNotFound);

        // The template moved under the sender while they were editing. Refused here rather than at the
        // send, so they find out while their words are still in front of them and can decide what to keep.
        if (template.Revision != prepared.TemplateRevision)
            throw new ConflictException(
                "Mẫu email vừa được cập nhật. Vui lòng mở lại email để xem trước theo nội dung mới.",
                EmailErrorCodes.PreviewStale);

        // The SAME pipeline the send runs — plain-text-or-HTML, image normalisation, validation,
        // sanitisation — through the same method, so the string hashed here is the string hashed there.
        // Two copies of these four steps is the one way this whole mechanism could pass its own checks
        // and still deliver something else.
        var authored = await ApprovedEmailContentVerifier.PrepareAuthoredAsync(
            new ApprovedEmailContent(
                FinalPreviewToken: string.Empty,
                Subject: request.Subject,
                BodyHtml: request.EditableBodyHtml,
                BodyText: request.EditableBodyText,
                Attachments: request.Attachments),
            _sanitizer,
            _normalizer,
            cancellationToken);

        var contentHash = EmailPreviewFingerprint.OfContent(authored.Subject, authored.BodyHtml);
        var attachmentHash = EmailPreviewFingerprint.OfAttachments(request.Attachments);

        var language = EmailLanguages.Normalize(request.Language);
        var expiry = VietnamTime.Now().Add(FinalLifetime);

        var finalToken = _tokens.Issue(new EmailPreviewTokenPayload(
            EmailPreviewPurposes.Final,
            actorId,
            code,
            template.Revision,
            prepared.ScopeKey,
            contentHash,
            attachmentHash,
            // Carried forward from the prepare token rather than taken from this request. Reply-To is
            // resolved from the sending account, so there is nothing here for a client to choose — and
            // accepting one would let it point replies at an address nobody verified.
            prepared.ReplyToEmail,
            expiry));

        return new BuildFinalEmailPreviewResponse(
            authored.Subject,
            BuildFinalHtml(code, authored.BodyHtml, language, template.BodyFormat),
            finalToken,
            prepared.ReplyToEmail,
            expiry.ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The author's words, the locked action block, the branded shell — in that order, which is the order
    /// the renderer and the dispatcher produce between them for an authored send.
    /// </summary>
    private static string BuildFinalHtml(
        string templateCode, string authoredBodyHtml, string language, Domain.Enums.EmailBodyFormat format)
    {
        var spec = EmailActionTemplates.For(templateCode);

        // Stripped first for the same reason the renderer strips: an author who pasted an action anchor
        // must not end up with two, and the block's position is the system's decision.
        var body = EmailComposition.StripActionArtifacts(authoredBodyHtml);

        if (spec is not null)
        {
            body += EmailComposition.ActionBlockStart
                    + EmailActionTemplates.DisabledBlockFor(templateCode, language)
                    + EmailComposition.ActionBlockEnd;
        }

        return format == Domain.Enums.EmailBodyFormat.HTML
            ? EmailComposition.BrandedShell(body)
            : body;
    }
}
