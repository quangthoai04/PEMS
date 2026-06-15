using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewFAQ;

public sealed class ViewFAQQueryHandler : IRequestHandler<ViewFAQQuery, ViewFAQDto>
{
    public Task<ViewFAQDto> Handle(ViewFAQQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View FAQ has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}