using MediatR;

namespace PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;

/// <summary>
/// Loads the full form data of a visit request so the Visitor OWNER can prefill the
/// edit (pending) or resubmit (rejected) form. Only the owner may call it, and only
/// while the request is editable (fully pending) or resubmittable (fully rejected).
/// </summary>
public sealed record GetEditableVisitRequestDetailQuery(ulong VisitRequestId)
    : IRequest<EditableVisitRequestDetailDto>;
