namespace PEMS.Domain.Constants;

/// <summary>PEMS v10: enums for email_action_tokens.</summary>
public static class EmailActionContexts
{
    public const string ParticipationResponse = "PARTICIPATION_RESPONSE";

    // Department Leader delegates a visit participation to a Department Staff member (spec BUG-02).
    // Distinct from ParticipationResponse because the valid starting participant status is the
    // opposite: this context is only ever minted once the row is already ASSIGNED, and must accept
    // exactly that status, while a direct invitation only ever starts at INVITED. Keeping them as two
    // contexts is what lets the renderer show "nhận nhiệm vụ" instead of "lời mời" wording, and lets
    // the token-validity ladder reject an ASSIGNED row for one context and accept it for the other.
    public const string ParticipationAssignmentResponse = "PARTICIPATION_ASSIGNMENT_RESPONSE";

    public const string LogisticsRequestResponse = "LOGISTICS_REQUEST_RESPONSE";
    public const string LogisticsAssigneeResponse = "LOGISTICS_ASSIGNEE_RESPONSE";
    public const string LogisticsNegotiation = "LOGISTICS_NEGOTIATION";
    public const string LogisticsProposalResponse = "LOGISTICS_PROPOSAL_RESPONSE";
    public const string LogisticsHandoverSignature = "LOGISTICS_HANDOVER_SIGNATURE";

    // Per-campus form v2 identity claim/transfer (plan §4.4). Deliberately NOT in All: the generic
    // anonymous email-action handler must REJECT these contexts — accepting a contact claim or a
    // 24h contact transfer requires an authenticated session whose email matches the invitation
    // (dedicated v2 handlers).
    public const string VisitContactClaim = "VISIT_CONTACT_CLAIM";
    public const string VisitContactTransfer = "VISIT_CONTACT_TRANSFER";

    public static readonly string[] All =
    {
        ParticipationResponse, ParticipationAssignmentResponse, LogisticsRequestResponse,
        LogisticsAssigneeResponse, LogisticsNegotiation, LogisticsProposalResponse,
        LogisticsHandoverSignature,
    };
}

public static class EmailActionTargetTypes
{
    public const string VisitParticipant = "VISIT_PARTICIPANT";
    public const string LogisticsItem = "LOGISTICS_ITEM";
    public const string LogisticsHandover = "LOGISTICS_HANDOVER";
    // v2 identity claim (kept out of All — see EmailActionContexts.VisitContactClaim).
    public const string VisitRequestIdentityChange = "VISIT_REQUEST_IDENTITY_CHANGE";

    public static readonly string[] All = { VisitParticipant, LogisticsItem, LogisticsHandover };
}

public static class EmailIntendedActions
{
    public const string Accept = "ACCEPT";
    public const string Decline = "DECLINE";
    public const string Negotiate = "NEGOTIATE";
    public const string ApproveProposal = "APPROVE_PROPOSAL";
    public const string RejectProposal = "REJECT_PROPOSAL";
    public const string ConfirmBorrow = "CONFIRM_BORROW";
    public const string ConfirmReturn = "CONFIRM_RETURN";

    public static readonly string[] All =
    {
        Accept, Decline, Negotiate, ApproveProposal, RejectProposal, ConfirmBorrow, ConfirmReturn,
    };
}

public static class EmailActionResultStatuses
{
    public const string Pending = "PENDING";
    public const string Success = "SUCCESS";
    public const string AlreadyResponded = "ALREADY_RESPONDED";
    public const string Expired = "EXPIRED";
    public const string Invalid = "INVALID";
    public const string Failed = "FAILED";

    public static readonly string[] All =
    {
        Pending, Success, AlreadyResponded, Expired, Invalid, Failed,
    };
}
