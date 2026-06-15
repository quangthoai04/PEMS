using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.MeetingMinutes.Queries.SearchAndFilterMinutes;

public sealed class SearchAndFilterMinutesQueryHandler : IRequestHandler<SearchAndFilterMinutesQuery, SearchAndFilterMinutesDto>
{
    public Task<SearchAndFilterMinutesDto> Handle(SearchAndFilterMinutesQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search/Filter Minutes has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}