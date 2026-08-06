using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// One resolved reception-host arrangement for ONE campus. <see cref="CampusCode"/> is UPPER-cased;
/// <see cref="Mode"/> is a <see cref="HostSelectionModes"/> value. <see cref="ProposedHostUserId"/> is
/// non-null exactly when the mode is SELF or SELECTED.
/// </summary>
public sealed record V2HostProposal(
    string CampusCode, string Mode, ulong? ProposedHostUserId, bool ConfirmedHostConflict);

/// <summary>
/// Actor facts (revalidated from the authenticated session + DB) needed to authorize a proposal,
/// captured as plain data so the role × mode × campus matrix is unit-testable without a database.
/// </summary>
public sealed record V2ProposalActor(
    bool IsVisitor,
    bool IsRegularStaff,
    bool IsStaffLeader,
    string? OwnCampusCode,
    bool OwnDepartmentIsIc,
    ulong ActorUserId);

/// <summary>
/// What the caller may do about the reception host on ONE campus. Returned to the client so the
/// frontend renders from a backend verdict instead of re-deriving permission from a role string —
/// and re-checked in every handler, because a capability is a hint, never a token.
/// </summary>
public sealed record V2HostProposalCapabilities(
    bool CanProposeSelfAsHost,
    bool CanProposeOtherHost,
    bool CanWaitForLaterAssignment,
    bool CanUpdateProposedHost);

/// <summary>
/// Who may propose whom as a campus's reception host, and in which mode.
///
/// <para>
/// This REPLACES the old direct-processing matrix. The difference is not cosmetic: direct processing
/// named a <c>current_host_user_id</c> inside the create transaction, which handed a campus to an
/// FPTU staff member before any guest-side contact had confirmed they were coming — the exact thing
/// the confirmation gate exists to prevent. Everything decided here is a PROPOSAL; it becomes an
/// assignment only when <c>ProposedHostActivationService</c> revalidates it at the moment the gate
/// opens.
/// </para>
///
/// <list type="bullet">
///   <item>Visitor / any non-internal actor → WAIT_FOR_LATER only; a payload proposing anybody is refused.</item>
///   <item>Regular IC Staff → may propose THEMSELF on their OWN campus, or wait.</item>
///   <item>Staff Leader → may propose themself or a same-campus IC Staff on their OWN campus, or wait.</item>
/// </list>
///
/// <para>
/// Every other campus always waits for that campus's own Staff Leader. The DB-only "is this a valid
/// host candidate" check belongs to the handler; everything here is pure.
/// </para>
/// </summary>
public static class V2HostProposalRules
{
    /// <summary>
    /// What this actor may do about the host of a campus with the given code. Used to fill the
    /// create-form capabilities and to answer the read model; the authorization decisions below do
    /// not depend on it.
    /// </summary>
    public static V2HostProposalCapabilities CapabilitiesFor(V2ProposalActor actor, string campusCode)
    {
        var ownCode = actor.OwnCampusCode?.Trim().ToUpperInvariant();
        var isOwnCampus = ownCode != null
            && string.Equals(campusCode?.Trim().ToUpperInvariant(), ownCode, StringComparison.OrdinalIgnoreCase);

        var isInternal = !actor.IsVisitor && (actor.IsRegularStaff || actor.IsStaffLeader);
        if (!isInternal || !isOwnCampus)
            return new V2HostProposalCapabilities(false, false, CanWaitForLaterAssignment: true, false);

        // A regular Staff may only take the campus on themself, and only from the IC department that
        // actually runs receptions there.
        var canSelf = actor.IsStaffLeader || (actor.IsRegularStaff && actor.OwnDepartmentIsIc);

        return new V2HostProposalCapabilities(
            CanProposeSelfAsHost: canSelf,
            CanProposeOtherHost: actor.IsStaffLeader,
            CanWaitForLaterAssignment: true,
            CanUpdateProposedHost: canSelf || actor.IsStaffLeader);
    }

    /// <summary>
    /// Reads one arrangement per campus off the create form. Every campus gets an entry — a campus
    /// that said nothing means WAIT_FOR_LATER, which is a real answer and not an absence. Any
    /// arrangement must reference one of the selected campuses.
    /// </summary>
    public static IReadOnlyList<V2HostProposal> BuildProposals(VisitRequestFormDataV2 form)
    {
        var selected = form.CampusVisits
            .Select(c => c.CampusId?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var proposals = new List<V2HostProposal>();
        foreach (var cv in form.CampusVisits)
        {
            var code = cv.CampusId?.Trim().ToUpperInvariant() ?? string.Empty;
            var selection = cv.HostSelection;

            var mode = string.IsNullOrWhiteSpace(selection?.Mode)
                ? HostSelectionModes.WaitForLater
                : selection!.Mode!.Trim().ToUpperInvariant();

            if (!HostSelectionModes.IsKnown(mode))
                throw new BusinessRuleException(
                    "Phương án người phụ trách tiếp đón không hợp lệ.",
                    VisitRequestErrorCodes.InvalidHostSelectionMode);

            if (selection is not null && !selected.Contains(code))
                throw new BusinessRuleException(
                    "Lựa chọn người phụ trách tiếp đón tham chiếu cơ sở không nằm trong danh sách cơ sở đã chọn.",
                    VisitRequestErrorCodes.HostSelectionCampusNotSelected);

            // The host id is carried through UNCHANGED, including on WAIT_FOR_LATER. Dropping it
            // here would turn "assign later, and by the way it is user 9" into a clean payload, and
            // the contradiction Authorize exists to catch would never reach it.
            proposals.Add(new V2HostProposal(
                code, mode, selection?.ProposedHostUserId,
                selection?.ConfirmedHostConflict ?? false));
        }
        return proposals;
    }

    /// <summary>
    /// Pure role × mode × campus authorization, and the point where SELF is resolved to the caller's
    /// own id. Returns the arrangement the backend will persist — the client only ever states an
    /// intent, so a SELF that named somebody else is refused rather than quietly honoured.
    /// </summary>
    public static IReadOnlyList<V2HostProposal> Authorize(
        V2ProposalActor actor, IReadOnlyList<V2HostProposal> proposals)
    {
        var ownCode = actor.OwnCampusCode?.Trim().ToUpperInvariant();
        var isInternal = !actor.IsVisitor && (actor.IsRegularStaff || actor.IsStaffLeader);
        var authorized = new List<V2HostProposal>(proposals.Count);

        foreach (var proposal in proposals)
        {
            if (proposal.Mode == HostSelectionModes.WaitForLater)
            {
                // Nothing to authorize, but a stray host id on a "wait" campus is a contradiction the
                // client should never send — accepting it silently would persist a proposal the user
                // did not make.
                if (proposal.ProposedHostUserId is not null)
                    throw new BusinessRuleException(
                        "Chờ phân công sau thì không được kèm người phụ trách dự kiến.",
                        VisitRequestErrorCodes.InvalidHostSelectionMode);

                authorized.Add(proposal with { ProposedHostUserId = null });
                continue;
            }

            // From here the payload proposes somebody. Only internal staff may.
            if (!isInternal)
                throw new BusinessRuleException(
                    "Đơn của khách luôn chờ Staff Leader từng cơ sở phân công người phụ trách tiếp đón.",
                    VisitRequestErrorCodes.ProposedHostNotAllowedForRole);

            var isOwnCampus = ownCode != null
                && string.Equals(proposal.CampusCode, ownCode, StringComparison.OrdinalIgnoreCase);
            if (!isOwnCampus)
                throw new ForbiddenException(
                    "Bạn chỉ được đề xuất người phụ trách cho cơ sở của chính mình; cơ sở khác do Staff Leader cơ sở đó phân công.",
                    VisitRequestErrorCodes.ProposeHostOtherCampusForbidden);

            switch (proposal.Mode)
            {
                case HostSelectionModes.Selected:
                    if (!actor.IsStaffLeader)
                        throw new ForbiddenException(
                            "Chỉ Staff Leader mới được đề xuất người khác làm người phụ trách tiếp đón.",
                            VisitRequestErrorCodes.StaffCannotAssignOtherHost);
                    if (proposal.ProposedHostUserId is null)
                        throw new BusinessRuleException(
                            "Chọn người phụ trách khác thì phải chọn cụ thể một người.",
                            VisitRequestErrorCodes.InvalidHostCandidate);
                    authorized.Add(proposal);
                    break;

                case HostSelectionModes.Self:
                    // SELF is resolved from the session, never from the payload. A client that names
                    // somebody else is not making a typo — it is trying to assign, under a mode that
                    // does not require Leader rights.
                    if (proposal.ProposedHostUserId.HasValue
                        && proposal.ProposedHostUserId.Value != actor.ActorUserId)
                        throw new ForbiddenException(
                            "Chế độ tự nhận không được đề xuất người khác.",
                            VisitRequestErrorCodes.StaffCannotAssignOtherHost);
                    if (actor.IsRegularStaff && !actor.OwnDepartmentIsIc)
                        throw new BusinessRuleException(
                            "Chỉ IC Staff thuộc phòng IC của cơ sở mới được tự nhận người phụ trách tiếp đón.",
                            VisitRequestErrorCodes.SelfHostNotEligible);
                    authorized.Add(proposal with { ProposedHostUserId = actor.ActorUserId });
                    break;

                default:
                    throw new BusinessRuleException(
                        "Phương án người phụ trách tiếp đón không hợp lệ.",
                        VisitRequestErrorCodes.InvalidHostSelectionMode);
            }
        }

        return authorized;
    }

    /// <summary>
    /// The actor role recorded when this proposal is eventually activated. A Leader who proposed
    /// themself and a Leader who picked an IC Staff both decide as STAFF_LEADER; a regular IC Staff
    /// proposing themself is the single STAFF case the DB allows, and only on their own request.
    /// </summary>
    public static string DecisionActorRoleFor(V2ProposalActor actor)
        => actor.IsStaffLeader ? DecisionActorRole.StaffLeader : DecisionActorRole.Staff;
}
