using MediatR;

namespace PEMS.Application.Emails.Commands.MarkEmailCompleted;

public class MarkEmailCompletedResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}

public class MarkEmailCompletedCommand : IRequest<MarkEmailCompletedResponse>
{
    public ulong SentEmailId { get; set; }
}
