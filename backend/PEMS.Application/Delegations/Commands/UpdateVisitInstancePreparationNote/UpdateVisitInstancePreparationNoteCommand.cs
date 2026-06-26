using MediatR;

namespace PEMS.Application.Delegations.Commands.UpdateVisitInstancePreparationNote;

/// <summary>
/// Host saves the internal "Ghi chú chung" (visit_request_campuses.preparation_note) for a campus
/// instance. Only the official current host may edit it, and only while the instance is in the prep
/// window (ASSIGNED / BEFORE_VISIT). The note may be null/empty (clears it).
/// </summary>
public sealed record UpdateVisitInstancePreparationNoteCommand(
    ulong VisitInstanceId,
    string? Note) : IRequest<UpdateVisitInstancePreparationNoteResponse>;
