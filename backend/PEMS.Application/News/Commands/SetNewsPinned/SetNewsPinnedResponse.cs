namespace PEMS.Application.News.Commands.SetNewsPinned;

public sealed record SetNewsPinnedResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsPinned { get; init; }
    public int RowVersion { get; init; }
}
