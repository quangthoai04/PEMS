using MediatR;

namespace PEMS.Application.Faqs.Commands.TranslateFaqDraft;

/// <summary>
/// One-shot Vietnamese → English preview translation for the FAQ create/edit form's "EN" button.
/// Never persists anything — mirrors <c>TranslateNewsDraftCommand</c>'s "preview only, caller
/// decides what to do with the result" contract, just for a (question, answer) pair instead of a
/// title/summary/sections shape.
/// </summary>
public sealed record TranslateFaqDraftCommand : IRequest<TranslateFaqDraftResponse>
{
    public string SourceLanguage { get; init; } = "vi";
    public string TargetLanguage { get; init; } = "en";
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
}

public sealed class TranslateFaqDraftResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = string.Empty;
    public string TargetLanguage { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
}
