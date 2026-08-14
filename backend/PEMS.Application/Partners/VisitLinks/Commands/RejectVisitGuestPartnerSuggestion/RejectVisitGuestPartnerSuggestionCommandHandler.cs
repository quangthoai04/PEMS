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

        // Scoped to the request AND this instance — dismissing through a sibling campus's endpoint is
        // a cross-instance write (PART-08). Legacy request-wide rows carry a null instance.
        var link = await _db.VisitGuestPartnerLinks
            .FirstOrDefaultAsync(l => l.LinkId == request.LinkId
                                      && l.VisitRequestId == instance.VisitRequestId
                                      && (l.VisitInstanceId == null
                                          || l.VisitInstanceId == instance.VisitInstanceId), cancellationToken)
            ?? throw new NotFoundException("VisitGuestPartnerLink", request.LinkId);

        // "Bỏ qua gợi ý" only ever applies to a SUGGESTION. A CONFIRMED relationship is a decision
        // somebody made on the record; dropping it must be its own audited use case, not a side effect
        // of the dismiss button (PART-05).
        if (link.MatchStatus == PartnerLinkMatchStatuses.Confirmed)
            throw new BusinessRuleException(
                "Liên kết này đã được xác nhận. Hãy dùng chức năng huỷ liên kết nếu muốn gỡ bỏ.",
                PartnerErrorCodes.LinkNotFound);

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
