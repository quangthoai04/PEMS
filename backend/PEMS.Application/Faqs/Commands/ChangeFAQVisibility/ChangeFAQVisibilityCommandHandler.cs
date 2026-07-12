using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Faqs;

using PEMS.Application.Common;
namespace PEMS.Application.Faqs.Commands.ChangeFAQVisibility;

public sealed class ChangeFAQVisibilityCommandHandler
    : IRequestHandler<ChangeFAQVisibilityCommand, ChangeFAQVisibilityResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ChangeFAQVisibilityCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ChangeFAQVisibilityResponse> Handle(
        ChangeFAQVisibilityCommand request,
        CancellationToken cancellationToken)
    {
        var faq = await _dbContext.Faqs
            .FirstOrDefaultAsync(x => x.FaqId == request.FaqId, cancellationToken);

        if (faq is null)
            throw new NotFoundException(nameof(Faq), request.FaqId);

        faq.Status = faq.Status == FaqConstants.Status.Published
            ? FaqConstants.Status.Hidden
            : FaqConstants.Status.Published;

        faq.UpdatedAt = VietnamTime.Now();
        faq.UpdatedBy = _currentUser.UserId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChangeFAQVisibilityResponse
        {
            FaqId = faq.FaqId,
            NewStatus = faq.Status,
            NewStatusLabel = FaqConstants.ToVietnameseStatusLabel(faq.Status)
        };
    }
}
