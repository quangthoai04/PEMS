using MediatR;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetAssignmentsProgressList;

namespace PEMS.Application.DepartmentReceptionTasks.Queries.GetAttentionItems;

public sealed class GetAttentionItemsQuery : IRequest<List<AssignmentsProgressItemDto>>
{
}

public sealed class GetAttentionItemsQueryHandler : IRequestHandler<GetAttentionItemsQuery, List<AssignmentsProgressItemDto>>
{
    private readonly IMediator _mediator;

    public GetAttentionItemsQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<List<AssignmentsProgressItemDto>> Handle(GetAttentionItemsQuery request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAssignmentsProgressListQuery
        {
            OwnerScope = "ME",
            SortDirection = "ASC",
            Page = 1,
            PageSize = 100
        }, cancellationToken);

        return result.Items.Where(x => x.NeedsAttention).ToList();
    }
}
