using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>One reason a status change cannot proceed. Ids and counts only — never PII.</summary>
public sealed class DepartmentPersonnelStatusBlocker
{
    public required string Code { get; init; }
    public int Count { get; init; }
    public required string Message { get; init; }
}

/// <summary>Non-fatal consequence the operator should see before confirming.</summary>
public sealed class DepartmentPersonnelStatusWarning
{
    public required string Code { get; init; }
    public int Count { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Everything the confirmation modal needs, and the exact verdict the command re-uses.
/// <see cref="CanChangeStatus"/> is the single gate: a non-empty blocker list is the only reason it is
/// false, so the preview endpoint and the write endpoint can never disagree.
/// </summary>
public sealed class DepartmentPersonnelStatusImpact
{
    public required ulong UserId { get; init; }
    public required string CurrentStatus { get; init; }
    public required string TargetStatus { get; init; }
    public int ActiveSessionCount { get; init; }
    public IReadOnlyList<DepartmentPersonnelStatusBlocker> Blockers { get; init; }
        = Array.Empty<DepartmentPersonnelStatusBlocker>();
    public IReadOnlyList<DepartmentPersonnelStatusWarning> Warnings { get; init; }
        = Array.Empty<DepartmentPersonnelStatusWarning>();

    public bool CanChangeStatus => Blockers.Count == 0;
}

/// <summary>
/// The enable/disable rulebook for department personnel (spec §14/§15), shared by the impact preview
/// and the write command so a toggle can never take a path the preview did not evaluate.
///
/// Only <c>ACTIVE ↔ INACTIVE</c> exists here. PENDING_EMAIL_CONFIRMATION activates solely by
/// confirming the email (activating it here would bypass the ownership proof) and LOCKED is a security
/// state that needs its own unlock flow — neither is reachable through this toggle.
/// </summary>
public static class DepartmentPersonnelStatusRules
{
    /// <summary>The only two values <c>targetStatus</c> may take.</summary>
    public static bool IsSupportedTargetStatus(string? targetStatus)
        => targetStatus is UserStatuses.Active or UserStatuses.Inactive;

    /// <summary>
    /// Evaluates a requested transition end-to-end. Read-only by construction: a refused change must
    /// leave the database exactly as it found it.
    /// </summary>
    /// <param name="target">The in-scope personnel row (already verified by the scope service).</param>
    /// <param name="department">The caller's department, used for the enable-side department/campus gate.</param>
    public static async Task<DepartmentPersonnelStatusImpact> EvaluateAsync(
        IApplicationDbContext db,
        DepartmentLeaderScope scope,
        User target,
        string targetStatus,
        string departmentStatus,
        string campusStatus,
        ulong? departmentHeadUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var blockers = new List<DepartmentPersonnelStatusBlocker>();
        var warnings = new List<DepartmentPersonnelStatusWarning>();

        var activeSessionCount = await db.UserSessions.AsNoTracking()
            .CountAsync(
                s => s.UserId == target.UserId && s.RevokedAt == null && s.ExpiresAt > now,
                cancellationToken);

        // ── Transition shape ──────────────────────────────────────────────────
        if (target.Status == targetStatus)
        {
            blockers.Add(new DepartmentPersonnelStatusBlocker
            {
                Code = DepartmentLeaderErrorCodes.PersonnelInvalidStatus,
                Message = "Nhân sự đã ở trạng thái này.",
            });
        }
        else if (targetStatus == UserStatuses.Inactive)
        {
            AddDisableBlockers(scope, target, departmentHeadUserId, blockers);
        }
        else // targetStatus == ACTIVE
        {
            AddEnableBlockers(target, departmentStatus, campusStatus, blockers);
        }

        // ── Active responsibilities. Only relevant when taking access AWAY: re-enabling an account
        //    never invalidates a duty. Reuses the shared checker (the same one that guards role
        //    changes) with an unchanged role shape, so the department-head group is skipped there and
        //    handled explicitly above as CURRENT_LEADER_DISABLE_FORBIDDEN. ──
        if (targetStatus == UserStatuses.Inactive && target.Status == UserStatuses.Active)
        {
            var impact = await AccountRoleChangeDependencyChecker.CheckAsync(
                db,
                target.UserId,
                RoleCodes.Department, target.SubRole, target.DepartmentId,
                RoleCodes.Department, target.SubRole, target.DepartmentId,
                cancellationToken);

            if (!impact.CanChangeRole)
            {
                var totalCount = impact.Blockers.Sum(b => b.Count);
                blockers.Add(new DepartmentPersonnelStatusBlocker
                {
                    Code = DepartmentLeaderErrorCodes.PersonnelHasActiveResponsibilities,
                    Count = totalCount,
                    Message = $"Nhân sự đang có {totalCount} nhiệm vụ chưa hoàn thành. "
                              + "Vui lòng bàn giao hoặc kết thúc các nhiệm vụ này trước khi vô hiệu hóa tài khoản.",
                });
            }
        }

        // ── Warnings: things the operator should know, but which do not stop the change. ──
        if (targetStatus == UserStatuses.Inactive && activeSessionCount > 0)
        {
            warnings.Add(new DepartmentPersonnelStatusWarning
            {
                Code = "ACTIVE_SESSIONS_WILL_BE_REVOKED",
                Count = activeSessionCount,
                Message = $"Nhân sự đang có {activeSessionCount} phiên đăng nhập sẽ bị thu hồi ngay lập tức.",
            });
        }

        if (targetStatus == UserStatuses.Active)
        {
            warnings.Add(new DepartmentPersonnelStatusWarning
            {
                Code = "MUST_SIGN_IN_AGAIN",
                Message = "Nhân sự cần đăng nhập lại; hệ thống không tự khôi phục phiên trước đó.",
            });
        }

        return new DepartmentPersonnelStatusImpact
        {
            UserId = target.UserId,
            CurrentStatus = target.Status,
            TargetStatus = targetStatus,
            ActiveSessionCount = activeSessionCount,
            Blockers = blockers,
            Warnings = warnings,
        };
    }

    private static void AddDisableBlockers(
        DepartmentLeaderScope scope,
        User target,
        ulong? departmentHeadUserId,
        List<DepartmentPersonnelStatusBlocker> blockers)
    {
        // Self-disable would lock the department out of its own management screen.
        if (target.UserId == scope.ActorUserId)
        {
            blockers.Add(new DepartmentPersonnelStatusBlocker
            {
                Code = DepartmentLeaderErrorCodes.PersonnelSelfDisableForbidden,
                Message = "Bạn không thể vô hiệu hóa tài khoản của chính mình.",
            });
        }

        // Disabling the seated head would leave the department without one — hand leadership over first.
        if (departmentHeadUserId is not null && target.UserId == departmentHeadUserId)
        {
            blockers.Add(new DepartmentPersonnelStatusBlocker
            {
                Code = DepartmentLeaderErrorCodes.CurrentLeaderDisableForbidden,
                Message = "Không thể vô hiệu hóa Trưởng phòng đương nhiệm. Vui lòng đổi trưởng phòng trước.",
            });
        }

        // Only an ACTIVE account can be deactivated. PENDING has never been active and LOCKED is
        // already denied access by a stricter, security-owned state.
        if (target.Status != UserStatuses.Active)
        {
            blockers.Add(new DepartmentPersonnelStatusBlocker
            {
                Code = target.Status switch
                {
                    UserStatuses.PendingEmailConfirmation => DepartmentLeaderErrorCodes.PersonnelEmailConfirmationPending,
                    UserStatuses.Locked => DepartmentLeaderErrorCodes.PersonnelSecurityLocked,
                    _ => DepartmentLeaderErrorCodes.PersonnelInvalidStatus,
                },
                Message = target.Status switch
                {
                    UserStatuses.PendingEmailConfirmation =>
                        "Tài khoản đang chờ xác nhận email nên chưa thể vô hiệu hóa.",
                    UserStatuses.Locked =>
                        "Tài khoản đang bị khóa vì lý do bảo mật và không thể thay đổi trạng thái tại đây.",
                    _ => "Chỉ có thể vô hiệu hóa tài khoản đang hoạt động.",
                },
            });
        }
    }

    private static void AddEnableBlockers(
        User target,
        string departmentStatus,
        string campusStatus,
        List<DepartmentPersonnelStatusBlocker> blockers)
    {
        // Enable is INACTIVE → ACTIVE only. PENDING and LOCKED are refused with their own codes so the
        // UI can explain the correct path (confirm the email / contact security) instead of a generic
        // "invalid status".
        switch (target.Status)
        {
            case UserStatuses.Inactive:
                break;

            case UserStatuses.PendingEmailConfirmation:
                blockers.Add(new DepartmentPersonnelStatusBlocker
                {
                    Code = DepartmentLeaderErrorCodes.PersonnelEmailConfirmationPending,
                    Message = "Tài khoản chỉ được kích hoạt bằng cách xác nhận email. "
                              + "Vui lòng dùng chức năng gửi lại email xác nhận.",
                });
                break;

            case UserStatuses.Locked:
                blockers.Add(new DepartmentPersonnelStatusBlocker
                {
                    Code = DepartmentLeaderErrorCodes.PersonnelSecurityLocked,
                    Message = "Tài khoản đang bị khóa vì lý do bảo mật và phải được mở khóa qua quy trình riêng.",
                });
                break;

            default:
                blockers.Add(new DepartmentPersonnelStatusBlocker
                {
                    Code = DepartmentLeaderErrorCodes.PersonnelInvalidStatus,
                    Message = "Chỉ có thể kích hoạt lại tài khoản đang bị vô hiệu hóa.",
                });
                break;
        }

        // A member cannot be brought back online into a department or campus that is itself offline.
        if (departmentStatus != EntityStatuses.Active)
        {
            blockers.Add(new DepartmentPersonnelStatusBlocker
            {
                Code = DepartmentLeaderErrorCodes.DepartmentNotActive,
                Message = "Phòng ban đang ngừng hoạt động nên không thể kích hoạt nhân sự.",
            });
        }

        if (campusStatus != EntityStatuses.Active)
        {
            blockers.Add(new DepartmentPersonnelStatusBlocker
            {
                Code = DepartmentLeaderErrorCodes.DepartmentNotActive,
                Message = "Cơ sở đang ngừng hoạt động nên không thể kích hoạt nhân sự.",
            });
        }
    }
}
