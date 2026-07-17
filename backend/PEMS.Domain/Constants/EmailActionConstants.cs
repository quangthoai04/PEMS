namespace PEMS.Domain.Constants;

/// <summary>PEMS v10: enums for email_action_tokens.</summary>
public static class EmailActionContexts
{
    public const string ParticipationResponse = "PARTICIPATION_RESPONSE";
    public const string LogisticsRequestResponse = "LOGISTICS_REQUEST_RESPONSE";
    public const string LogisticsAssigneeResponse = "LOGISTICS_ASSIGNEE_RESPONSE";
    public const string LogisticsNegotiation = "LOGISTICS_NEGOTIATION";
    public const string LogisticsProposalResponse = "LOGISTICS_PROPOSAL_RESPONSE";
    public const string LogisticsHandoverSignature = "LOGISTICS_HANDOVER_SIGNATURE";

    // Per-campus form v2 identity claim (plan §4.4). Deliberately NOT in All: the generic anonymous
    // email-action handler must REJECT this context — accepting a contact claim requires an
    // authenticated session whose email matches the claim (dedicated v2 handler).
    public const string VisitContactClaim = "VISIT_CONTACT_CLAIM";

    public static readonly string[] All =
    {
        ParticipationResponse, LogisticsRequestResponse, LogisticsAssigneeResponse, LogisticsNegotiation,
        LogisticsProposalResponse, LogisticsHandoverSignature,
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
