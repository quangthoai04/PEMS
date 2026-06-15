using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewContactInfo;

public sealed class ViewContactInfoQueryHandler : IRequestHandler<ViewContactInfoQuery, ViewContactInfoDto>
{
    public Task<ViewContactInfoDto> Handle(ViewContactInfoQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Contact Info has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}