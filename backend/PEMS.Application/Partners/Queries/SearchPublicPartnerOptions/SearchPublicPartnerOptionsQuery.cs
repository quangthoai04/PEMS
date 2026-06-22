using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.Partners.Queries.SearchPublicPartnerOptions;

public sealed class SearchPublicPartnerOptionsQuery : IRequest<IReadOnlyList<PublicPartnerOptionDto>>
{
    public string? Keyword { get; init; }
    public int Limit { get; init; } = 20;
}
