using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Shared "resolve to the canonical Vietnamese short name or reject" used by every visit-request
/// write path that persists a NEW-OR-CHANGED nationality value: create (registrant + members) and
/// copy-on-write member replacement (pending-edit / resubmit / instance-edit / instance-resubmit /
/// amendment approval, all of which stage new <c>VisitGuestMember</c> rows through
/// <see cref="VisitRequestV2EditOps.StageReplaceMembers"/>).
///
/// <para>
/// Registrant nationality on an EDIT is handled separately, in
/// <c>VisitRequestV2EditService.ApplyCommonFields</c> and <c>VisitSafeEditService</c>, because only
/// those paths need to compare the incoming value against what is ALREADY stored first: an edit that
/// never touched nationality must not be blocked just because the legacy value it echoes back happens
/// not to resolve.
/// </para>
///
/// <para>
/// A member row has the same "unrelated edit" protection, but earns it a different way (Patch 4
/// hardening H4-3): a campus's member set is copy-on-write, so THE WHOLE SET is rewritten the moment
/// any campus field changes, not just nationality. <see cref="MemberContentIndex"/> is what carves the
/// "unrelated" members back out of that wholesale rewrite — a row whose full content (not just its
/// nationality) already existed on the campus is exempt from this resolver entirely; only a row that is
/// genuinely new-or-changed content reaches <see cref="ResolveOrThrow"/>. The same check runs a second
/// time, earlier, in <c>VisitAmendmentService.SubmitAsync</c> (H4-4) — so a proposal containing a
/// genuinely new unresolvable nationality is rejected at SUBMIT, before a pending amendment nobody
/// could approve is even created, rather than only failing once a Staff Leader tries to approve it.
/// </para>
/// </summary>
internal static class NationalityResolution
{
    public static string ResolveOrThrow(string input, string fieldMessage)
    {
        if (!CountryName.TryResolve(input, out var canonical))
            throw new BusinessRuleException(
                $"{fieldMessage} '{(input ?? string.Empty).Trim()}'. {CountryName.FormatHint}",
                VisitRequestErrorCodes.InvalidNationality);
        return canonical!;
    }
}
