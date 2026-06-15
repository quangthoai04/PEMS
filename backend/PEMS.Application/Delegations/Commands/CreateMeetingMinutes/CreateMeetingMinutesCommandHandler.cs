using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.CreateMeetingMinutes;

public sealed class CreateMeetingMinutesCommandHandler : IRequestHandler<CreateMeetingMinutesCommand, CreateMeetingMinutesResponse>
{
    public Task<CreateMeetingMinutesResponse> Handle(CreateMeetingMinutesCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create Meeting Minutes has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}