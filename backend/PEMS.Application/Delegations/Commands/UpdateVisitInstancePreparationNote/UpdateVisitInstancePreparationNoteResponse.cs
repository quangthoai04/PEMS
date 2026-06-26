namespace PEMS.Application.Delegations.Commands.UpdateVisitInstancePreparationNote;

public sealed record UpdateVisitInstancePreparationNoteResponse(
    ulong VisitInstanceId,
    string? PreparationNote,
    string Message);
