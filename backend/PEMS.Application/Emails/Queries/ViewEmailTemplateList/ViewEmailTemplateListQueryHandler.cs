using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateList;

public sealed class ViewEmailTemplateListQueryHandler : IRequestHandler<ViewEmailTemplateListQuery, ViewEmailTemplateListDto>
{
    public Task<ViewEmailTemplateListDto> Handle(ViewEmailTemplateListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Email Template List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}