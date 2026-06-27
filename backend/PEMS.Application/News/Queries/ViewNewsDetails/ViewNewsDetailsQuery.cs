using MediatR;

namespace PEMS.Application.News.Queries.ViewNewsDetails;

public sealed record ViewNewsDetailsQuery(ulong NewsId) : IRequest<ViewNewsDetailsDto>;
