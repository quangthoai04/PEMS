using MediatR;

namespace PEMS.Application.Faqs.Commands.ChangeFAQVisibility;

public class ChangeFAQVisibilityCommand : IRequest<ChangeFAQVisibilityResponse>
{
    public ulong FaqId { get; init; }
}