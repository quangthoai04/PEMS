using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.ReplaceStaffLeader;

/// <summary>
/// Replace Staff Leader (Trưởng phòng IC) of a campus. HO or ADMIN only. Runs the whole swap in one
/// transaction: demote the old leader to STAFF/STAFF (status preserved), promote/create the new
/// STAFF/LEADER, repoint campuses.ic_head_user_id + departments.head_user_id, revoke sessions, and
/// audit. A LOCKED old leader additionally records a security event. See REPLACE_STAFF_LEADER spec.
/// </summary>
public sealed class ReplaceStaffLeaderCommandHandler
    : IRequestHandler<ReplaceStaffLeaderCommand, ReplaceStaffLeaderResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessionService;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IAccountEmailConfirmationService _confirmations;

    public ReplaceStaffLeaderCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        ISecurityAuditService audit,
        IDateTimeService clock,
        ISystemEmailDispatcher dispatcher,
        IAccountEmailConfirmationService confirmations)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _audit = audit;
        _clock = clock;
        _dispatcher = dispatcher;
        _confirmations = confirmations;
    }

    public async Task<ReplaceStaffLeaderResponse> Handle(
        ReplaceStaffLeaderCommand request, CancellationToken cancellationToken)
    {
        // BR-RSL-01, narrowed by ADMIN_ACCOUNT_MANAGEMENT spec §15: replacing the campus IC head is
        // personnel management and belongs to HO alone. ADMIN used to be allowed here; it now gets its
        // own code so a direct API call is refused with a reason rather than a generic 403.
        if (_currentUser.RoleCode == RoleCodes.Admin)
            throw new AuthBusinessException(
                AccountErrorCodes.AdminAccountEditNotAllowed,
                "ADMIN chỉ được xem tài khoản và xử lý khóa bảo mật; việc thay thế Trưởng phòng IC thuộc về Head Office.",
                403);

        if (_currentUser.RoleCode != RoleCodes.Ho)
            throw new ForbiddenException("Bạn không có quyền thay thế Staff Leader.");

        var actorId = _currentUser.UserId;
        var mode = (request.Mode ?? string.Empty).Trim().ToUpperInvariant();
        var reason = AccountIdentityRules.NormalizeReason(request.Reason);
        if (AccountIdentityRules.ValidateReplacementReason(reason) is { } reasonError)
            throw new ValidationException(reasonError);

        // Identity of a brand-new leader is normalized + re-validated before any write, so a direct
        // API call can never insert a name/email the modal would have rejected.
        var newUserFullName = string.Empty;
        var newUserEmail = string.Empty;
        if (mode == ReplaceStaffLeaderModes.CreateNewUser)
        {
            newUserFullName = AccountIdentityRules.NormalizeFullName(request.FullName);
            newUserEmail = AccountIdentityRules.NormalizeEmail(request.Email);
            if (AccountIdentityRules.ValidateFullName(newUserFullName) is { } nameError)
                throw new ValidationException(nameError);
            if (AccountIdentityRules.ValidateEmail(newUserEmail) is { } emailError)
                throw new ValidationException(emailError);
        }

        // ── Validate campus / IC department / current-leader consistency (read-only). The full
        //    case matrix lives in StaffLeaderAvailability; EnsureReplaceable throws 404/422/409 for
        //    every non-replaceable state (no leader → use Create; head data lệch → cleanup). ──
        var avail = await StaffLeaderAvailability.ResolveAsync(_db, request.CampusId, cancellationToken);
        StaffLeaderAvailability.EnsureReplaceable(avail);

        var oldLeaderId = avail.Leader!.UserId;
        var icDepartmentId = avail.IcDepartmentId!.Value;

        var now = _clock.VietnamNow;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        string oldLeaderEmail;
        string oldLeaderName;
        User newLeader;
        var existingUserPromoted = false;
        // Raw confirmation token for a brand-new (pending) leader — embedded in the emailed link post-commit.
        string? newLeaderConfirmationToken = null;
        try
        {
            // Tracked loads for the write.
            var campus = await _db.Campuses.FirstOrDefaultAsync(c => c.CampusId == request.CampusId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy cơ sở được chọn.");
            var icDept = await _db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == icDepartmentId, cancellationToken)
                ?? throw new BusinessRuleException("Cơ sở này chưa có Phòng Hợp tác Quốc tế đang hoạt động.");
            var oldLeader = await _db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == oldLeaderId, cancellationToken)
                ?? throw new ConflictException(
                    "Dữ liệu Staff Leader không nhất quán. Vui lòng đồng bộ dữ liệu trước khi thay thế.",
                    AccountErrorCodes.IcLeaderReferenceMismatch);

            // Concurrency re-check inside the transaction: both heads must still point to the same
            // old leader (defeats two HOs replacing the same campus at once).
            if (campus.IcHeadUserId != oldLeaderId || icDept.HeadUserId != oldLeaderId
                || oldLeader.Role.RoleCode != RoleCodes.Staff || oldLeader.SubRole != UserSubRoles.Leader)
                throw new ConflictException(
                    "Dữ liệu Staff Leader đã thay đổi. Vui lòng tải lại và thử lại.",
                    AccountErrorCodes.IcLeaderReferenceMismatch);

            oldLeaderEmail = oldLeader.Email;
            oldLeaderName = oldLeader.FullName;
            var oldLeaderStatusAfter = oldLeader.Status; // preserved (ACTIVE/INACTIVE/LOCKED — BR-RSL-10/11/12)
            var oldLeaderWasLocked = oldLeader.Status == UserStatuses.Locked;

            // ── Resolve the new leader (promote existing IC Staff, or create a fresh account). ──
            if (mode == ReplaceStaffLeaderModes.ExistingUser)
            {
                if (request.NewLeaderUserId is null)
                    throw new ValidationException("Vui lòng chọn nhân sự thay thế.");
                if (request.NewLeaderUserId.Value == oldLeaderId)
                    throw new BusinessRuleException("Nhân sự được chọn đang là Staff Leader hiện tại.");

                newLeader = await _db.Users.Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == request.NewLeaderUserId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy nhân sự được chọn.");

                // BR-RSL-15: target must be STAFF/STAFF, ACTIVE, same campus, same IC department.
                if (newLeader.Role.RoleCode != RoleCodes.Staff || newLeader.SubRole != UserSubRoles.Staff)
                    throw new BusinessRuleException(
                        "Chỉ có thể chọn nhân sự IC Staff làm Staff Leader mới.",
                        AccountErrorCodes.InvalidReplacementCandidate);
                if (newLeader.Status != UserStatuses.Active)
                    throw new BusinessRuleException(
                        "Nhân sự được chọn không ở trạng thái hoạt động.",
                        AccountErrorCodes.InvalidReplacementCandidate);
                if (newLeader.PrimaryCampusId != request.CampusId)
                    throw new BusinessRuleException(
                        "Nhân sự được chọn không thuộc cơ sở này.",
                        AccountErrorCodes.InvalidReplacementCandidate);
                if (newLeader.DepartmentId != icDepartmentId)
                    throw new BusinessRuleException(
                        "Nhân sự được chọn không thuộc Phòng Hợp tác Quốc tế của cơ sở này.",
                        AccountErrorCodes.InvalidReplacementCandidate);

                newLeader.SubRole = UserSubRoles.Leader; // promote (BR-RSL-13)
                newLeader.UpdatedAt = now;
                newLeader.UpdatedBy = actorId;
                existingUserPromoted = true;
            }
            else // CREATE_NEW_USER
            {
                var email = newUserEmail;
                if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
                    throw new ConflictException(
                        AccountIdentityRules.EmailAlreadyUsedMessage, AccountErrorCodes.EmailAlreadyExists);

                var staffRole = await _db.Roles.FirstOrDefaultAsync(
                    r => r.RoleCode == RoleCodes.Staff && r.Status == EntityStatuses.Active, cancellationToken)
                    ?? throw new ValidationException("Role 'STAFF' is not valid or not active.");

                newLeader = new User
                {
                    FullName = newUserFullName,
                    Email = email,
                    Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone!.Trim(),
                    Gender = request.Gender,
                    RoleId = staffRole.RoleId,
                    SubRole = UserSubRoles.Leader,
                    PrimaryCampusId = request.CampusId,
                    DepartmentId = icDepartmentId,
                    // P0 #1: a brand-new leader must prove email ownership before it can act. Created
                    // PENDING; it reserves the IC-head slot below but holds no authority (cannot log in)
                    // until it confirms its email and activates. No login email is sent pre-activation.
                    Status = UserStatuses.PendingEmailConfirmation,
                    CreatedVia = CreatedViaValues.ManualCreated,
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                _db.Users.Add(newLeader);
            }

            // ── Demote the old leader to STAFF/STAFF, preserving its status (BR-RSL-09/10/11/12). ──
            var oldSubRoleBefore = oldLeader.SubRole;
            oldLeader.SubRole = UserSubRoles.Staff;
            oldLeader.UpdatedAt = now;
            oldLeader.UpdatedBy = actorId;

            await _db.SaveChangesAsync(cancellationToken); // assigns newLeader.UserId in CREATE_NEW_USER

            // A brand-new leader gets a one-time email-ownership token (hash persisted with the txn below).
            if (!existingUserPromoted)
                newLeaderConfirmationToken = await _confirmations.IssuePendingAsync(
                    newLeader.UserId, newLeader.Email, isResend: false, cancellationToken);

            // ── Repoint campus + IC department heads to the new leader (BR-RSL-17). ──
            campus.IcHeadUserId = newLeader.UserId;
            campus.UpdatedBy = actorId;
            campus.UpdatedAt = now;

            icDept.HeadUserId = newLeader.UserId;
            icDept.UpdatedBy = actorId;
            icDept.UpdatedAt = now;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = request.CampusId,
                Action = "REPLACE_STAFF_LEADER",
                EntityType = "User",
                EntityId = newLeader.UserId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "StaffLeader",
                        OldValueText = JsonSerializer.Serialize(new
                        {
                            oldLeaderUserId = oldLeaderId,
                            oldLeaderSubRoleBefore = oldSubRoleBefore,
                            oldLeaderSubRoleAfter = UserSubRoles.Staff,
                            oldLeaderStatus = oldLeaderStatusAfter,
                        }),
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            campusId = request.CampusId,
                            icDepartmentId,
                            newLeaderUserId = newLeader.UserId,
                            mode,
                            reason,
                        })
                    }
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);

            // ── Revoke sessions inside the transaction (BR-RSL-18/19). Old leader always; new
            //    leader too when promoting an existing account so its token picks up the new role. ──
            await _sessionService.RevokeAllActiveSessionsAsync(
                oldLeaderId, SessionRevokeReasons.RoleChanged, actorId, cancellationToken);
            if (existingUserPromoted)
                await _sessionService.RevokeAllActiveSessionsAsync(
                    newLeader.UserId, SessionRevokeReasons.RoleChanged, actorId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // Security event for replacing a LOCKED leader (recorded after commit; BR-RSL-12/§18).
            if (oldLeaderWasLocked)
                await _audit.WriteSecurityEventAsync(
                    userId: oldLeaderId,
                    emailSnapshot: oldLeaderEmail,
                    eventType: SecurityEventTypes.SecurityPolicyCheck,
                    result: "SUCCESS",
                    selectedCampusId: request.CampusId,
                    detailText: $"event={SecurityEventDetailMarkers.StaffLeaderReplacedWhileLocked}; campusId={request.CampusId}; reason={reason}",
                    cancellationToken: cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── Notifications after commit (non-fatal). New-leader email outcome surfaced to the UI.
        //    The replacement reason is included in both emails. ──
        var emailStatus = await SendNewLeaderEmailAsync(
            newLeader, avail.CampusName, reason, now, newLeaderConfirmationToken, cancellationToken);
        await SendOldLeaderEmailAsync(
            oldLeaderId, oldLeaderEmail, oldLeaderName, avail.CampusName,
            newLeader.FullName, reason, now, cancellationToken);

        return new ReplaceStaffLeaderResponse
        {
            CampusId = request.CampusId,
            IcDepartmentId = icDepartmentId,
            OldLeaderUserId = oldLeaderId,
            NewLeaderUserId = newLeader.UserId,
            NewLeaderEmail = newLeader.Email,
            EmailNotificationStatus = emailStatus,
        };
    }

    private async Task<string> SendNewLeaderEmailAsync(
        User newLeader, string? campusName, string reason, DateTime effectiveAt,
        string? confirmationToken, CancellationToken cancellationToken)
    {
        // Brand-new leader (PENDING) → confirmation link ONLY, never a "log in now" email pre-activation.
        if (confirmationToken is not null)
        {
            var confirmation = await _dispatcher.SendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountEmailConfirmation,
                new EmailRecipient(newLeader.Email, newLeader.FullName),
                // The account was just written as STAFF/LEADER on this campus — that is the shape the
                // email describes, so there is nothing to read back.
                await AccountEmailVariables.ForConfirmationAsync(
                    _db, newLeader.FullName, RoleCodes.Staff, newLeader.SubRole,
                    newLeader.PrimaryCampusId, _confirmations.ExpiryHours, cancellationToken),
                TrustedBlocks: AccountEmailVariables.ConfirmationBlocks(_confirmations.BuildConfirmUrl(confirmationToken)),
                RelatedType: "User",
                RelatedId: newLeader.UserId,
                SentBy: _currentUser.UserId), cancellationToken);

            return confirmation.NotificationStatus;
        }

        // Promoted existing (already active + confirmed) leader → role-change notification.
        // The catch preserves this call site's pre-existing behaviour: the replacement is already
        // committed, so nothing about the notification may turn a successful swap into a failed request.
        try
        {
            var result = await _dispatcher.SendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountStaffLeaderAssigned,
                new EmailRecipient(newLeader.Email, newLeader.FullName),
                AccountEmailVariables.ForStaffLeaderAssigned(
                    newLeader.FullName, campusName, effectiveAt, reason),
                RelatedType: "User",
                RelatedId: newLeader.UserId,
                SentBy: _currentUser.UserId), cancellationToken);

            return result.NotificationStatus;
        }
        catch
        {
            return "FAILED";
        }
    }

    private async Task SendOldLeaderEmailAsync(
        ulong oldLeaderId, string email, string fullName, string? campusName, string successorName,
        string reason, DateTime effectiveAt, CancellationToken cancellationToken)
    {
        try
        {
            await _dispatcher.SendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountStaffLeaderReplaced,
                new EmailRecipient(email, fullName),
                AccountEmailVariables.ForStaffLeaderReplaced(
                    fullName, campusName, successorName, effectiveAt, reason),
                RelatedType: "User",
                RelatedId: oldLeaderId,
                SentBy: _currentUser.UserId), cancellationToken);
        }
        catch
        {
            // Replace already committed; a failed notification must not fail the request.
        }
    }
}
