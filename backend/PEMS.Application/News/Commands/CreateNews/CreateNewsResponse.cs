namespace PEMS.Application.News.Commands.CreateNews;

public sealed class CreateNewsResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public CreateNewsData? Data { get; init; }
    public string? ErrorCode { get; init; }
}

public sealed class CreateNewsData
{
    public ulong NewsId { get; init; }
    public ulong VisitInstanceId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public ulong? ExistingNewsId { get; init; }
    public bool? CanEditExisting { get; init; }
}
