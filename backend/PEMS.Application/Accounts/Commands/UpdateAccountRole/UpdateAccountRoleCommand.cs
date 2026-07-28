using MediatR;

namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

/// <summary>
/// UC-100 Update Account Role. Typical use: a Staff Leader promotes a VISITOR to an
/// internal role for their campus. Campus/department are (re)assigned per role rules,
/// existing auth providers are kept, and all active sessions are revoked so the user
/// re-authenticates with the new role.
/// </summary>
public sealed class UpdateAccountRoleCommand : IRequest<UpdateAccountRoleResponse>
{
    public ulong UserId { get; set; }
    public string NewRoleCode { get; set; } = string.Empty;  // ADMIN | HO | STAFF | DEPARTMENT | STUDENT | VISITOR
    public string? SubRole { get; set; }                     // Leader | Staff (STAFF/DEPARTMENT only)
    public ulong? PrimaryCampusId { get; set; }
    public ulong? DepartmentId { get; set; }
    public string? StudentCode { get; set; }                 // MSSV — required for STUDENT (Staff Leader flow)

    // ── Identity fields (Staff Leader flow) ──────────────────────────────────────────────────────
    // Optional so legacy role-only requests stay compatible. Only honoured when the target's
    // ORIGINAL role/sub-role permits identity edits (STAFF/STAFF, DEPARTMENT/LEADER, STUDENT); the
    // handler re-derives that from the database and rejects an unauthorized change (never silently
    // ignores it). A null value means "leave unchanged".
    public string? FullName { get; set; }
    public string? Email { get; set; }

    // ── Department-head handover (Staff Leader flow) ─────────────────────────────────────────────
    // Set when the target currently heads a GENERAL department AND this change moves them out of
    // that seat. The handover runs in the SAME transaction as the role change, so the account never
    // passes through an in-between state that nobody is allowed to manage: without it the target
    // would be demoted to DEPARTMENT/STAFF by the separate reassign-lead flow and fall outside the
    // Staff Leader's manageable set (spec §3.3), leaving the role change unreachable.
    // Null when the target heads nothing — supplying it anyway is an error, never ignored.
    public ulong? ReplacementDepartmentHeadUserId { get; set; }
}
