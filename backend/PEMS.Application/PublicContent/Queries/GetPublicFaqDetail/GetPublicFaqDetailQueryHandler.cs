using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.PublicContent.Queries.SearchInformation;
using PEMS.Application.PublicContent.Queries.ViewFAQ;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.GetPublicFaqDetail;

/// <summary>
/// Resolves one public FAQ under exactly the rules the search that linked to it used: PUBLISHED only,
/// and content in the requested language only — EN comes from <c>faq_translations</c> with no Vietnamese
/// fallback, VI falls back to the legacy Vietnamese columns on <c>faqs</c>. Hidden, missing, or
/// untranslated-into-EN all produce the same 404, so the deep link can never open an accordion holding
/// content the visitor should not be reading.
/// </summary>
public sealed class GetPublicFaqDetailQueryHandler : IRequestHandler<GetPublicFaqDetailQuery, ViewFaqDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicFaqDetailQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ViewFaqDto> Handle(GetPublicFaqDetailQuery request, CancellationToken cancellationToken)
    {
        var lang = SearchLanguages.Normalize(request.LanguageCode);
        var isEnglish = lang == SearchLanguages.English;

        var row = await _db.Faqs
            .AsNoTracking()
            .Where(f => f.FaqId == request.FaqId && f.Status == FaqConstants.Status.Published)
            .Select(f => new
            {
                f.FaqId,
                f.FaqType,
                f.DisplayOrder,
                f.CreatedAt,
                Question = _db.FaqTranslations
                               .Where(t => t.FaqId == f.FaqId && t.LanguageCode == lang)
                               .Select(t => (string?)t.Question).FirstOrDefault()
                           ?? (isEnglish ? null : f.Question),
                Answer = _db.FaqTranslations
                             .Where(t => t.FaqId == f.FaqId && t.LanguageCode == lang)
                             .Select(t => (string?)t.Answer).FirstOrDefault()
                         ?? (isEnglish ? null : f.Answer),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Not published / not there, or there but with nothing to show in this language — same answer.
        if (row is null || string.IsNullOrWhiteSpace(row.Question))
        {
            throw new NotFoundException("PublicFaq", request.FaqId);
        }

        return new ViewFaqDto
        {
            FaqId = row.FaqId,
            FaqType = row.FaqType,
            FaqTypeLabel = FaqConstants.ToTypeLabel(row.FaqType, lang),
            Question = row.Question,
            Answer = row.Answer ?? string.Empty,
            DisplayOrder = row.DisplayOrder,
            CreatedAt = row.CreatedAt,
        };
    }
}
