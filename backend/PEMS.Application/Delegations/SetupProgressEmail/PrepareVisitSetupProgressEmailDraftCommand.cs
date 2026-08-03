using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// Prepares (or re-opens) the Host's "Gửi cập nhật chuẩn bị" draft for one campus instance: renders the
/// template, resolves the default recipients, generates the Schedule Report and attaches it.
/// </summary>
/// <param name="ReuseExistingDraft">
/// When true (the default) an unsent draft this Host already has for this instance is re-opened instead
/// of a second one being created. Without it, every click of the button would leave another draft and
/// another archived PDF behind — the Host would end up choosing between near-identical drafts, and the
/// delegation's document folder would fill with copies of one report.
/// </param>
public sealed record PrepareVisitSetupProgressEmailDraftCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string? LanguageCode,
    bool ReuseExistingDraft = true) : IRequest<PrepareVisitSetupProgressEmailDraftResponse>;

public sealed class PrepareVisitSetupProgressEmailDraftResponse
{
    public ulong DraftId { get; init; }

    /// <summary>True when an existing unsent draft was re-opened rather than a new one created.</summary>
    public bool ReusedExistingDraft { get; init; }

    /// <summary>vi | en — the language BOTH the template and the attached report were produced in.</summary>
    public string LanguageCode { get; init; } = "vi";

    /// <summary>The mandatory Schedule Report attachment, so the composer can mark it undeletable.</summary>
    public ulong ReportFileId { get; init; }
    public string ReportFileName { get; init; } = string.Empty;

    /// <summary>Vietnam wall-clock moment the attached report was rendered from live data.</summary>
    public string ReportGeneratedAt { get; init; } = string.Empty;

    /// <summary>
    /// The body as generated, which the draft was also saved with.
    ///
    /// <para>
    /// Returned so the composer knows what the UNEDITED body looks like. It compares the draft it loads
    /// against this to tell "the Host has written something" from "this is still the generated text",
    /// and that single distinction is what decides whether re-syncing has to warn about overwriting
    /// edits. Without it the composer would have to either warn every time — training the Host to click
    /// through it — or never, which is the silent overwrite the warning exists to prevent.
    /// </para>
    /// </summary>
    public string BodyHtml { get; init; } = string.Empty;

    /// <summary>
    /// Things the Host should read before sending — a missing guest address, a fallback recipient. These
    /// are informational: whether the draft can be sent is decided by its TO group, not by this list.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}
