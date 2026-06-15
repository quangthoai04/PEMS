using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Faqs.Queries.ViewListFAQ;

public sealed class ViewListFAQQueryHandler : IRequestHandler<ViewListFAQQuery, ViewListFAQDto>
{
    public Task<ViewListFAQDto> Handle(ViewListFAQQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View List FAQ has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}