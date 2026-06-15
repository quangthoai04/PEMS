using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.News.Commands.ApproveNews;

public sealed class ApproveNewsCommandHandler : IRequestHandler<ApproveNewsCommand, ApproveNewsResponse>
{
    public Task<ApproveNewsResponse> Handle(ApproveNewsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Approve News has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}