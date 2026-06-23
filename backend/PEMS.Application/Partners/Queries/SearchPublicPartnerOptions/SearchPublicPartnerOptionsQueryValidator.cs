using FluentValidation;

namespace PEMS.Application.Partners.Queries.SearchPublicPartnerOptions;

public sealed class SearchPublicPartnerOptionsQueryValidator 
    : AbstractValidator<SearchPublicPartnerOptionsQuery>
{
    public SearchPublicPartnerOptionsQueryValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Keyword));

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50);
    }
}
