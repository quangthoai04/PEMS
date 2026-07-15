using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Faqs;

using PEMS.Application.Common;
namespace PEMS.Application.Faqs.Commands.CreateFAQ;

public sealed class CreateFAQCommandHandler : IRequestHandler<CreateFAQCommand, CreateFAQResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;

    public CreateFAQCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
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

        var now = VietnamTime.Now();

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

        _dbContext.Faqs.Add(faq);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateFAQResponse
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
            UpdatedAt = faq.UpdatedAt,
            UpdatedBy = faq.UpdatedBy
        };
    }
}
