using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Commands.AddMultilingualNews;
using PEMS.Application.News.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.TranslateNews;

public sealed class TranslateNewsCommandHandler
    : IRequestHandler<TranslateNewsCommand, TranslateNewsResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly INewsTranslationService _translator;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IMediator _mediator;

    public TranslateNewsCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        INewsTranslationService translator,
        IHtmlSanitizerService sanitizer,
        IMediator mediator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _translator = translator;
        _sanitizer = sanitizer;
        _mediator = mediator;
    }

    public async Task<TranslateNewsResponse> Handle(
        TranslateNewsCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;
        var campusId = _currentUser.PrimaryCampusId;

        var news = await _dbContext.News
            .AsNoTracking()
            .Where(n => n.NewsId == request.NewsId)
            .Select(n => new { n.NewsId, n.CampusId, n.AuthorUserId, n.Status })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tin tức", request.NewsId);

        // Same authorization rule as AddMultilingualNews (which re-checks on save).
        var isLeaderOfCampus = roleCode == RoleCodes.Staff
                            && subRole == UserSubRoles.Leader
                            && news.CampusId == campusId;
        var isAuthor = ((roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
                        || roleCode == RoleCodes.Student)
                       && news.AuthorUserId == currentUserId;
        var authorMayTranslate = isAuthor
            && (news.Status == NewsConstants.Status.PendingReview
             || news.Status == NewsConstants.Status.Rejected);

        if (!isLeaderOfCampus && !authorMayTranslate)
            throw new ForbiddenException("Bạn không có quyền dịch bài viết này.");

        var sourceLang = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "vi" : request.SourceLanguage.Trim();
        var targetLang = request.TargetLanguage.Trim();

        if (string.IsNullOrEmpty(targetLang))
            throw new ValidationException("Vui lòng chọn ngôn ngữ đích.");
        if (!NewsConstants.Languages.Allowed.Contains(targetLang))
            throw new ValidationException(
                $"Ngôn ngữ '{targetLang}' không được hỗ trợ. Các ngôn ngữ hợp lệ: {string.Join(", ", NewsConstants.Languages.Allowed)}.");
        if (string.Equals(sourceLang, targetLang, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Ngôn ngữ nguồn và ngôn ngữ đích không được trùng nhau.");

        var targetExists = await _dbContext.NewsTranslations
            .AsNoTracking()
            .AnyAsync(t => t.NewsId == request.NewsId && t.LanguageCode == targetLang, cancellationToken);
        if (targetExists)
            throw new ConflictException($"Bài viết đã có bản dịch '{targetLang}'.");

        // Load the source translation + sections
        var source = await _dbContext.NewsTranslations
            .AsNoTracking()
            .Where(t => t.NewsId == request.NewsId && t.LanguageCode == sourceLang)
            .Select(t => new { t.NewsTranslationId, t.Title, t.Summary })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Bản dịch nguồn", request.NewsId);

        var sourceSections = await _dbContext.NewsContentSections
            .AsNoTracking()
            .Where(s => s.NewsTranslationId == source.NewsTranslationId)
            .OrderBy(s => s.SectionOrder)
            .Select(s => new { s.SectionOrder, s.SectionTitle, s.SectionBodyHtml })
            .ToListAsync(cancellationToken);

        if (sourceSections.Count == 0)
            throw new ConflictException("Bản dịch nguồn không có nội dung để dịch.");

        // Plain-text batch: [title, summary, section titles…]; HTML batch: section bodies.
        var plainInputs = new List<string> { source.Title, source.Summary ?? string.Empty };
        plainInputs.AddRange(sourceSections.Select(s => s.SectionTitle));

        var htmlInputs = sourceSections.Select(s => s.SectionBodyHtml).ToList();

        var plainResults = await _translator.TranslateTextAsync(plainInputs, sourceLang, targetLang, cancellationToken);
        var htmlResults  = await _translator.TranslateHtmlAsync(htmlInputs, sourceLang, targetLang, cancellationToken);

        var translatedTitle   = _sanitizer.Sanitize(plainResults[0]).Trim();
        var translatedSummary = _sanitizer.Sanitize(plainResults[1]).Trim();

        var translatedSections = new List<TranslatedNewsSectionDto>(sourceSections.Count);
        for (var i = 0; i < sourceSections.Count; i++)
        {
            translatedSections.Add(new TranslatedNewsSectionDto
            {
                SectionOrder    = sourceSections[i].SectionOrder,
                SectionTitle    = _sanitizer.Sanitize(plainResults[2 + i]).Trim(),
                SectionBodyHtml = _sanitizer.Sanitize(htmlResults[i])
            });
        }

        if (string.IsNullOrWhiteSpace(translatedTitle))
            throw new BusinessRuleException(
                "Kết quả dịch tiêu đề rỗng — vui lòng thử lại.", "TRANSLATION_EMPTY_RESULT");

        ulong? translationId = null;
        if (request.Save)
        {
            var saveResult = await _mediator.Send(new AddMultilingualNewsCommand
            {
                NewsId       = request.NewsId,
                LanguageCode = targetLang,
                Title        = translatedTitle,
                Summary      = translatedSummary,
                Sections     = translatedSections.Select(s => new AddMultilingualNewsSectionDto
                {
                    SectionOrder    = s.SectionOrder,
                    SectionTitle    = s.SectionTitle,
                    SectionBodyHtml = s.SectionBodyHtml
                }).ToList(),
                CopySectionFilesFromLanguage = sourceLang
            }, cancellationToken);

            translationId = saveResult.TranslationId;
        }

        return new TranslateNewsResponse
        {
            Success        = true,
            Message        = request.Save
                ? $"Đã dịch và lưu bản dịch '{targetLang}'."
                : $"Đã dịch sang '{targetLang}'. Xem lại nội dung trước khi lưu.",
            NewsId         = request.NewsId,
            SourceLanguage = sourceLang,
            LanguageCode   = targetLang,
            Title          = translatedTitle,
            Summary        = translatedSummary,
            Sections       = translatedSections,
            Saved          = request.Save,
            TranslationId  = translationId
        };
    }
}
