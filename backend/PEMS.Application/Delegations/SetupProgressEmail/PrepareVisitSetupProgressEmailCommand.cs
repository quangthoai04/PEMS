using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// Opens the Host's "Gửi cập nhật chuẩn bị" composer: renders the message, resolves the default
/// recipients, and generates the Schedule Report that goes with it.
///
/// <para>
/// Nothing is saved. This used to create an <c>email_drafts</c> row (with its recipients and its
/// attachment, in a transaction) and hand back a <c>draftId</c> the browser then fetched — a round trip
/// whose only purpose was to give the composer somewhere to read the message from. The message is now
/// simply returned, and the composer holds it: no draft to reopen, no draft to reconcile, and no
/// "EmailDraft (…) was not found" when the id the browser was holding no longer resolved.
/// </para>
/// <para>
/// The <c>reuseExistingDraft</c> parameter is gone with it. It existed so a second click did not leave a
/// second draft and a second archived PDF; with nothing persisted, a second click simply renders again.
/// </para>
/// </summary>
public sealed record PrepareVisitSetupProgressEmailCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string? LanguageCode) : IRequest<PrepareVisitSetupProgressEmailResponse>;

public sealed class PrepareVisitSetupProgressEmailResponse
{
    public string Subject { get; init; } = string.Empty;

    /// <summary>The body as generated, which is what the composer opens on and may then edit.</summary>
    public string BodyHtml { get; init; } = string.Empty;

    /// <summary>vi | en — the language BOTH the message and the attached report were produced in.</summary>
    public string LanguageCode { get; init; } = "vi";

    /// <summary>The mandatory Schedule Report, so the composer can mark it undeletable.</summary>
    public ulong ReportFileId { get; init; }
    public string ReportFileName { get; init; } = string.Empty;

    /// <summary>Vietnam wall-clock moment the report — and the body's tables — were built from.</summary>
    public string ReportGeneratedAt { get; init; } = string.Empty;

    /// <summary>
    /// The default envelope, derived from the instance rather than from whatever the compose screen had
    /// loaded. The Host may add, remove and move anybody afterwards.
    /// </summary>
    public List<SetupProgressRecipientDto> Recipients { get; init; } = new();

    /// <summary>
    /// Things the Host should read before sending — a missing guest address, a fallback recipient. These
    /// are informational: whether the message can be sent is decided by its TO group, not by this list.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>One default recipient, carrying the group it belongs to.</summary>
public sealed class SetupProgressRecipientDto
{
    public string Email { get; init; } = string.Empty;
    public string? Name { get; init; }
    /// <summary>TO | CC | BCC.</summary>
    public string RecipientType { get; init; } = EmailRecipientTypes.To;
}

public sealed class PrepareVisitSetupProgressEmailCommandHandler
    : IRequestHandler<PrepareVisitSetupProgressEmailCommand, PrepareVisitSetupProgressEmailResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitSetupProgressComposer _composer;
    private readonly IVisitSetupProgressRecipientResolver _recipients;

    public PrepareVisitSetupProgressEmailCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IVisitSetupProgressComposer composer,
        IVisitSetupProgressRecipientResolver recipients)
    {
        _db = db;
        _currentUser = currentUser;
        _composer = composer;
        _recipients = recipients;
    }

    public async Task<PrepareVisitSetupProgressEmailResponse> Handle(
        PrepareVisitSetupProgressEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var instance = await VisitSetupProgressEmailGuard.ResolveHostInstanceAsync(
            _db, request.VisitRequestId, request.VisitInstanceId, userId, cancellationToken);

        var composed = await _composer.ComposeAsync(
            instance, userId, request.LanguageCode, cancellationToken);

        var envelope = await _recipients.ResolveAsync(instance, cancellationToken);

        return new PrepareVisitSetupProgressEmailResponse
        {
            Subject = composed.Subject,
            BodyHtml = composed.BodyHtml,
            LanguageCode = composed.LanguageCode,
            ReportFileId = composed.ReportFileId,
            ReportFileName = composed.ReportFileName,
            ReportGeneratedAt = composed.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            Recipients = Flatten(envelope).ToList(),
            Warnings = envelope.Warnings.ToList(),
        };
    }

    private static IEnumerable<SetupProgressRecipientDto> Flatten(SetupProgressRecipients e)
    {
        foreach (var r in e.To) yield return Map(r, EmailRecipientTypes.To);
        foreach (var r in e.Cc) yield return Map(r, EmailRecipientTypes.Cc);
        foreach (var r in e.Bcc) yield return Map(r, EmailRecipientTypes.Bcc);

        static SetupProgressRecipientDto Map(EmailRecipient r, string type)
            => new() { Email = r.Email, Name = r.DisplayName, RecipientType = type };
    }
}
