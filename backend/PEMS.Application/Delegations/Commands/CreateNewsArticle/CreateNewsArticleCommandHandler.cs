using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.CreateNewsArticle;

public sealed class CreateNewsArticleCommandHandler : IRequestHandler<CreateNewsArticleCommand, CreateNewsArticleResponse>
{
    public Task<CreateNewsArticleResponse> Handle(CreateNewsArticleCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create News Article has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}