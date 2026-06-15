using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Queries.ViewEmail;

public sealed class ViewEmailQueryHandler : IRequestHandler<ViewEmailQuery, ViewEmailDto>
{
    public Task<ViewEmailDto> Handle(ViewEmailQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Email has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}