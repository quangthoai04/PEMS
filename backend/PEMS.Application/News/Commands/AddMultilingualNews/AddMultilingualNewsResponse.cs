namespace PEMS.Application.News.Commands.AddMultilingualNews;

public sealed class AddMultilingualNewsResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public ulong NewsId { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public ulong TranslationId { get; init; }
}
