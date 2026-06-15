using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewPolicyAndTerms;

public sealed class ViewPolicyAndTermsQueryHandler : IRequestHandler<ViewPolicyAndTermsQuery, ViewPolicyAndTermsDto>
{
    public Task<ViewPolicyAndTermsDto> Handle(ViewPolicyAndTermsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Policy & Terms has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}