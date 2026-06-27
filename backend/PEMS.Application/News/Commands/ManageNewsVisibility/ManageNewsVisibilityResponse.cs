namespace PEMS.Application.News.Commands.ManageNewsVisibility;

public sealed class ManageNewsVisibilityResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? NewStatus { get; init; }
    public string? NewStatusLabel { get; init; }
}
