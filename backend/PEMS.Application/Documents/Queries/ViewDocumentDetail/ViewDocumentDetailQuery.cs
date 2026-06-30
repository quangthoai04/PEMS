using MediatR;

namespace PEMS.Application.Documents.Queries.ViewDocumentDetail;

public class ViewDocumentDetailQuery : IRequest<ViewDocumentDetailDto>
{
    public ulong DocumentId { get; set; }
}
