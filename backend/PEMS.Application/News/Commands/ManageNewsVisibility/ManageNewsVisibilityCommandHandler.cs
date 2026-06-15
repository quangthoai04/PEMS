using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.News.Commands.ManageNewsVisibility;

public sealed class ManageNewsVisibilityCommandHandler : IRequestHandler<ManageNewsVisibilityCommand, ManageNewsVisibilityResponse>
{
    public Task<ManageNewsVisibilityResponse> Handle(ManageNewsVisibilityCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Manage News Visibility has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}