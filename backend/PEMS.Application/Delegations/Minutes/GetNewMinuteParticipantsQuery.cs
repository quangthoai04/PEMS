using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// "Đồng bộ người mới": computes the attendance rows that the auto-fill rule says should exist for an
/// existing biên bản but are not in <c>minute_participants</c> yet (a newly-accepted participant, a
/// late guest, or a host assigned after creation). It does NOT persist anything — the rows are
/// returned as candidates (with <c>minuteParticipantId = 0</c>) for the editor to append to its draft
/// and save. This keeps it append-only and never disturbs rows the Host already edited/checked.
///
/// <para>
/// <paramref name="IgnoredExistingParticipantIds"/> carries the rows the caller has REMOVED from the
/// draft but has not saved yet. Those rows are still in <c>minute_participants</c>, so without this the
/// auto-fill rule would keep counting them as "already present" and the person the editor just deleted
/// could never be synced back in the same session. Ignoring them here does not delete anything: the
/// row only disappears from the database if the save actually omits it.
/// </para>
/// </summary>
public sealed record GetNewMinuteParticipantsQuery(
    ulong MinutesId,
    IReadOnlyList<ulong>? IgnoredExistingParticipantIds = null) : IRequest<List<MinuteParticipantDto>>;
