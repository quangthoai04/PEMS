namespace PEMS.Application.News.Commands.ApproveNews;

public sealed class ApproveNewsResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? NewStatus { get; init; }
    public string? NewStatusLabel { get; init; }
}
