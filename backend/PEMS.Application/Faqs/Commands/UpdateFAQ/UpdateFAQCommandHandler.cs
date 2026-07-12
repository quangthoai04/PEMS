using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

using PEMS.Application.Common;
namespace PEMS.Application.Faqs.Commands.UpdateFAQ;

public sealed class UpdateFAQCommandHandler : IRequestHandler<UpdateFAQCommand, UpdateFAQResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;

    public UpdateFAQCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
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

        var changed = !string.Equals(faq.FaqType, faqType, StringComparison.Ordinal)
            || !string.Equals(faq.Question, sanitizedQuestion, StringComparison.Ordinal)
            || !string.Equals(faq.Answer, sanitizedAnswer, StringComparison.Ordinal);

        if (changed)
        {
            var normalizedQuestion = sanitizedQuestion.ToLower();
            var exists = await _dbContext.Faqs
                .AsNoTracking()
                .AnyAsync(x => x.FaqId != request.FaqId && x.Question.ToLower() == normalizedQuestion, cancellationToken);

            if (exists)
                throw new ConflictException(
                    "This question already exists in the system. Please enter a different question.",
                    "DUPLICATE_FAQ_QUESTION");

            faq.FaqType = faqType;
            faq.Question = sanitizedQuestion;
            faq.Answer = sanitizedAnswer;
            faq.UpdatedAt = VietnamTime.Now();
            faq.UpdatedBy = _currentUser.UserId;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

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
            DisplayOrder = faq.DisplayOrder,
            Status = faq.Status,
            StatusLabel = FaqConstants.ToVietnameseStatusLabel(faq.Status),
            CreatedAt = faq.CreatedAt,
            CreatedBy = faq.CreatedBy,
            CreatedByName = faq.CreatedBy.HasValue && userNames.TryGetValue(faq.CreatedBy.Value, out var cn) ? cn : null,
            UpdatedAt = faq.UpdatedAt,
            UpdatedBy = faq.UpdatedBy,
            UpdatedByName = faq.UpdatedBy.HasValue && userNames.TryGetValue(faq.UpdatedBy.Value, out var un) ? un : null,
            Changed = changed
        };
    }
}
