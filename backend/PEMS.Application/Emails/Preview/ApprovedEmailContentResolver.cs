using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Utils;

namespace PEMS.Application.Emails.Preview;

/// <summary>
/// The one thing a send command calls to turn "the sender approved this" into content it may dispatch.
///
/// <para>
/// It exists so that a command needs to know only two things it already knows — which template it is
/// sending and what the message is about — and gets the template revision lookup, the token verification,
/// the actor check, the scope check, the staleness check, the image normalisation, the sanitisation and
/// the hash comparison without restating any of them. Four commands offer a runtime editor today; the
/// fifth inherits every guarantee by calling this rather than by remembering nine steps.
/// </para>
/// </summary>
public sealed class ApprovedEmailContentResolver : IApprovedEmailContentResolver
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IEmailImageLayoutNormalizer _normalizer;
    private readonly IEmailPreviewTokenService _tokens;

    public ApprovedEmailContentResolver(
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

    public async Task<SystemEmailContent> ResolveAsync(
        ApprovedEmailContent? approved,
        string templateCode,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        // The unedited path costs no query. Most sends take it — a reminder, an automatic notice, a host
        // who read the preview and pressed send — and making them all read email_templates twice to
        // support the minority that edited would be a cost paid by everybody for nobody.
        if (approved is null) return SystemEmailContent.FromTemplate.Instance;

        var revision = await _db.EmailTemplates
            .AsNoTracking()
            .Where(t => t.TemplateCode == templateCode)
            .Select(t => (uint?)t.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                $"Không tìm thấy template email '{templateCode}'.", EmailErrorCodes.TemplateNotFound);

        return await ApprovedEmailContentVerifier.ResolveAsync(
            approved,
            _tokens,
            _sanitizer,
            _normalizer,
            _currentUser.UserId,
            templateCode,
            revision,
            scopeKey,
            cancellationToken);
    }

    public IReadOnlyList<EmailComposeAttachmentInput> AttachmentsOf(ApprovedEmailContent? approved)
        => approved?.Attachments is { Count: > 0 } a
            ? a.ToList()
            : System.Array.Empty<EmailComposeAttachmentInput>();
}

/// <summary>See <see cref="ApprovedEmailContentResolver"/>.</summary>
public interface IApprovedEmailContentResolver
{
    /// <param name="scopeKey">
    /// Built by the CALLER from its own arguments with <see cref="EmailPreviewFingerprint.Scope"/> — never
    /// echoed back from the request. That is what stops an approved message being replayed at a different
    /// recipient.
    /// </param>
    Task<SystemEmailContent> ResolveAsync(
        ApprovedEmailContent? approved,
        string templateCode,
        string scopeKey,
        CancellationToken cancellationToken = default);

    /// <summary>The attachments the sender approved, or an empty list.</summary>
    IReadOnlyList<EmailComposeAttachmentInput> AttachmentsOf(ApprovedEmailContent? approved);
}
