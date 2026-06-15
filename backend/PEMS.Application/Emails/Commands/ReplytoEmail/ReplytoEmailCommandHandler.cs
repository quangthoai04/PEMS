using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Commands.ReplytoEmail;

public sealed class ReplytoEmailCommandHandler : IRequestHandler<ReplytoEmailCommand, ReplytoEmailResponse>
{
    public Task<ReplytoEmailResponse> Handle(ReplytoEmailCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Reply to Email has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}