using System.Linq;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// The ONE place that answers "is the person named as registrant on this form the authenticated caller?".
///
/// Both authenticated write paths depend on the answer and must never disagree:
///   • create-v2 (direct, no OTP)   → allowed ONLY when the answer is yes;
///   • initiate/verify-v2 (OTP)     → the delegated path, where the answer is no and the OTP proves the
///                                    entered registrant really owns that mailbox.
///
/// Normalisation is deliberately just trim + lower-case (plan §5.2). Gmail dot/+alias folding and domain
/// rewriting are NOT applied: two addresses that differ by a dot are different mailboxes as far as identity
/// is concerned, and silently folding them would let one account claim another's identity.
/// </summary>
public static class RegistrantIdentityRules
{
    /// <summary>trim + lower-case. Mirrors <c>normalizeEmail()</c> on the frontend.</summary>
    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// True only when both addresses are present AND equal after normalisation. Two blanks are NOT the same
    /// identity — an actor with no email on record must never match a blank form field.
    /// </summary>
    public static bool IsSameIdentity(string? left, string? right)
    {
        var a = Normalize(left);
        return a.Length > 0 && a == Normalize(right);
    }

    /// <summary>
    /// Guards the DIRECT authenticated create: the registrant snapshot must be the caller's own identity.
    /// A mismatch is a routing decision, not a validation typo — the caller has to go through the delegated
    /// OTP flow so the named registrant proves ownership of the mailbox. Nothing is written on this path.
    /// </summary>
    public static void EnsureDirectCreateIsSelfRegistration(string? actorEmail, string? formRegistrantEmail)
    {
        if (IsSameIdentity(actorEmail, formRegistrantEmail)) return;

        throw new ConflictException(
            "Email người đăng ký khác với email tài khoản đang đăng nhập. "
            + "Vui lòng xác minh email người đăng ký bằng mã OTP trước khi gửi đơn.",
            VisitRequestErrorCodes.RegistrantEmailVerificationRequired);
    }

    /// <summary>
    /// Guards every NON-self submission (public visitor submit and the authenticated delegated OTP flow):
    /// a form that is not self-registration may not name a reception host. Proposing a host is a right
    /// that comes from being internal staff of that campus, and the person these payloads register on
    /// behalf of is neither. It is REJECTED rather than silently dropped, so a forged payload is
    /// distinguishable from a clean one and the client learns the intent did not apply.
    /// </summary>
    public static void EnsureNoHostProposalIntent(VisitRequestFormDataV2 form)
    {
        var carriesProposal = form.CampusVisits.Any(cv =>
        {
            var mode = cv.HostSelection?.Mode?.Trim().ToUpperInvariant();
            return cv.HostSelection?.ProposedHostUserId is not null
                || (!string.IsNullOrEmpty(mode) && mode != HostSelectionModes.WaitForLater);
        });

        if (!carriesProposal) return;

        throw new BusinessRuleException(
            "Đơn tạo hộ người khác luôn chờ Staff Leader của từng cơ sở phân công người phụ trách tiếp đón.",
            VisitRequestErrorCodes.ProposedHostNotAllowedForRole);
    }
}
