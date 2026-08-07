using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// "Đồng bộ dữ liệu mới nhất": rebuilds BOTH halves of the message from one fresh read — the HTML tables
/// in the body and the attached Schedule Report — and moves the snapshot timestamp.
///
/// <para>
/// A composer can sit open while the agenda, the participant list or the timings change underneath it, and
/// the Host needs to send what is true now.
/// </para>
/// <para>
/// Because the body IS rebuilt, this replaces anything the Host typed into it. The composer must therefore
/// ask before calling it — silently discarding someone's writing is the one outcome this must not produce —
/// and the response says <see cref="RefreshVisitSetupProgressEmailResponse.BodyRewritten"/> so a client
/// cannot treat a rebuild as additive. The subject and the recipient list are NOT touched: those are
/// addressing decisions, not a picture of the setup.
/// </para>
/// <para>
/// The request no longer carries a draft id — there is no draft to update. It rebuilds and returns; what
/// the composer does with the result is the composer's business, which is also why the confirmation now
/// lives entirely in the browser where the unsaved edit it is protecting actually is.
/// </para>
/// </summary>
public sealed record RefreshVisitSetupProgressEmailCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string? LanguageCode) : IRequest<RefreshVisitSetupProgressEmailResponse>;

public sealed class RefreshVisitSetupProgressEmailResponse
{
    /// <summary>
    /// The rebuilt Schedule Report, or null when it could not be archived this time.
    ///
    /// <para>
    /// Null here is the case that most needs stating, because the body WAS rebuilt: the composer is now
    /// holding a message describing a newer moment than the report it was carrying, so the previous PDF is
    /// not the latest one and must not be presented as though it were. The client's part is to drop the
    /// report it had and show <see cref="Warnings"/> — see the composer's refresh handler.
    /// </para>
    /// </summary>
    public ulong? ReportFileId { get; init; }
    public string? ReportFileName { get; init; }

    /// <summary>The moment BOTH the body and the PDF were built from — one read, one timestamp.</summary>
    public string ReportGeneratedAt { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = "vi";

    /// <summary>
    /// Why the report is missing, when it is. Empty on a clean refresh.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>The freshly rendered body, which REPLACES what the composer was holding.</summary>
    public string BodyHtml { get; init; } = string.Empty;

    /// <summary>The freshly rendered subject, offered so a language switch is coherent.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Always true — kept explicit so the client cannot treat a rebuild as additive.</summary>
    public bool BodyRewritten { get; init; } = true;
}

public sealed class RefreshVisitSetupProgressEmailCommandHandler
    : IRequestHandler<RefreshVisitSetupProgressEmailCommand, RefreshVisitSetupProgressEmailResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitSetupProgressComposer _composer;

    public RefreshVisitSetupProgressEmailCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IVisitSetupProgressComposer composer)
    {
        _db = db;
        _currentUser = currentUser;
        _composer = composer;
    }

    public async Task<RefreshVisitSetupProgressEmailResponse> Handle(
        RefreshVisitSetupProgressEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        // Re-authorised from the database, like every other call in this flow: a host handover or a
        // cancellation between opening the composer and pressing "đồng bộ" has to take effect here.
        var instance = await VisitSetupProgressEmailGuard.ResolveHostInstanceAsync(
            _db, request.VisitRequestId, request.VisitInstanceId, userId, cancellationToken);

        var composed = await _composer.ComposeAsync(
            instance, userId, request.LanguageCode, cancellationToken);

        return new RefreshVisitSetupProgressEmailResponse
        {
            Subject = composed.Subject,
            BodyHtml = composed.BodyHtml,
            ReportFileId = composed.ReportFileId,
            ReportFileName = composed.ReportFileName,
            ReportGeneratedAt = composed.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            LanguageCode = composed.LanguageCode,
            BodyRewritten = true,
            Warnings = composed.ReportWarning is { } w ? new List<string> { w } : new List<string>(),
        };
    }
}
