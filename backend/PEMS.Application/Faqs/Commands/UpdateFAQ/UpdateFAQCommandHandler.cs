using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<UpdateFAQCommandHandler> _logger;

    public UpdateFAQCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer,
        INewsTranslationService translator,
        ILogger<UpdateFAQCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
        _translator = translator;
        _logger = logger;
    }

    public async Task<UpdateFAQResponse> Handle(UpdateFAQCommand request, CancellationToken cancellationToken)
    {
        var faq = await _dbContext.Faqs
            .FirstOrDefaultAsync(x => x.FaqId == request.FaqId, cancellationToken);

        if (faq is null)
            throw new NotFoundException($"FAQ with ID {request.FaqId} was not found.");

        var sanitizedQuestion = _sanitizer.Sanitize(request.Question).Trim();
        var sanitizedAnswer = _sanitizer.Sanitize(request.Answer).Trim();

        if (string.IsNullOrWhiteSpace(sanitizedQuestion))
            throw new ValidationException("Question is required.");

        if (string.IsNullOrWhiteSpace(sanitizedAnswer))
            throw new ValidationException("Answer is required.");

        var viChanged = !string.Equals(faq.Question, sanitizedQuestion, StringComparison.Ordinal)
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

        // ── DB-TXN-007: read/prepare, then the external call, then the transactional mutation. ──
        // Everything below was previously read AND the translator called from inside the open write
        // transaction, holding it (and whatever locks it implicitly took) for the duration of an HTTP
        // round trip. Nothing here needs the transaction's atomicity to be correct: this handler takes
        // no row lock (no IUserMutationLockService involved) and the entities loaded below are the same
        // EF-tracked instances SaveChangesAsync will use once the transaction opens further down — EF's
        // change tracker is independent of when the transaction starts, so moving the read earlier
        // changes nothing about what gets written or when. The translator call keeps its exact existing
        // failure contract: a provider hiccup is caught, logged, and the row stays Vietnamese-only — that
        // was already decided outside the transaction's rollback path (the catch never rolled back), so
        // moving it fully outside changes nothing about that behavior either.
        var translations = await _dbContext.FaqTranslations
            .Where(t => t.FaqId == request.FaqId)
            .ToListAsync(cancellationToken);

        var viTranslation = translations.FirstOrDefault(t => t.LanguageCode == "vi");

        var providedEnglishQuestion = _sanitizer.Sanitize(request.EnglishQuestion ?? string.Empty).Trim();
        var providedEnglishAnswer = _sanitizer.Sanitize(request.EnglishAnswer ?? string.Empty).Trim();
        var englishProvided = !string.IsNullOrWhiteSpace(providedEnglishQuestion)
                            && !string.IsNullOrWhiteSpace(providedEnglishAnswer);

        var enTranslation = translations.FirstOrDefault(t => t.LanguageCode == "en");

        // Null means "translation unavailable this save" (auto-translate attempt failed) — the
        // EN row is then left as it was (or absent); public reads already fall back requested
        // language → vi, and the admin can translate later via the EN panel.
        string? englishQuestionOut;
        string? englishAnswerOut;
        string? autoTranslatedQuestion = null;
        string? autoTranslatedAnswer = null;

        if (englishProvided)
        {
            englishQuestionOut = providedEnglishQuestion;
            englishAnswerOut = providedEnglishAnswer;
        }
        else if (enTranslation is null)
        {
            // EN panel was never opened for this FAQ — best-effort auto-translate once so the
            // FAQ isn't left English-less. A translation-provider hiccup (quota, HTTP 400,
            // config) must never block the update itself, so failures are logged and skipped.
            try
            {
                var translated = await _translator.TranslateTextAsync(
                    new List<string> { sanitizedQuestion, sanitizedAnswer },
                    NewsConstants.Languages.Default, "en", cancellationToken);
                autoTranslatedQuestion = _sanitizer.Sanitize(translated[0]).Trim();
                autoTranslatedAnswer = _sanitizer.Sanitize(translated[1]).Trim();
                englishQuestionOut = autoTranslatedQuestion;
                englishAnswerOut = autoTranslatedAnswer;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Auto-translate to English failed for FAQ {FaqId}; leaving it Vietnamese-only for now.",
                    faq.FaqId);
                englishQuestionOut = null;
                englishAnswerOut = null;
            }
        }
        else
        {
            // EN already exists and the panel wasn't opened this time — leave it untouched.
            englishQuestionOut = enTranslation.Question;
            englishAnswerOut = enTranslation.Answer;
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            if (viChanged)
            {
                faq.Question = sanitizedQuestion;
                faq.Answer = sanitizedAnswer;
                faq.UpdatedAt = now;
                faq.UpdatedBy = _currentUser.UserId;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

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
            }
            else if (enTranslation is null && autoTranslatedQuestion is not null && autoTranslatedAnswer is not null)
            {
                englishChanged = true;
                _dbContext.FaqTranslations.Add(new FaqTranslation
                {
                    FaqId = faq.FaqId,
                    LanguageCode = "en",
                    Question = autoTranslatedQuestion,
                    Answer = autoTranslatedAnswer,
                    TranslationSource = "AUTO",
                    TranslationStatus = "READY",
                    SourceHash = sourceHash,
                    TranslatedAt = now,
                    CreatedAt = now,
                    CreatedBy = _currentUser.UserId,
                });
            }
            // else: auto-translate failed above (autoTranslatedQuestion/Answer stayed null), or EN
            // already existed and the panel wasn't opened — nothing to write for EN either way.

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
