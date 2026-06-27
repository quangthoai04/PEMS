using MediatR;

namespace PEMS.Application.Emails.Queries.GetUnprocessedEmailCount;

public class GetUnprocessedEmailCountResponse
{
    public int Count { get; set; }
}

public class GetUnprocessedEmailCountQuery : IRequest<GetUnprocessedEmailCountResponse>
{
}
