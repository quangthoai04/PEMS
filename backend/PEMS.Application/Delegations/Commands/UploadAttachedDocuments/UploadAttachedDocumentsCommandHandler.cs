using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.UploadAttachedDocuments;

public sealed class UploadAttachedDocumentsCommandHandler : IRequestHandler<UploadAttachedDocumentsCommand, UploadAttachedDocumentsResponse>
{
    public Task<UploadAttachedDocumentsResponse> Handle(UploadAttachedDocumentsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Upload Attached Documents has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}