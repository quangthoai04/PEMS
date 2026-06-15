using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Faqs.Commands.ChangeFAQVisibility;

public sealed class ChangeFAQVisibilityCommandHandler : IRequestHandler<ChangeFAQVisibilityCommand, ChangeFAQVisibilityResponse>
{
    public Task<ChangeFAQVisibilityResponse> Handle(ChangeFAQVisibilityCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Change FAQ Visibility has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}