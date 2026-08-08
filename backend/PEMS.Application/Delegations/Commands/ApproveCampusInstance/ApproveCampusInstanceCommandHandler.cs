using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.ApproveCampusInstance;

/// <summary>
/// The campus Staff Leader approves ONE campus and names its Host, in one act.
///
/// <para>
/// The approval itself lives in <see cref="ICampusApprovalExecutor"/> rather than here, because this is
/// no longer the only way a campus gets approved: a Staff Leader who is also the registrant can fix the
/// schedule and approve in a single transaction ("Lưu và duyệt"). Both paths must produce exactly the
/// same approved campus — same Host invariant, same participant row, same aggregate, same audit — and
/// two copies of that would drift.
/// </para>
/// </summary>
public sealed class ApproveCampusInstanceCommandHandler
    : IRequestHandler<ApproveCampusInstanceCommand, ApproveCampusInstanceResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ICampusApprovalExecutor _approval;

    public ApproveCampusInstanceCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        ICampusApprovalExecutor approval)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _approval = approval;
    }

    public async Task<ApproveCampusInstanceResponse> Handle(
        ApproveCampusInstanceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Approval authority is the Staff Leader OF THIS CAMPUS. Role and campus are one relation, not
        // two checks: a leader of another campus is a stranger to this one whatever their role says.
        if (!VisitRequestOwnership.IsCampusLeader(_currentUser, instance.CampusId))
            throw new ForbiddenException(
                _currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader
                    ? "Cơ sở này không thuộc phạm vi phụ trách của bạn."
                    : "Chỉ Staff Leader mới được duyệt và gán host.");

        var previousRequestStatus = visit.Status;
        var now = _clock.VietnamNow;

        CampusApprovalOutcome outcome;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            outcome = await _approval.ApproveAndAssignHostAsync(
                visit, instance, request.HostUserId, request.DecisionNote, actorId, now, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        // Post-commit and best-effort: the campus IS approved whatever happens to the notifications.
        // They used to run inside the transaction, which meant a notification failure rolled back a
        // decision the Staff Leader had already taken.
        await _approval.NotifyApprovedAsync(
            visit, instance, outcome, previousRequestStatus, actorId, cancellationToken);

        return new ApproveCampusInstanceResponse(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            outcome.RequestStatus,
            outcome.InstanceStatus,
            outcome.HostUserId,
            outcome.Message,
            outcome.HasHostingConflict);
    }
}
