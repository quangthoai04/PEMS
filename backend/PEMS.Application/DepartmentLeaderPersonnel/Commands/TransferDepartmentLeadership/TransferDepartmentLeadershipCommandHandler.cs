using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.TransferDepartmentLeadership;

/// <summary>
/// Spec §16 — moves the department head from the caller to one of their staff.
///
/// Three writes have to be atomic: the outgoing head becomes STAFF, the incoming one becomes LEADER,
/// and <c>departments.head_user_id</c> moves. They run inside a single transaction that takes the row
/// locks FIRST, so there is no observable instant in which the department has no leader or two of
/// them, and two concurrent transfers cannot both succeed — whichever locks first commits, and the
/// other re-reads a department it no longer heads and returns 409 instead of overwriting the winner.
///
/// The order is: lock (users ascending, then department) → re-read → validate → write → commit →
/// revoke sessions → notify. Both accounts must sign in again, because both are carrying a token that
/// now claims the wrong sub-role.
/// </summary>
public sealed class TransferDepartmentLeadershipCommandHandler
    : IRequestHandler<TransferDepartmentLeadershipCommand, TransferDepartmentLeadershipResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;
    private readonly IUserMutationLockService _lockService;
    private readonly ISessionService _sessionService;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IDateTimeService _clock;

    public TransferDepartmentLeadershipCommandHandler(
        IApplicationDbContext db,
        IDepartmentLeaderPersonnelScopeService scopeService,
        IUserMutationLockService lockService,
        ISessionService sessionService,
        ISystemEmailDispatcher dispatcher,
        IDateTimeService clock)
    {
        _db = db;
        _scopeService = scopeService;
        _lockService = lockService;
        _sessionService = sessionService;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<TransferDepartmentLeadershipResponse> Handle(
        TransferDepartmentLeadershipCommand request, CancellationToken cancellationToken)
    {
        // 1. Authenticate + verify the caller is the seated head, before anything else.
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        if (request.NewLeaderUserId == scope.ActorUserId)
            throw new BusinessRuleException(
                "Bạn đã là Trưởng phòng của phòng ban này.",
                DepartmentLeaderErrorCodes.LeaderCandidateInvalid);

        var now = _clock.VietnamNow;

        ulong previousLeaderId;
        string previousLeaderName;
        string previousLeaderEmail;
        string newLeaderName;
        string newLeaderEmail;

        // 2. One transaction for all three writes.
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            // 3-5. Lock both users in ONE call (the service orders ids ascending, which locking them
            //      one at a time in request order would not) and then the department. Users before
            //      departments, everywhere, is what makes deadlock impossible.
            await _lockService.LockUsersAsync(
                new[] { scope.ActorUserId, request.NewLeaderUserId }, cancellationToken);
            await _lockService.LockDepartmentsAsync(new[] { scope.DepartmentId }, cancellationToken);

            // 6. Re-read everything AFTER the lock. Anything read before it is potentially stale.
            var department = await _db.Departments
                .FirstAsync(d => d.DepartmentId == scope.DepartmentId, cancellationToken);

            // 7. Still ours? If a concurrent transfer moved the seat, this request is acting on a
            //    stale screen and must reload rather than clobber the other outcome.
            if (department.HeadUserId != scope.ActorUserId)
                throw new ConflictException(
                    "Trưởng phòng của phòng ban này vừa thay đổi. Vui lòng tải lại trang và thử lại.",
                    DepartmentLeaderErrorCodes.LeadershipAlreadyChanged);

            if (department.Status != EntityStatuses.Active)
                throw new BusinessRuleException(
                    "Phòng ban đã ngừng hoạt động nên không thể đổi Trưởng phòng.",
                    DepartmentLeaderErrorCodes.DepartmentNotActive);

            var outgoing = await _db.Users
                .Include(u => u.Role)
                .FirstAsync(u => u.UserId == scope.ActorUserId, cancellationToken);

            var incoming = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == request.NewLeaderUserId, cancellationToken);

            // 8. Re-validate the candidate against the SAME predicates the candidates endpoint used.
            //    The list the operator saw may be seconds old — the account could have been disabled,
            //    moved, or promoted in the meantime.
            EnsureUsableSuccessor(incoming, department.DepartmentId, department.CampusId);

            previousLeaderId = outgoing.UserId;
            previousLeaderName = outgoing.FullName;
            previousLeaderEmail = outgoing.Email;
            newLeaderName = incoming!.FullName;
            newLeaderEmail = incoming.Email;

            // 9-11. The three writes. Between them the department is momentarily inconsistent, which is
            //       exactly why they share one transaction and the row locks above — no other
            //       transaction can observe the intermediate state.
            outgoing.SubRole = UserSubRoles.Staff;
            outgoing.UpdatedAt = now;
            outgoing.UpdatedBy = scope.ActorUserId;

            incoming.SubRole = UserSubRoles.Leader;
            incoming.UpdatedAt = now;
            incoming.UpdatedBy = scope.ActorUserId;

            department.HeadUserId = incoming.UserId;
            department.UpdatedAt = now;
            department.UpdatedBy = scope.ActorUserId;

            // 12. Audit.
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = scope.ActorUserId,
                CampusId = scope.CampusId,
                Action = DepartmentPersonnelAuditActions.TransferLeadership,
                EntityType = "Department",
                EntityId = department.DepartmentId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "HeadUserId",
                        OldValueText = JsonSerializer.Serialize(new
                        {
                            headUserId = previousLeaderId,
                            subRole = UserSubRoles.Leader,
                        }),
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            headUserId = incoming.UserId,
                            previousLeaderNewSubRole = UserSubRoles.Staff,
                            newLeaderSubRole = UserSubRoles.Leader,
                            departmentId = department.DepartmentId,
                        }),
                    },
                },
                CreatedAt = now,
            });

            // 13-14.
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // 15. Both tokens now claim a sub-role neither account holds — revoke them. Post-commit, so a
        //     revoke failure cannot undo a leadership change that already happened.
        var revoked = await _sessionService.RevokeAllActiveSessionsAsync(
            previousLeaderId, SessionRevokeReasons.RoleChanged, scope.ActorUserId, cancellationToken);
        revoked += await _sessionService.RevokeAllActiveSessionsAsync(
            request.NewLeaderUserId, SessionRevokeReasons.RoleChanged, scope.ActorUserId, cancellationToken);

        // 16. Notify both parties. Best-effort.
        var emailStatus = await SendTransferMailsAsync(
            previousLeaderId, previousLeaderName, previousLeaderEmail,
            request.NewLeaderUserId, newLeaderName, newLeaderEmail,
            scope.DepartmentName, scope.ActorUserId, cancellationToken);

        return new TransferDepartmentLeadershipResponse
        {
            Success = true,
            DepartmentId = scope.DepartmentId,
            PreviousLeaderUserId = previousLeaderId,
            PreviousLeaderName = previousLeaderName,
            NewLeaderUserId = request.NewLeaderUserId,
            NewLeaderName = newLeaderName,
            RevokedSessions = revoked,
            ActorMustSignInAgain = true,
            EmailNotificationStatus = emailStatus,
            Message = $"Đã chuyển vai trò Trưởng phòng cho {newLeaderName}. "
                      + "Bạn không còn quyền quản lý phòng ban và sẽ được đăng xuất.",
        };
    }

    /// <summary>
    /// The successor must be an ACTIVE <c>DEPARTMENT + STAFF</c> member of THIS department, in its
    /// campus. Each failure gets its own code so the UI can say what is actually wrong instead of a
    /// generic "invalid candidate" (spec §18).
    /// </summary>
    private static void EnsureUsableSuccessor(User? candidate, ulong departmentId, ulong campusId)
    {
        if (candidate is null)
            throw new BusinessRuleException(
                "Không tìm thấy nhân sự được chọn làm Trưởng phòng mới.",
                DepartmentLeaderErrorCodes.LeaderCandidateInvalid);

        if (candidate.Role?.RoleCode != RoleCodes.Department)
            throw new BusinessRuleException(
                "Trưởng phòng mới phải là nhân sự thuộc phòng ban.",
                DepartmentLeaderErrorCodes.LeaderCandidateInvalid);

        if (candidate.DepartmentId != departmentId)
            throw new BusinessRuleException(
                "Trưởng phòng mới phải thuộc đúng phòng ban đang bàn giao.",
                DepartmentLeaderErrorCodes.LeaderCandidateWrongDepartment);

        if (candidate.PrimaryCampusId != campusId)
            throw new BusinessRuleException(
                "Trưởng phòng mới không thuộc cơ sở của phòng ban này.",
                DepartmentLeaderErrorCodes.LeaderCandidateWrongDepartment);

        if (candidate.SubRole != UserSubRoles.Staff)
            throw new BusinessRuleException(
                "Chỉ có thể chọn nhân viên phòng ban làm Trưởng phòng mới.",
                DepartmentLeaderErrorCodes.LeaderCandidateInvalid);

        // PENDING and LOCKED land here too: an account that cannot sign in must not be handed a
        // department.
        if (candidate.Status != UserStatuses.Active)
            throw new BusinessRuleException(
                "Trưởng phòng mới phải là tài khoản đang hoạt động.",
                DepartmentLeaderErrorCodes.LeaderCandidateNotActive);
    }

    private async Task<string> SendTransferMailsAsync(
        ulong previousLeaderUserId,
        string previousLeaderName,
        string previousLeaderEmail,
        ulong newLeaderUserId,
        string newLeaderName,
        string newLeaderEmail,
        string departmentName,
        ulong? actorId,
        CancellationToken cancellationToken)
    {
        var newOk = await TrySendAsync(new SystemEmailRequest(
            SystemEmailTemplates.DeptLeadershipGranted,
            new EmailRecipient(newLeaderEmail, newLeaderName),
            new Dictionary<string, string>
            {
                ["fullName"] = newLeaderName,
                ["departmentName"] = departmentName,
            },
            RelatedType: "User",
            RelatedId: newLeaderUserId,
            SentBy: actorId), cancellationToken);

        var oldOk = await TrySendAsync(new SystemEmailRequest(
            SystemEmailTemplates.DeptLeadershipHandedOver,
            new EmailRecipient(previousLeaderEmail, previousLeaderName),
            new Dictionary<string, string>
            {
                ["fullName"] = previousLeaderName,
                ["departmentName"] = departmentName,
            },
            RelatedType: "User",
            RelatedId: previousLeaderUserId,
            SentBy: actorId), cancellationToken);

        return (oldOk, newOk) switch
        {
            (true, true) => DepartmentPersonnelEmails.StatusSent,
            (false, false) => DepartmentPersonnelEmails.StatusFailed,
            _ => DepartmentPersonnelEmails.StatusPartial,
        };
    }

    private async Task<bool> TrySendAsync(SystemEmailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dispatcher.SendAsync(request, cancellationToken);
            return result.Delivery.Status == EmailDeliveryStatus.Sent;
        }
        catch
        {
            return false; // the transfer is committed; a notification failure must not surface as one
        }
    }
}
