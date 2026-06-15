using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Profiles.Queries.ViewProfile;

public sealed class ViewProfileQueryHandler : IRequestHandler<ViewProfileQuery, ViewProfileDto>
{
    public Task<ViewProfileDto> Handle(ViewProfileQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Profile has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}