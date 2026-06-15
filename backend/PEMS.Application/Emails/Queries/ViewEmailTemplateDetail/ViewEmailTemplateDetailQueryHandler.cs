using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateDetail;

public sealed class ViewEmailTemplateDetailQueryHandler : IRequestHandler<ViewEmailTemplateDetailQuery, ViewEmailTemplateDetailDto>
{
    public Task<ViewEmailTemplateDetailDto> Handle(ViewEmailTemplateDetailQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Email Template Detail has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}