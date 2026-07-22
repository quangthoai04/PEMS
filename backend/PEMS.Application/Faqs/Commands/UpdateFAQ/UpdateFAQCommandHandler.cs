using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Faqs;

using PEMS.Application.Common;
namespace PEMS.Application.Faqs.Commands.UpdateFAQ;

public sealed class UpdateFAQCommandHandler : IRequestHandler<UpdateFAQCommand, UpdateFAQResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly INewsTranslationService _translator;

    public UpdateFAQCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer,
        INewsTranslationService translator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
        _translator = translator;
    }

    public async Task<UpdateFAQResponse> Handle(UpdateFAQCommand request, CancellationToken cancellationToken)
    {
        var faq = await _dbContext.Faqs
            .FirstOrDefaultAsync(x => x.FaqId == request.FaqId, cancellationToken);

        if (faq is null)
            throw new NotFoundException($"FAQ with ID {request.FaqId} was not found.");

        var faqType = request.FaqType.Trim();
        var sanitizedQuestion = _sanitizer.Sanitize(request.Question).Trim();
        var sanitizedAnswer = _sanitizer.Sanitize(request.Answer).Trim();

        if (string.IsNullOrWhiteSpace(sanitizedQuestion))
            throw new ValidationException("Question is required.");

        if (string.IsNullOrWhiteSpace(sanitizedAnswer))
            throw new ValidationException("Answer is required.");

        var viChanged = !string.Equals(faq.FaqType, faqType, StringComparison.Ordinal)
            || !string.Equals(faq.Question, sanitizedQuestion, StringComparison.Ordinal)
            || !string.Equals(faq.Answer, sanitizedAnswer, StringComparison.Ordinal);

        if (viChanged)
        {
            var normalizedQuestion = sanitizedQuestion.ToLower();
            var exists = await _dbContext.Faqs
                .AsNoTracking()
                .AnyAsync(x => x.FaqId != request.FaqId && x.Question.ToLower() == normalizedQuestion, cancellationToken);

            if (exists)
                throw new ConflictException(
                    "This question already exists in the system. Please enter a different question.",
                    "DUPLICATE_FAQ_QUESTION");
        }

        var now = VietnamTime.Now();
        var sourceHash = ComputeHash(sanitizedQuestion, sanitizedAnswer);
        var englishChanged = false;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            if (viChanged)
            {
                faq.FaqType = faqType;
                faq.Question = sanitizedQuestion;
                faq.Answer = sanitizedAnswer;
                faq.UpdatedAt = now;
                faq.UpdatedBy = _currentUser.UserId;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var translations = await _dbContext.FaqTranslations
                .Where(t => t.FaqId == request.FaqId)
                .ToListAsync(cancellationToken);

            var viTranslation = translations.FirstOrDefault(t => t.LanguageCode == "vi");
            if (viTranslation is null)
            {
                _dbContext.FaqTranslations.Add(new FaqTranslation
                {
                    FaqId = faq.FaqId,
                    LanguageCode = "vi",
                    Question = sanitizedQuestion,
                    Answer = sanitizedAnswer,
                    TranslationSource = "LEGACY",
                    TranslationStatus = "READY",
                    SourceHash = sourceHash,
                    TranslatedAt = now,
                    CreatedAt = now,
                    CreatedBy = _currentUser.UserId,
                });
            }
            else if (viChanged)
            {
                viTranslation.Question = sanitizedQuestion;
                viTranslation.Answer = sanitizedAnswer;
                viTranslation.SourceHash = sourceHash;
                viTranslation.TranslatedAt = now;
                viTranslation.UpdatedAt = now;
                viTranslation.UpdatedBy = _currentUser.UserId;
            }

            var providedEnglishQuestion = _sanitizer.Sanitize(request.EnglishQuestion ?? string.Empty).Trim();
            var providedEnglishAnswer = _sanitizer.Sanitize(request.EnglishAnswer ?? string.Empty).Trim();
            var englishProvided = !string.IsNullOrWhiteSpace(providedEnglishQuestion)
                                && !string.IsNullOrWhiteSpace(providedEnglishAnswer);

            var enTranslation = translations.FirstOrDefault(t => t.LanguageCode == "en");

            string englishQuestionOut;
            string englishAnswerOut;

            if (englishProvided)
            {
                // EN panel was open this save — trust whatever is shown there (machine-translated
                // preview left as-is, or hand-edited). Never re-call the translator here.
                englishChanged = true;
                if (enTranslation is null)
                {
                    _dbContext.FaqTranslations.Add(new FaqTranslation
                    {
                        FaqId = faq.FaqId,
                        LanguageCode = "en",
                        Question = providedEnglishQuestion,
                        Answer = providedEnglishAnswer,
                        TranslationSource = "MANUAL",
                        TranslationStatus = "READY",
                        SourceHash = sourceHash,
                        TranslatedAt = now,
                        CreatedAt = now,
                        CreatedBy = _currentUser.UserId,
                    });
                }
                else
                {
                    enTranslation.Question = providedEnglishQuestion;
                    enTranslation.Answer = providedEnglishAnswer;
                    enTranslation.TranslationSource = "MANUAL";
                    enTranslation.TranslationStatus = "READY";
                    enTranslation.SourceHash = sourceHash;
                    enTranslation.TranslatedAt = now;
                    enTranslation.UpdatedAt = now;
                    enTranslation.UpdatedBy = _currentUser.UserId;
                }
                englishQuestionOut = providedEnglishQuestion;
                englishAnswerOut = providedEnglishAnswer;
            }
            else if (enTranslation is null)
            {
                // EN panel was never opened for this FAQ — auto-translate once now so the FAQ is
                // never left English-less, same "translate exactly once at save time" rule as create.
                englishChanged = true;
                var translated = await _translator.TranslateTextAsync(
                    new List<string> { sanitizedQuestion, sanitizedAnswer },
                    NewsConstants.Languages.Default, "en", cancellationToken);
                englishQuestionOut = _sanitizer.Sanitize(translated[0]).Trim();
                englishAnswerOut = _sanitizer.Sanitize(translated[1]).Trim();

                _dbContext.FaqTranslations.Add(new FaqTranslation
                {
                    FaqId = faq.FaqId,
                    LanguageCode = "en",
                    Question = englishQuestionOut,
                    Answer = englishAnswerOut,
                    TranslationSource = "AUTO",
                    TranslationStatus = "READY",
                    SourceHash = sourceHash,
                    TranslatedAt = now,
                    CreatedAt = now,
                    CreatedBy = _currentUser.UserId,
                });
            }
            else
            {
                // EN already exists and the panel wasn't opened this time — leave it untouched.
                englishQuestionOut = enTranslation.Question;
                englishAnswerOut = enTranslation.Answer;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var userIds = new[] { faq.CreatedBy, faq.UpdatedBy }
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var userNames = userIds.Any()
                ? await _dbContext.Users
                    .AsNoTracking()
                    .Where(u => userIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.FullName })
                    .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken)
                : new Dictionary<ulong, string>();

            return new UpdateFAQResponse
            {
                FaqId = faq.FaqId,
                FaqType = faq.FaqType,
                FaqTypeLabel = FaqConstants.ToVietnameseTypeLabel(faq.FaqType),
                Question = faq.Question,
                Answer = faq.Answer,
                EnglishQuestion = englishQuestionOut,
                EnglishAnswer = englishAnswerOut,
                DisplayOrder = faq.DisplayOrder,
                Status = faq.Status,
                StatusLabel = FaqConstants.ToVietnameseStatusLabel(faq.Status),
                CreatedAt = faq.CreatedAt,
                CreatedBy = faq.CreatedBy,
                CreatedByName = faq.CreatedBy.HasValue && userNames.TryGetValue(faq.CreatedBy.Value, out var cn) ? cn : null,
                UpdatedAt = faq.UpdatedAt,
                UpdatedBy = faq.UpdatedBy,
                UpdatedByName = faq.UpdatedBy.HasValue && userNames.TryGetValue(faq.UpdatedBy.Value, out var un) ? un : null,
                Changed = viChanged || englishChanged
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>SHA-256 of the Vietnamese source content a translation was derived from.</summary>
    private static string ComputeHash(params string?[] parts)
    {
        var joined = string.Join('\x1f', parts.Select(p => p ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
