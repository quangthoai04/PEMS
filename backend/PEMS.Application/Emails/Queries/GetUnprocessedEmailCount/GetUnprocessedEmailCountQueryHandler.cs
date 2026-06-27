using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Emails.Queries.GetUnprocessedEmailCount;

public class GetUnprocessedEmailCountQueryHandler : IRequestHandler<GetUnprocessedEmailCountQuery, GetUnprocessedEmailCountResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUnprocessedEmailCountQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<GetUnprocessedEmailCountResponse> Handle(GetUnprocessedEmailCountQuery request, CancellationToken cancellationToken)
    {
        var currentUserEmail = _currentUserService.Email;
        if (string.IsNullOrEmpty(currentUserEmail))
        {
            var user = await _context.Users.FindAsync(_currentUserService.UserId);
            currentUserEmail = user?.Email ?? "";
        }

        var count = await _context.SentEmails
            .Include(e => e.Recipients)
            .Where(e => e.Recipients.Any(r => r.RecipientEmail == currentUserEmail) && !e.DeliveredAt.HasValue && e.Status != "FAILED")
            .CountAsync(cancellationToken);

        return new GetUnprocessedEmailCountResponse { Count = count };
    }
}
