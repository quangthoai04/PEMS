using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Feedbacks.Queries.SearchAndFilterFeedback;

public sealed class SearchAndFilterFeedbackQueryHandler : IRequestHandler<SearchAndFilterFeedbackQuery, SearchAndFilterFeedbackDto>
{
    public Task<SearchAndFilterFeedbackDto> Handle(SearchAndFilterFeedbackQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search/Filter Feedback has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}