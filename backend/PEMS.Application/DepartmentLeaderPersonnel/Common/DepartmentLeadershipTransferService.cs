using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// See <see cref="IDepartmentLeadershipTransferService"/>. Concurrency contract: lock the
/// expected-old and new leader together (ascending, one call), THEN the department — users before
/// departments, everywhere, is what keeps this deadlock-free against every other flow that locks
/// both — then re-read the department UNDER LOCK and unconditionally compare its HeadUserId against
/// the caller's pre-lock read. Extracted from the original self-service-only
/// <c>TransferDepartmentLeadershipCommandHandler</c> so the legacy third-party reassignment path
/// gains the exact same atomicity, candidate validation, audit trail, session revocation and
/// notification — not just an authorization gate bolted onto its old, thinner logic.
/// </summary>
public sealed class DepartmentLeadershipTransferService : IDepartmentLeadershipTransferService
{
    private readonly IApplicationDbContext _db;
    private readonly IUserMutationLockService _lockService;
    private readonly ISessionService _sessionService;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IDateTimeService _clock;

    public DepartmentLeadershipTransferService(
        IApplicationDbContext db,
        IUserMutationLockService lockService,
        ISessionService sessionService,
        ISystemEmailDispatcher dispatcher,
        IDateTimeService clock)
    {
        _db = db;
        _lockService = lockService;
        _sessionService = sessionService;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<DepartmentLeadershipTransferResult> TransferAsync(
        ulong departmentId,
        ulong expectedCurrentLeaderUserId,
        ulong newLeaderUserId,
        ulong actorUserId,
        bool actorMustBeCurrentLeader,
        CancellationToken cancellationToken)
    {
        if (expectedCurrentLeaderUserId == newLeaderUserId)
            throw new BusinessRuleException(
                "Người này đã là Trưởng phòng của phòng ban này.",
                DepartmentLeaderErrorCodes.LeaderCandidateInvalid);

        var now = _clock.VietnamNow;

        ulong previousLeaderId;
        string previousLeaderName;
        string previousLeaderEmail;
        string newLeaderName;
        string newLeaderEmail;
        string departmentName;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            var usersToLock = expectedCurrentLeaderUserId == newLeaderUserId
                ? new[] { expectedCurrentLeaderUserId }
                : new[] { expectedCurrentLeaderUserId, newLeaderUserId };
            await _lockService.LockUsersAsync(usersToLock, cancellationToken);
            await _lockService.LockDepartmentsAsync(new[] { departmentId }, cancellationToken);

            // Re-read AFTER the lock — anything read before it (including the caller's own
            // pre-check) is potentially stale.
            var department = await _db.Departments
                .FirstAsync(d => d.DepartmentId == departmentId, cancellationToken);

            // Unconditional for BOTH callers: the seat this transaction is about to move must still
            // be the one the caller thinks it is, regardless of who the actor is.
            if (department.HeadUserId != expectedCurrentLeaderUserId)
                throw new ConflictException(
                    "Trưởng phòng của phòng ban này vừa thay đổi. Vui lòng tải lại trang và thử lại.",
                    DepartmentLeaderErrorCodes.LeadershipAlreadyChanged);

            // Self-service only: the actor must literally BE that head, not merely be authorized to
            // oversee whoever the head happens to be. Checked against the same already-locked,
            // already-re-read row — no extra query.
            if (actorMustBeCurrentLeader && department.HeadUserId != actorUserId)
                throw new ForbiddenException();

            if (department.Status != EntityStatuses.Active)
                throw new BusinessRuleException(
                    "Phòng ban đã ngừng hoạt động nên không thể đổi Trưởng phòng.",
                    DepartmentLeaderErrorCodes.DepartmentNotActive);

            var outgoing = await _db.Users
                .Include(u => u.Role)
                .FirstAsync(u => u.UserId == expectedCurrentLeaderUserId, cancellationToken);

            var incoming = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == newLeaderUserId, cancellationToken);

            // Re-validate the candidate against the SAME predicates the candidates list used — it
            // may be seconds old, and the account could have been disabled, moved or promoted since.
            EnsureUsableSuccessor(incoming, department.DepartmentId, department.CampusId);

            previousLeaderId = outgoing.UserId;
            previousLeaderName = outgoing.FullName;
            previousLeaderEmail = outgoing.Email;
            newLeaderName = incoming!.FullName;
            newLeaderEmail = incoming.Email;
            departmentName = department.Name;

            // Three writes that must be atomic: between them the department is momentarily
            // inconsistent, which is exactly why they share this transaction and the row locks above.
            outgoing.SubRole = UserSubRoles.Staff;
            outgoing.UpdatedAt = now;
            outgoing.UpdatedBy = actorUserId;

            incoming.SubRole = UserSubRoles.Leader;
            incoming.UpdatedAt = now;
            incoming.UpdatedBy = actorUserId;

            department.HeadUserId = incoming.UserId;
            department.UpdatedAt = now;
            department.UpdatedBy = actorUserId;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorUserId,
                CampusId = department.CampusId,
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

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Post-commit: both tokens now claim a sub-role neither account holds. A revoke/notify
        // failure here must never undo a leadership change that already committed.
        var revoked = await _sessionService.RevokeAllActiveSessionsAsync(
            previousLeaderId, SessionRevokeReasons.RoleChanged, actorUserId, cancellationToken);
        revoked += await _sessionService.RevokeAllActiveSessionsAsync(
            newLeaderUserId, SessionRevokeReasons.RoleChanged, actorUserId, cancellationToken);

        var emailStatus = await SendTransferMailsAsync(
            previousLeaderId, previousLeaderName, previousLeaderEmail,
            newLeaderUserId, newLeaderName, newLeaderEmail,
            departmentName, actorUserId, cancellationToken);

        return new DepartmentLeadershipTransferResult(
            departmentId, departmentName,
            previousLeaderId, previousLeaderName,
            newLeaderUserId, newLeaderName,
            revoked, emailStatus);
    }

    /// <summary>
    /// The successor must be an ACTIVE <c>DEPARTMENT + STAFF</c> member of THIS department, in its
    /// campus. Each failure gets its own code so the UI can say what is actually wrong instead of a
    /// generic "invalid candidate".
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
