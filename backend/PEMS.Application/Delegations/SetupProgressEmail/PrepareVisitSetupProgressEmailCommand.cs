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
/// recipients, and — when file storage allows — generates the Schedule Report that goes with it.
///
/// <para>
/// The report is a DEFAULT attachment, not a required one. Producing it is the last thing this does and
/// the only part that can fail without failing the operation: a message the Host can read, edit and send
/// is what "prepare" means, and a PDF that could not be archived does not take that away. See
/// <see cref="IVisitSetupProgressComposer"/>.
/// </para>
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

    /// <summary>
    /// The Schedule Report, attached by DEFAULT — null when it could not be produced.
    ///
    /// <para>
    /// Null is a normal answer, not an error the caller must handle as one: the composer opens either way,
    /// with the report already in its attachment list when there is one and with a warning when there is
    /// not. It used to be non-nullable and the report used to be mandatory, which made an expired Google
    /// Drive grant the end of the whole flow — the Host could not compose, let alone send, a message whose
    /// text does not depend on the PDF at all.
    /// </para>
    /// <para>
    /// Absence is expressed as null in both fields rather than as <c>0</c> and <c>""</c>, so a client
    /// cannot accidentally attach file id zero or render a nameless chip.
    /// </para>
    /// </summary>
    public ulong? ReportFileId { get; init; }
    public string? ReportFileName { get; init; }

    /// <summary>Vietnam wall-clock moment the report — and the body's tables — were built from.</summary>
    public string ReportGeneratedAt { get; init; } = string.Empty;

    /// <summary>
    /// The default envelope, derived from the instance rather than from whatever the compose screen had
    /// loaded. The Host may add, remove and move anybody afterwards.
    /// </summary>
    public List<SetupProgressRecipientDto> Recipients { get; init; } = new();

    /// <summary>
    /// Things the Host should read before sending — a missing guest address, a fallback recipient, a
    /// Schedule Report that could not be produced. These are informational: whether the message can be
    /// sent is decided by its TO group, not by this list.
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

        // A missing report joins the envelope's own notices rather than getting a channel of its own:
        // the composer already shows this list above the form, and "the report is not attached" is the
        // same kind of thing as "the guest has no address on file" — something to read before sending,
        // not something that decides whether sending is possible.
        var warnings = envelope.Warnings.ToList();
        if (composed.ReportWarning is { } reportWarning) warnings.Add(reportWarning);

        return new PrepareVisitSetupProgressEmailResponse
        {
            Subject = composed.Subject,
            BodyHtml = composed.BodyHtml,
            LanguageCode = composed.LanguageCode,
            ReportFileId = composed.ReportFileId,
            ReportFileName = composed.ReportFileName,
            ReportGeneratedAt = composed.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            Recipients = Flatten(envelope).ToList(),
            Warnings = warnings,
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
