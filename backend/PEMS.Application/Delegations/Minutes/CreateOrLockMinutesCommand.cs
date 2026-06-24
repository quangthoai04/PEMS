using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Open the minutes editor for a campus instance: creates the single minutes record (if none
/// exists yet) and acquires the edit lock for the caller, or acquires the lock on the existing
/// record when it is free / expired. Returns the record + a lock token. Fails (409) when another
/// user currently holds an unexpired lock.
/// </summary>
public sealed record CreateOrLockMinutesCommand(ulong VisitInstanceId, string? Title) : IRequest<MinuteDto>;
