using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceSentEmails;

/// <summary>
/// Lists the emails already sent for a campus instance, optionally narrowed to one target
/// (a participant invitation or a logistics request). Joins sent_emails + sent_email_recipients
/// (+ email_templates for the template code/name). Read-only; scope is enforced server-side
/// (host of the instance, the campus Staff Leader, or HO — never another instance's emails).
/// </summary>
public sealed record GetVisitInstanceSentEmailsQuery(
    ulong VisitInstanceId,
    string? RelatedType = null,     // VISIT_PARTICIPANT | LOGISTICS_ITEM (null = every target of the instance)
    ulong? RelatedId = null,        // participant_id or logistics_item_id when RelatedType is set
    string? RecipientEmail = null   // optional extra filter on the recipient address
) : IRequest<GetVisitInstanceSentEmailsResponse>;
