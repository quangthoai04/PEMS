using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.VisitLinks.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.VisitLinks.Commands.RejectVisitGuestPartnerSuggestion;

public sealed class RejectVisitGuestPartnerSuggestionCommandHandler
    : IRequestHandler<RejectVisitGuestPartnerSuggestionCommand, RejectVisitGuestPartnerSuggestionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public RejectVisitGuestPartnerSuggestionCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<RejectVisitGuestPartnerSuggestionResponse> Handle(
        RejectVisitGuestPartnerSuggestionCommand request, CancellationToken cancellationToken)
    {
        var instance = await VisitLinkSupport.LoadInstanceWithAccessAsync(
            _db, _currentUser, request.VisitInstanceId, cancellationToken);

        var link = await _db.VisitGuestPartnerLinks
            .FirstOrDefaultAsync(l => l.LinkId == request.LinkId
                                      && l.VisitRequestId == instance.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitGuestPartnerLink", request.LinkId);

        var now = _clock.VietnamNow;
        link.MatchStatus = PartnerLinkMatchStatuses.Rejected;
        link.UpdatedAt = now;
        link.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = instance.CampusId,
            Action = "REJECT_VISIT_GUEST_PARTNER_SUGGESTION",
            EntityType = "VisitGuestPartnerLink",
            EntityId = link.LinkId,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new RejectVisitGuestPartnerSuggestionResponse { LinkId = link.LinkId };
    }
}
