using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsQueryHandler : IRequestHandler<SearchDocumentsQuery, SearchDocumentsDto>
{
    public Task<SearchDocumentsDto> Handle(SearchDocumentsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search Documents has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}