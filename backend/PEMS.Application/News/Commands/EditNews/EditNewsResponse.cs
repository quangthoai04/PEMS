namespace PEMS.Application.News.Commands.EditNews;

public sealed class EditNewsResponse
{
    public bool    Success        { get; init; }
    public string? Message        { get; init; }
    public string? NewStatus      { get; init; }
    public string? NewStatusLabel { get; init; }
    public int     NewRowVersion  { get; init; }
}
