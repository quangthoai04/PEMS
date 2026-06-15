using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.EditMeetingMinutes;

public sealed class EditMeetingMinutesCommandHandler : IRequestHandler<EditMeetingMinutesCommand, EditMeetingMinutesResponse>
{
    public Task<EditMeetingMinutesResponse> Handle(EditMeetingMinutesCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Edit Meeting Minutes has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}