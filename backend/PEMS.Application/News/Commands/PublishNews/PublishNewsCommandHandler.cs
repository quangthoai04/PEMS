using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.News.Commands.PublishNews;

public sealed class PublishNewsCommandHandler : IRequestHandler<PublishNewsCommand, PublishNewsResponse>
{
    public Task<PublishNewsResponse> Handle(PublishNewsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Publish News has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}