using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Feedbacks.Queries.ViewFeedbackSummary;

public sealed class ViewFeedbackSummaryQueryHandler : IRequestHandler<ViewFeedbackSummaryQuery, ViewFeedbackSummaryDto>
{
    public Task<ViewFeedbackSummaryDto> Handle(ViewFeedbackSummaryQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Feedback Summary has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}