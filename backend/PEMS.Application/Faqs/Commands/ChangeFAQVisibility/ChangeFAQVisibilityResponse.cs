namespace PEMS.Application.Faqs.Commands.ChangeFAQVisibility;

public sealed class ChangeFAQVisibilityResponse
{
    public ulong FaqId { get; init; }
    public string NewStatus { get; init; } = string.Empty;
    public string NewStatusLabel { get; init; } = string.Empty;
}
