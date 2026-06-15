using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Documents.Queries.ViewDocumentList;

public sealed class ViewDocumentListQueryHandler : IRequestHandler<ViewDocumentListQuery, ViewDocumentListDto>
{
    public Task<ViewDocumentListDto> Handle(ViewDocumentListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Document List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}