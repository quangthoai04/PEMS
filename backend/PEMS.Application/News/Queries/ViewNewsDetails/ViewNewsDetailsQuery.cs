using MediatR;

namespace PEMS.Application.News.Queries.ViewNewsDetails;

/// <param name="LanguageCode">Preferred translation; falls back to 'vi' then to any available one.</param>
public sealed record ViewNewsDetailsQuery(ulong NewsId, string? LanguageCode = null) : IRequest<ViewNewsDetailsDto>;
