using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>Read the (single) meeting-minutes record for a campus instance, with lock state + action flags.</summary>
public sealed record GetVisitInstanceMinutesQuery(ulong VisitInstanceId) : IRequest<MinuteDto>;
