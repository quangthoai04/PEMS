using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.UpdateProposedHost;

/// <summary>
/// Sets, changes or clears ONE campus's proposed reception host before the confirmation gate opens.
///
/// <para>
/// This is deliberately NOT a way to change who is running a campus. Once a campus is decided the
/// host moves only through the host-handover flow, and this command refuses outright — otherwise
/// "update the proposal" would become a second, unaudited assignment path with weaker checks than
/// the first (plan §5.3).
/// </para>
///
/// <para>
/// Authority is re-derived here from the caller's account and the campus, never from a capability the
/// client echoed back: capabilities are for rendering.
/// </para>
/// </summary>
public sealed class UpdateProposedHostCommandHandler
    : IRequestHandler<UpdateProposedHostCommand, UpdateProposedHostResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IUserMutationLockService _lockService;

    public UpdateProposedHostCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IUserMutationLockService lockService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _lockService = lockService;
    }

    public async Task<UpdateProposedHostResponse> Handle(
        UpdateProposedHostCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var actorId = _currentUser.UserId.Value;

        var mode = request.HostSelectionMode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!HostSelectionModes.IsKnown(mode))
            throw new BusinessRuleException(
                "Phương án người phụ trách tiếp đón không hợp lệ.",
                VisitRequestErrorCodes.InvalidHostSelectionMode);

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var instance = visit.CampusInstances
            .FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new ConflictException(
                "Đơn đã bị hủy nên không thể đổi người phụ trách dự kiến.",
                VisitRequestErrorCodes.ProposedHostNotEditable);

        // Pre-decision only. A campus that is ASSIGNED or beyond has a real host; changing a proposal
        // there would either do nothing (confusing) or quietly re-assign (dangerous).
        if (instance.Status is not (VisitInstanceStatuses.WaitingContactConfirmation
                                    or VisitInstanceStatuses.WaitingRequestApproval))
            throw new ConflictException(
                "Cơ sở đã được xử lý — hãy dùng chức năng bàn giao người phụ trách.",
                VisitRequestErrorCodes.ProposedHostNotEditable);

        if (instance.CurrentHostUserId is not null)
            throw new ConflictException(
                "Cơ sở đã có người phụ trách chính thức.",
                VisitRequestErrorCodes.ProposedHostNotEditable);

        if (instance.RowVersion != request.ExpectedRowVersion)
            throw new ConflictException(
                "Cơ sở này vừa được cập nhật ở nơi khác. Hãy tải lại và thử lại.",
                VisitRequestErrorCodes.InstanceVersionConflict);

        var actor = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == actorId, cancellationToken)
            ?? throw new ForbiddenException();

        if (!string.Equals(actor.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản của bạn hiện không hoạt động.");

        var isLeaderHere = actor.Role.RoleCode == RoleCodes.Staff
                           && actor.SubRole == UserSubRoles.Leader
                           && actor.PrimaryCampusId == instance.CampusId;
        var isStaffHere = actor.Role.RoleCode == RoleCodes.Staff
                          && actor.SubRole == UserSubRoles.Staff
                          && actor.PrimaryCampusId == instance.CampusId;

        if (!isLeaderHere && !isStaffHere)
            throw new ForbiddenException(
                "Chỉ nhân sự IC của cơ sở này mới được đặt người phụ trách dự kiến.",
                VisitRequestErrorCodes.ProposeHostOtherCampusForbidden);

        // A regular Staff speaks only for themself: they may take an empty slot or drop their own
        // proposal, never overwrite the Leader's pick of somebody else.
        if (!isLeaderHere
            && instance.ProposedHostUserId is not null
            && instance.ProposedHostUserId != actorId)
            throw new ForbiddenException(
                "Chỉ Staff Leader mới được thay đổi người phụ trách dự kiến do người khác đặt.",
                VisitRequestErrorCodes.StaffCannotAssignOtherHost);

        var now = _clock.VietnamNow;
        ulong? proposedId = null;
        string? proposedName = null;

        if (mode == HostSelectionModes.Self)
        {
            if (request.ProposedHostUserId.HasValue && request.ProposedHostUserId.Value != actorId)
                throw new ForbiddenException(
                    "Chế độ tự nhận không được đề xuất người khác.",
                    VisitRequestErrorCodes.StaffCannotAssignOtherHost);
            proposedId = actorId;
        }
        else if (mode == HostSelectionModes.Selected)
        {
            if (!isLeaderHere)
                throw new ForbiddenException(
                    "Chỉ Staff Leader mới được đề xuất người khác làm người phụ trách tiếp đón.",
                    VisitRequestErrorCodes.StaffCannotAssignOtherHost);
            proposedId = request.ProposedHostUserId
                ?? throw new BusinessRuleException(
                    "Chọn người phụ trách khác thì phải chọn cụ thể một người.",
                    VisitRequestErrorCodes.InvalidHostCandidate);
        }

        if (proposedId is not null)
        {
            // Lock before trusting eligibility, same protocol as approve/create: a concurrent role
            // change and a host decision must not both believe they won.
            await _lockService.LockUsersAsync(new[] { proposedId.Value }, cancellationToken);

            var (eligibility, host) = await VisitHostEligibility.EvaluateAsync(
                _db, proposedId.Value, instance.CampusId, actorId, cancellationToken);
            if (eligibility == HostEligibility.NotFound || host is null)
                throw new NotFoundException("User", proposedId.Value);
            if (eligibility != HostEligibility.Eligible)
                throw new BusinessRuleException(
                    VisitHostEligibility.MessageFor(eligibility),
                    VisitRequestErrorCodes.InvalidHostCandidate);
            proposedName = host.FullName;
        }

        var previousMode = instance.HostSelectionMode;
        var previousHost = instance.ProposedHostUserId;

        instance.HostSelectionMode = mode;
        instance.ProposedHostUserId = proposedId;
        instance.ProposedHostByUserId = proposedId is null ? null : actorId;
        instance.ProposedHostAt = proposedId is null ? null : now;
        // A re-proposal after a failed activation goes back to PENDING: it is a fresh intention, and
        // leaving it NEEDS_RESELECTION would keep telling the Staff Leader to fix what they just fixed.
        instance.ProposedHostActivationStatus = proposedId is null
            ? null
            : ProposedHostActivationStatuses.Pending;
        instance.ProposedHostActivatedAt = null;
        instance.RowVersion += 1;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;

        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "UPDATE_PROPOSED_HOST",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = "EDIT",
            Reason = $"mode={previousMode}->{mode}",
            CreatedAt = now,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "proposed_host_user_id",
            OldValueText = previousHost?.ToString(),
            NewValueText = proposedId?.ToString(),
            CreatedAt = now,
        });
        _db.AuditLogs.Add(audit);

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateProposedHostResponse(
            visit.VisitRequestId, instance.VisitInstanceId, mode, proposedId, proposedName,
            instance.ProposedHostActivationStatus, instance.RowVersion,
            proposedId is null
                ? "Đã chuyển sang chờ phân công sau."
                : "Đã lưu người phụ trách tiếp đón dự kiến. Đang chờ đầu mối đoàn khách xác nhận.");
    }
}
