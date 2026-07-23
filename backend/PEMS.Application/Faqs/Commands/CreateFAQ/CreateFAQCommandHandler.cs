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
namespace PEMS.Application.Faqs.Commands.CreateFAQ;

public sealed class CreateFAQCommandHandler : IRequestHandler<CreateFAQCommand, CreateFAQResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly INewsTranslationService _translator;
    private readonly ILogger<CreateFAQCommandHandler> _logger;

    public CreateFAQCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer,
        INewsTranslationService translator,
        ILogger<CreateFAQCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
        _translator = translator;
        _logger = logger;
    }

    public async Task<CreateFAQResponse> Handle(CreateFAQCommand request, CancellationToken cancellationToken)
    {
        var faqType = request.FaqType.Trim();

        var status = string.IsNullOrWhiteSpace(request.Status)
            ? FaqConstants.Status.Published
            : request.Status.Trim();

        var sanitizedQuestion = _sanitizer.Sanitize(request.Question).Trim();
        var sanitizedAnswer = _sanitizer.Sanitize(request.Answer).Trim();

        if (string.IsNullOrWhiteSpace(sanitizedQuestion))
            throw new ValidationException("Question is required.");

        if (string.IsNullOrWhiteSpace(sanitizedAnswer))
            throw new ValidationException("Answer is required.");

        var normalizedQuestion = sanitizedQuestion.ToLower();

        var exists = await _dbContext.Faqs
            .AsNoTracking()
            .AnyAsync(x => x.Question.ToLower() == normalizedQuestion, cancellationToken);

        if (exists)
            throw new ConflictException(
                "This question already exists in the system. Please enter a different question.",
                "DUPLICATE_FAQ_QUESTION");

        // English: content provided by the admin (EN panel was opened — machine-translated preview
        // or hand-edited) wins as-is (MANUAL). Otherwise the backend translates once, right now,
        // so a FAQ is never created English-less.
        var providedEnglishQuestion = _sanitizer.Sanitize(request.EnglishQuestion ?? string.Empty).Trim();
        var providedEnglishAnswer = _sanitizer.Sanitize(request.EnglishAnswer ?? string.Empty).Trim();
        var englishProvided = !string.IsNullOrWhiteSpace(providedEnglishQuestion)
                            && !string.IsNullOrWhiteSpace(providedEnglishAnswer);

        // Null means "translation unavailable" (auto-translate attempt failed) — the EN row is then
        // simply not created; public reads already fall back requested language → vi, and the
        // admin can translate later via the EN panel.
        string? englishQuestion;
        string? englishAnswer;
        string englishSource = "AUTO";
        if (englishProvided)
        {
            englishQuestion = providedEnglishQuestion;
            englishAnswer = providedEnglishAnswer;
            englishSource = "MANUAL";
        }
        else
        {
            // Best-effort only: a translation-provider hiccup (quota, HTTP 400, config) must never
            // block creating the FAQ itself.
            try
            {
                var translated = await _translator.TranslateTextAsync(
                    new List<string> { sanitizedQuestion, sanitizedAnswer },
                    NewsConstants.Languages.Default, "en", cancellationToken);
                englishQuestion = _sanitizer.Sanitize(translated[0]).Trim();
                englishAnswer = _sanitizer.Sanitize(translated[1]).Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Auto-translate to English failed for FAQ \"{Question}\"; saving Vietnamese-only for now.",
                    sanitizedQuestion);
                englishQuestion = null;
                englishAnswer = null;
            }
        }

        var now = VietnamTime.Now();
        var sourceHash = ComputeHash(sanitizedQuestion, sanitizedAnswer);

        var faq = new Faq
        {
            FaqType = faqType,
            Question = sanitizedQuestion,
            Answer = sanitizedAnswer,
            DisplayOrder = 0,
            Status = status,
            CreatedAt = now,
            CreatedBy = _currentUser.UserId,
        };

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Faqs.Add(faq);
            await _dbContext.SaveChangesAsync(cancellationToken); // get FaqId

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

            if (englishQuestion is not null && englishAnswer is not null)
            {
                _dbContext.FaqTranslations.Add(new FaqTranslation
                {
                    FaqId = faq.FaqId,
                    LanguageCode = "en",
                    Question = englishQuestion,
                    Answer = englishAnswer,
                    TranslationSource = englishSource,
                    TranslationStatus = "READY",
                    SourceHash = sourceHash,
                    TranslatedAt = now,
                    CreatedAt = now,
                    CreatedBy = _currentUser.UserId,
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new CreateFAQResponse
        {
            FaqId = faq.FaqId,
            FaqType = faq.FaqType,
            FaqTypeLabel = FaqConstants.ToVietnameseTypeLabel(faq.FaqType),
            Question = faq.Question,
            Answer = faq.Answer,
            EnglishQuestion = englishQuestion,
            EnglishAnswer = englishAnswer,
            DisplayOrder = faq.DisplayOrder,
            Status = faq.Status,
            StatusLabel = FaqConstants.ToVietnameseStatusLabel(faq.Status),
            CreatedAt = faq.CreatedAt,
            CreatedBy = faq.CreatedBy,
            UpdatedAt = faq.UpdatedAt,
            UpdatedBy = faq.UpdatedBy
        };
    }

    /// <summary>SHA-256 of the Vietnamese source content a translation was derived from.</summary>
    private static string ComputeHash(params string?[] parts)
    {
        var joined = string.Join('\x1f', parts.Select(p => p ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
