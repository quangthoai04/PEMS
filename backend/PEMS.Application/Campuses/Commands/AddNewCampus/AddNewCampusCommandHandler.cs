using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Campuses.Commands.AddNewCampus;

public sealed class AddNewCampusCommandHandler : IRequestHandler<AddNewCampusCommand, AddNewCampusResponse>
{
    public Task<AddNewCampusResponse> Handle(AddNewCampusCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Add New Campus has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}