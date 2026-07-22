using MediatR;

namespace PEMS.Application.Partners.Commands.TranslatePartnerDraft;

/// <summary>
/// One-shot Vietnamese → English preview translation for the Partner create/edit form's "EN"
/// button. Never persists anything — mirrors <c>TranslateNewsDraftCommand</c>'s "preview only,
/// caller decides what to do with the result" contract. Country/city are proper nouns and are
/// echoed back unchanged rather than sent to the translator (mirrors how the seeded
/// partner_translations rows keep the same country/city text in both languages).
/// </summary>
public sealed record TranslatePartnerDraftCommand : IRequest<TranslatePartnerDraftResponse>
{
    public string SourceLanguage { get; init; } = "vi";
    public string TargetLanguage { get; init; } = "en";
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? Country { get; init; }
    public string? City { get; init; }
    public string? Description { get; init; }
    public string? Address { get; init; }
}

public sealed class TranslatePartnerDraftResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = string.Empty;
    public string TargetLanguage { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? Country { get; init; }
    public string? City { get; init; }
    public string? Description { get; init; }
    public string? Address { get; init; }
}
