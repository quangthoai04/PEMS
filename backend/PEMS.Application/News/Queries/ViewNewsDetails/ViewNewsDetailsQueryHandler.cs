using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.News.Queries.ViewNewsDetails;

public sealed class ViewNewsDetailsQueryHandler : IRequestHandler<ViewNewsDetailsQuery, ViewNewsDetailsDto>
{
    public Task<ViewNewsDetailsDto> Handle(ViewNewsDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View News Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}