using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.TranslateNewsDraft;

public sealed class TranslateNewsDraftCommandHandler
    : IRequestHandler<TranslateNewsDraftCommand, TranslateNewsDraftResponse>
{
    private readonly ICurrentUserService _currentUser;
    private readonly INewsTranslationService _translator;
    private readonly IHtmlSanitizerService _sanitizer;

    public TranslateNewsDraftCommandHandler(
        ICurrentUserService currentUser,
        INewsTranslationService translator,
        IHtmlSanitizerService sanitizer)
    {
        _currentUser = currentUser;
        _translator = translator;
        _sanitizer = sanitizer;
    }

    public async Task<TranslateNewsDraftResponse> Handle(
        TranslateNewsDraftCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
            throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;

        // Same authoring roles as CreateNewsCommand (incl. Staff Leader self-hosting a delegation)
        // may compose news, so the same set may ask for a draft translation while composing one.
        var isAllowed = roleCode == RoleCodes.Staff || roleCode == RoleCodes.Student;
        if (!isAllowed)
            throw new ForbiddenException("Chỉ Staff và Student mới được dịch tin tức.");

        var sourceLang = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "vi" : request.SourceLanguage.Trim();
        var targetLang = request.TargetLanguage.Trim();

        var allSections = request.Sections.OrderBy(s => s.SectionOrder).ToList();

        // Sections still empty (title AND body) while the user is composing are skipped —
        // nothing to translate yet, and Google Translate rejects empty strings anyway. They are
        // echoed back untranslated so the frontend can still map results back by sectionOrder.
        var translatableSections = allSections
            .Where(s => !string.IsNullOrWhiteSpace(s.SectionTitle) || !string.IsNullOrWhiteSpace(s.SectionBodyHtml))
            .ToList();

        // Plain-text batch: [title, summary, section titles…]; HTML batch: section bodies.
        // Same batching shape as TranslateNewsCommandHandler, on purpose, so both call sites
        // exercise the translation provider identically.
        var plainInputs = new List<string> { request.Title, request.Summary ?? string.Empty };
        plainInputs.AddRange(translatableSections.Select(s => string.IsNullOrWhiteSpace(s.SectionTitle) ? " " : s.SectionTitle));

        var htmlInputs = translatableSections
            .Select(s => string.IsNullOrWhiteSpace(s.SectionBodyHtml) ? " " : s.SectionBodyHtml)
            .ToList();

        var plainResults = plainInputs.Count > 0
            ? await _translator.TranslateTextAsync(plainInputs, sourceLang, targetLang, cancellationToken)
            : Array.Empty<string>();
        var htmlResults = htmlInputs.Count > 0
            ? await _translator.TranslateHtmlAsync(htmlInputs, sourceLang, targetLang, cancellationToken)
            : Array.Empty<string>();

        var translatedTitle   = _sanitizer.Sanitize(plainResults[0]).Trim();
        var translatedSummary = _sanitizer.Sanitize(plainResults[1]).Trim();

        var translatedByOrder = new Dictionary<int, TranslatedNewsDraftSectionDto>();
        for (var i = 0; i < translatableSections.Count; i++)
        {
            translatedByOrder[translatableSections[i].SectionOrder] = new TranslatedNewsDraftSectionDto
            {
                SectionOrder    = translatableSections[i].SectionOrder,
                SectionTitle    = _sanitizer.Sanitize(plainResults[2 + i]).Trim(),
                SectionBodyHtml = _sanitizer.Sanitize(htmlResults[i])
            };
        }

        var translatedSections = allSections
            .Select(s => translatedByOrder.TryGetValue(s.SectionOrder, out var t)
                ? t
                : new TranslatedNewsDraftSectionDto { SectionOrder = s.SectionOrder, SectionTitle = string.Empty, SectionBodyHtml = string.Empty })
            .ToList();

        if (string.IsNullOrWhiteSpace(translatedTitle))
            throw new BusinessRuleException(
                "Kết quả dịch tiêu đề rỗng — vui lòng thử lại.", "TRANSLATION_EMPTY_RESULT");

        return new TranslateNewsDraftResponse
        {
            Success        = true,
            Message        = $"Đã dịch sang '{targetLang}'. Xem lại nội dung trước khi lưu.",
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Title          = translatedTitle,
            Summary        = translatedSummary,
            Sections       = translatedSections
        };
    }
}
