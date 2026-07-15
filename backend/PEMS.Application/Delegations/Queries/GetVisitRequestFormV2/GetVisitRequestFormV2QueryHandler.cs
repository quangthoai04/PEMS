using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services.VisitFormRead;

namespace PEMS.Application.Delegations.Queries.GetVisitRequestFormV2;

public sealed class GetVisitRequestFormV2QueryHandler
    : IRequestHandler<GetVisitRequestFormV2Query, ResolvedVisitFormDto>
{
    private readonly IVisitFormReadService _formReadService;
    private readonly PerCampusFormV2Options _options;

    public GetVisitRequestFormV2QueryHandler(
        IVisitFormReadService formReadService,
        PerCampusFormV2Options options)
    {
        _formReadService = formReadService;
        _options = options;
    }

    public async Task<ResolvedVisitFormDto> Handle(
        GetVisitRequestFormV2Query request, CancellationToken cancellationToken)
    {
        // Flag OFF ⇒ the v2 surface does not exist. Keep v1 behaviour untouched in rolling deployment.
        if (!_options.Enabled)
            throw new NotFoundException("Endpoint", request.VisitRequestId);

        return await _formReadService.ResolveAsync(request.VisitRequestId, cancellationToken);
    }
}
