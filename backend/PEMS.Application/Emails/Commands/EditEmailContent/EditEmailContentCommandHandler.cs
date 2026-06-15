using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Commands.EditEmailContent;

public sealed class EditEmailContentCommandHandler : IRequestHandler<EditEmailContentCommand, EditEmailContentResponse>
{
    public Task<EditEmailContentResponse> Handle(EditEmailContentCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Edit Email Content has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}