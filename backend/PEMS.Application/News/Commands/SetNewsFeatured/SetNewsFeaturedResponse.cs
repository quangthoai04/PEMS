namespace PEMS.Application.News.Commands.SetNewsFeatured;

public sealed class SetNewsFeaturedResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsFeatured { get; init; }
    public int RowVersion { get; init; }
}
