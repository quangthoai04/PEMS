using FluentValidation;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

/// <summary>
/// Same convention as <c>ViewFaqQueryValidator</c>'s faqType rule: blank is valid (the handler applies
/// the default), an unrecognised value is a validation error rather than a silent no-match. Limit needs
/// no rule here — <see cref="SearchInformationQuery.Limit"/> clamps itself to [1, 20] on init.
/// </summary>
public sealed class SearchInformationQueryValidator : AbstractValidator<SearchInformationQuery>
{
    /// <summary>Long enough for any real phrase; a cap keeps a megabyte "keyword" out of the LIKE pattern.</summary>
    public const int KeywordMaxLength = 200;

    public SearchInformationQueryValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(KeywordMaxLength)
            .WithMessage($"Keyword must be at most {KeywordMaxLength} characters.");

        RuleFor(x => x.LanguageCode)
            .Must(SearchLanguages.IsSupported)
            .WithMessage("Language code is invalid. Supported values: vi, en.");
    }
}
