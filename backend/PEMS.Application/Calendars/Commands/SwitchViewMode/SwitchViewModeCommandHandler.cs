using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Calendars.Commands.SwitchViewMode;

public sealed class SwitchViewModeCommandHandler : IRequestHandler<SwitchViewModeCommand, SwitchViewModeResponse>
{
    public Task<SwitchViewModeResponse> Handle(SwitchViewModeCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Switch View Mode has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}