using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Save the minutes content. Requires the caller to currently hold the edit lock (matching
/// <see cref="EditLockToken"/>) and an up-to-date <see cref="RowVersion"/>. On success the content
/// is persisted, row_version is bumped, status becomes SAVED and the lock is released.
/// </summary>
public sealed record SaveMinutesCommand(
    ulong MinutesId,
    string Title,
    string? Content,
    string EditLockToken,
    uint RowVersion) : IRequest<MinuteDto>;
