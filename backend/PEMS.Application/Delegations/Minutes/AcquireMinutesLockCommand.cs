using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>Re-acquire the edit lock on an existing minutes record (re-open for editing). 409 if held by another.</summary>
public sealed record AcquireMinutesLockCommand(ulong MinutesId) : IRequest<MinuteDto>;
