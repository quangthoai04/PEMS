using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Feedbacks.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Feedbacks.Queries.GetVisitorFeedback;

public sealed class GetVisitorFeedbackQueryHandler
    : IRequestHandler<GetVisitorFeedbackQuery, GetVisitorFeedbackResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitorFeedbackQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetVisitorFeedbackResponse> Handle(
        GetVisitorFeedbackQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);
        var visitRequest = await _db.VisitRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.VisitRequestId == instance.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", instance.VisitRequestId);

        // Only the current Host of THIS instance or a Staff Leader of ITS campus may view — the
        // same audience the "Visitor đã gửi đánh giá" notification goes to. Never trust the caller
        // blindly; re-check here so a stale/forwarded link can't leak another delegation's feedback.
        var isHost = instance.CurrentHostUserId == userId;
        var isCampusStaffLeader = !isHost && await _db.Users.AsNoTracking()
            .AnyAsync(u => u.UserId == userId
                           && u.Role.RoleCode == RoleCodes.Staff
                           && u.SubRole == UserSubRoles.Leader
                           && u.PrimaryCampusId == instance.CampusId, cancellationToken);
        if (!isHost && !isCampusStaffLeader)
            throw new ForbiddenException("Bạn không có quyền xem đánh giá của Visitor cho chuyến thăm này.");

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        string? hostName = null;
        if (instance.CurrentHostUserId.HasValue)
        {
            hostName = await _db.Users.AsNoTracking()
                .Where(u => u.UserId == instance.CurrentHostUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var feedbacks = await _db.Feedbacks.AsNoTracking()
            .Where(f => f.VisitInstanceId == request.VisitInstanceId
                        && f.FeedbackType == FeedbackTypes.VisitorOverall)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(cancellationToken);

        return new GetVisitorFeedbackResponse
        {
            VisitInstanceId = instance.VisitInstanceId,
            VisitRequestId = instance.VisitRequestId,
            RequestCode = visitRequest.RequestCode,
            DelegationName = visitRequest.DelegationName,
            OrganizationName = visitRequest.RegistrantOrganization,
            CampusName = campusName,
            HostName = hostName,
            InstanceStatus = instance.Status,
            PlannedStartAt = instance.PlannedStartAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            PlannedEndAt = instance.PlannedEndAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            Feedbacks = feedbacks.Select(f => new VisitorFeedbackItemDto
            {
                FeedbackId = f.FeedbackId,
                VisitorName = f.SubmitterNameSnapshot,
                Rating = f.Rating,
                Comment = f.Comment,
                SubmittedAt = f.SubmittedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            }).ToList(),
        };
    }
}
