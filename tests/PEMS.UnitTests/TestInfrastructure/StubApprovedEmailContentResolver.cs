using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Preview;

namespace PEMS.UnitTests.TestInfrastructure;

/// <summary>
/// An <see cref="IApprovedEmailContentResolver"/> that runs the real sanitisation but skips the token.
///
/// <para>
/// The handler tests are about what a send DOES with approved content — that the edited subject reaches
/// the dispatcher, that the action block is still injected, that attachments are recorded — not about
/// whether the token verifies. Those two questions have different natural homes: the token's own rules
/// (actor, expiry, revision, scope, content hash) belong to <c>ApprovedEmailContentVerifier</c> and are
/// tested against it directly, where a case can be written in three lines instead of by standing up a
/// signed preview inside a handler fixture.
/// </para>
/// <para>
/// What this deliberately keeps is the SANITISER. It is the one part of the pipeline whose absence would
/// let a handler test pass on content the real send would reject, so leaving it out would make these
/// tests agree with each other and disagree with production.
/// </para>
/// </summary>
public sealed class StubApprovedEmailContentResolver : IApprovedEmailContentResolver
{
    private readonly IHtmlSanitizerService _sanitizer;

    /// <summary>Every (templateCode, scopeKey) pair a handler asked about, in call order.</summary>
    public List<(string TemplateCode, string ScopeKey)> Calls { get; } = new();

    public StubApprovedEmailContentResolver(IHtmlSanitizerService sanitizer) => _sanitizer = sanitizer;

    public Task<SystemEmailContent> ResolveAsync(
        ApprovedEmailContent? approved,
        string templateCode,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((templateCode, scopeKey));

        if (approved is null)
            return Task.FromResult<SystemEmailContent>(SystemEmailContent.FromTemplate.Instance);

        var bodyHtml = !string.IsNullOrWhiteSpace(approved.BodyText)
            ? EmailComposition.PlainTextToHtml(approved.BodyText)
            : approved.BodyHtml ?? string.Empty;

        return Task.FromResult<SystemEmailContent>(
            SystemEmailContent.AuthoredByUser.Create(approved.Subject, bodyHtml, _sanitizer));
    }

    public IReadOnlyList<EmailComposeAttachmentInput> AttachmentsOf(ApprovedEmailContent? approved)
        => approved?.Attachments is { Count: > 0 } a
            ? new List<EmailComposeAttachmentInput>(a)
            : Array.Empty<EmailComposeAttachmentInput>();
}
