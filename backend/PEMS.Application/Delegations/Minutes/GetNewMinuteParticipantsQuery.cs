using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// "Đồng bộ người mới": computes the attendance rows that the auto-fill rule says should exist for an
/// existing biên bản but are not in <c>minute_participants</c> yet (a newly-accepted participant, a
/// late guest, or a host assigned after creation). It does NOT persist anything — the rows are
/// returned as candidates (with <c>minuteParticipantId = 0</c>) for the editor to append to its draft
/// and save. This keeps it append-only and never disturbs rows the Host already edited/checked.
/// </summary>
public sealed record GetNewMinuteParticipantsQuery(ulong MinutesId) : IRequest<List<MinuteParticipantDto>>;
