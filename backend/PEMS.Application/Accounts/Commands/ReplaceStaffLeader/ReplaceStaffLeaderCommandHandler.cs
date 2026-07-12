using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.ReplaceStaffLeader;

/// <summary>
/// Replace Staff Leader (Trưởng phòng IC) of a campus. HO only. Runs the whole swap in one
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
    private readonly IEmailService _emailService;

    public ReplaceStaffLeaderCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        ISecurityAuditService audit,
        IDateTimeService clock,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _audit = audit;
        _clock = clock;
        _emailService = emailService;
    }

    public async Task<ReplaceStaffLeaderResponse> Handle(
        ReplaceStaffLeaderCommand request, CancellationToken cancellationToken)
    {
        // BR-RSL-01: only HO may replace a Staff Leader.
        if (_currentUser.RoleCode != RoleCodes.Ho)
            throw new ForbiddenException("Bạn không có quyền thay thế Staff Leader.");

        var actorId = _currentUser.UserId;
        var mode = (request.Mode ?? string.Empty).Trim().ToUpperInvariant();
        var reason = request.Reason.Trim();

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
                var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
                if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
                    throw new ConflictException("Email này đã tồn tại trong hệ thống.", AccountErrorCodes.EmailAlreadyExists);

                var staffRole = await _db.Roles.FirstOrDefaultAsync(
                    r => r.RoleCode == RoleCodes.Staff && r.Status == EntityStatuses.Active, cancellationToken)
                    ?? throw new ValidationException("Role 'STAFF' is not valid or not active.");

                newLeader = new User
                {
                    FullName = (request.FullName ?? string.Empty).Trim(),
                    Email = email,
                    Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone!.Trim(),
                    Gender = request.Gender,
                    RoleId = staffRole.RoleId,
                    SubRole = UserSubRoles.Leader,
                    PrimaryCampusId = request.CampusId,
                    DepartmentId = icDepartmentId,
                    Status = UserStatuses.Active,
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
                    eventType: SecurityEventTypes.StaffLeaderReplacedWhileLocked,
                    result: "SUCCESS",
                    selectedCampusId: request.CampusId,
                    detailText: $"Replaced locked Staff Leader of campus {request.CampusId}. Reason: {reason}",
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
            newLeader, avail.CampusName, avail.IcDepartmentName, reason, cancellationToken);
        await SendOldLeaderEmailAsync(oldLeaderEmail, oldLeaderName, avail.CampusName, reason, cancellationToken);

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
        User newLeader, string? campusName, string? departmentName, string reason, CancellationToken cancellationToken)
    {
        var name = System.Net.WebUtility.HtmlEncode(newLeader.FullName);
        var emailEnc = System.Net.WebUtility.HtmlEncode(newLeader.Email);
        var campusEnc = System.Net.WebUtility.HtmlEncode(campusName ?? "—");
        var deptEnc = System.Net.WebUtility.HtmlEncode(departmentName ?? "Phòng Hợp tác Quốc tế");
        var reasonEnc = System.Net.WebUtility.HtmlEncode(reason);

        var html =
            $"<p>Xin chào {name},</p>" +
            $"<p>Bạn đã được phân công làm <strong>Trưởng phòng Hợp tác Quốc tế</strong> của {campusEnc} trên hệ thống PEMS.</p>" +
            "<p><strong>Thông tin tài khoản:</strong></p>" +
            "<ul>" +
            $"<li>Email đăng nhập: <strong>{emailEnc}</strong></li>" +
            "<li>Vai trò: <strong>Staff Leader — Trưởng phòng IC</strong></li>" +
            $"<li>Cơ sở: <strong>{campusEnc}</strong></li>" +
            $"<li>Phòng ban: <strong>{deptEnc}</strong></li>" +
            "</ul>" +
            $"<p><strong>Lý do thay thế:</strong> {reasonEnc}</p>" +
            "<p>Vui lòng đăng nhập Internal Portal bằng email trên thông qua SSO/Google/FEID.</p>" +
            "<p>Trân trọng,<br/>PEMS System</p>";

        try
        {
            await _emailService.SendAsync(
                newLeader.Email, "[PEMS] Bạn đã được phân công làm Trưởng phòng IC", html, cancellationToken);
            return "SENT";
        }
        catch
        {
            return "FAILED";
        }
    }

    private async Task SendOldLeaderEmailAsync(
        string email, string fullName, string? campusName, string reason, CancellationToken cancellationToken)
    {
        var name = System.Net.WebUtility.HtmlEncode(fullName);
        var campusEnc = System.Net.WebUtility.HtmlEncode(campusName ?? "—");
        var reasonEnc = System.Net.WebUtility.HtmlEncode(reason);

        var html =
            $"<p>Xin chào {name},</p>" +
            $"<p>Vai trò Trưởng phòng Hợp tác Quốc tế của bạn tại {campusEnc} đã được cập nhật.</p>" +
            "<p>Bạn không còn là Staff Leader của cơ sở này trên hệ thống PEMS. " +
            "Tài khoản của bạn hiện được chuyển về vai trò Staff thuộc Phòng Hợp tác Quốc tế.</p>" +
            $"<p><strong>Lý do thay thế:</strong> {reasonEnc}</p>" +
            "<p>Nếu thông tin này chưa chính xác, vui lòng liên hệ HO hoặc quản trị hệ thống.</p>" +
            "<p>Trân trọng,<br/>PEMS System</p>";

        try
        {
            await _emailService.SendAsync(
                email, "[PEMS] Vai trò Trưởng phòng IC của bạn đã được thay đổi", html, cancellationToken);
        }
        catch
        {
            // Replace already committed; a failed notification must not fail the request.
        }
    }
}
