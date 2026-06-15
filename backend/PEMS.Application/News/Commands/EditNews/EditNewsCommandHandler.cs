using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.News.Commands.EditNews;

public sealed class EditNewsCommandHandler : IRequestHandler<EditNewsCommand, EditNewsResponse>
{
    public Task<EditNewsResponse> Handle(EditNewsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Edit News has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}