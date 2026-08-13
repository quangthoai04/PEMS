using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// WHO is allowed to be a campus's operational contact — the guest-side person who coordinates the
/// visit with FPTU.
///
/// <para>
/// The role is EXTERNAL by definition. It exists so somebody outside FPTU confirms, in their own name,
/// that they will bring this delegation: that confirmation is what the whole contact gate is waiting
/// for, and an FPTU account answering it confirms nothing — the university would be assuring itself
/// that a guest is coming. So no internal account (ADMIN, HO, STAFF, DEPARTMENT, STUDENT) may hold it,
/// whatever else that person is to the request.
/// </para>
/// <para>
/// "Whatever else" is the part that needs saying out loud, because every one of these was a way in
/// before: being the registrant, having an address that matches the registrant's, leading the campus,
/// being a Staff Leader, or simply POSTing the address straight at the API. None of them is an
/// exception — the check is on the ACCOUNT behind the address, and it runs on every door where an
/// address is named and every door where an account claims the role.
/// </para>
/// <para>
/// An address with no account at all is fine: nobody has been repurposed, and the invitation flow
/// provisions the same VISITOR account the public form would create. That is why this asks "is there
/// an internal account here", never "does an account exist".
/// </para>
/// </summary>
public static class OperationalContactEligibility
{
    /// <summary>
    /// The one role test. VISITOR is the only external role, and the check is written that way round
    /// on purpose: a role added to the system later is internal until somebody decides otherwise, so
    /// the failure mode of forgetting to update this is a refusal rather than a silent grant.
    /// </summary>
    public static bool IsExternalRole(string? roleCode)
        => string.Equals(roleCode, RoleCodes.Visitor, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The address a caller wants to make a campus's operational contact. Called wherever an address is
    /// NAMED — create, replace, transfer, re-invite — before an invitation is written or a link is made.
    ///
    /// <para>
    /// A refusal here means no invitation row, no email, no link: the caller is asked for a different
    /// address instead. The wording never says WHICH kind of account owns the address — that would turn
    /// the contact field into a directory lookup for the internal staff list.
    /// </para>
    /// </summary>
    public static async Task EnsureEmailMayHoldContactRoleAsync(
        IApplicationDbContext db, string? email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        var normalized = email.Trim().ToLowerInvariant();
        var account = await db.Users.AsNoTracking()
            .Where(u => u.Email == normalized)
            .Select(u => new { RoleCode = u.Role.RoleCode, u.Status })
            .FirstOrDefaultAsync(ct);

        // No account yet — nothing to repurpose, and the invitation will provision a VISITOR.
        if (account is null) return;

        EnsureRoleIsExternal(account.RoleCode);
        EnsureAccountActive(account.Status);
    }

    /// <summary>
    /// The account ANSWERING an invitation, at the moment it would take the role. Distinct from the
    /// address check above and not redundant with it: an address can be clean when the invitation is
    /// written and belong to an internal account by the time it is answered — a guest's address added
    /// to the staff directory, an account whose role was changed — and this is the last point before
    /// the campus is handed over.
    /// </summary>
    public static void EnsureAccountMayHoldContactRole(string? roleCode, string? status)
    {
        EnsureRoleIsExternal(roleCode, OperationalContactErrorCodes.MustBeExternal, AccountIsInternalMessage);
        EnsureAccountActive(status, OperationalContactErrorCodes.AccountInactive);
    }

    /// <summary>Told to the person holding the internal account, so it explains rather than deflects.</summary>
    private const string AccountIsInternalMessage =
        "Tài khoản nội bộ FPTU không thể nhận vai trò đầu mối của đoàn. "
        + "Vai trò này dành cho khách hoặc đối tác bên ngoài; vui lòng đề nghị người đăng ký mời một email bên ngoài.";

    private static void EnsureRoleIsExternal(
        string? roleCode, string? errorCode = null, string? message = null)
    {
        if (IsExternalRole(roleCode)) return;

        throw new ConflictException(
            message ?? VisitRequestErrorMessages.ContactEmailNotEligible,
            errorCode ?? VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount);
    }

    /// <summary>
    /// A deactivated VISITOR is a different answer from an internal one, and worth telling apart: the
    /// address is the right KIND of account, it has simply been disabled, so the way forward is support
    /// rather than a different address.
    /// </summary>
    private static void EnsureAccountActive(string? status, string? errorCode = null)
    {
        if (string.Equals(status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase)) return;

        throw new BusinessRuleException(
            VisitRequestErrorMessages.AccountInactive,
            errorCode ?? VisitRequestErrorCodes.VisitorAccountInactive);
    }
}
