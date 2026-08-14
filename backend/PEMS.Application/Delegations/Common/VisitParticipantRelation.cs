using System.Collections.Generic;
using System.Linq;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Which participant row represents a person's relation to a campus instance — the one question that
/// must be answered the same way everywhere, because "who is this row about" and "what may THIS caller
/// do" are different questions and were being answered with the same row.
///
/// <para>
/// <c>visit_participants</c> carries <c>UNIQUE (visit_instance_id, user_id)</c>, so one person holds at
/// most ONE row per instance and every flow reuses it (invite revives a DECLINED/REMOVED row rather than
/// adding a second). The ordering below therefore normally has nothing to choose between — it exists so
/// a caller reading a set of rows never depends on the order the database happened to return them in,
/// and so a superseded row can never outrank a live one.
/// </para>
/// </summary>
public static class VisitParticipantRelation
{
    /// <summary>
    /// Lower is "more live". ACCEPTED outranks ASSIGNED outranks INVITED; a DECLINED or REMOVED row is
    /// history and must never be picked over a live one.
    /// </summary>
    public static int LivenessRank(string? status) => status switch
    {
        ParticipantStatuses.Accepted => 0,
        ParticipantStatuses.Assigned => 1,
        ParticipantStatuses.Invited => 2,
        ParticipantStatuses.Declined => 3,
        ParticipantStatuses.Removed => 4,
        _ => 5,
    };

    /// <summary>A row that still stands: the person is on this visit, in some stage of answering.</summary>
    public static bool IsLive(string? status) =>
        status is ParticipantStatuses.Invited or ParticipantStatuses.Accepted or ParticipantStatuses.Assigned;

    /// <summary>
    /// The rows that carry the HOST role for <paramref name="userId"/> on this instance.
    ///
    /// <para>
    /// Matching on user id alone is what let a handover rewrite whatever other relation that person
    /// happened to hold here. The host marker is the pair (<c>is_host</c>,
    /// <c>participant_role = IC_HOST</c>); either half alone is drift, and a row carrying only one half
    /// still has to be demoted or the instance keeps a second "host" behind it — so both shapes match,
    /// with the intact pair first. REMOVED rows are history and are never touched.
    /// </para>
    /// </summary>
    public static List<VisitParticipant> HostRowsOf(IEnumerable<VisitParticipant> rows, ulong userId)
        => rows
            .Where(p => p.UserId == userId
                        && p.Status != ParticipantStatuses.Removed
                        && (p.IsHost || p.ParticipantRole == ParticipantRoles.IcHost))
            .OrderByDescending(p => p.IsHost && p.ParticipantRole == ParticipantRoles.IcHost)
            .ThenByDescending(p => p.ParticipantId)
            .ToList();

    /// <summary>
    /// The single row to treat as <paramref name="userId"/>'s relation to this instance — the most live
    /// one. Includes DECLINED/REMOVED rows as a last resort because the unique key forces every flow to
    /// REUSE the existing row rather than add another; callers that need "is this person actually on the
    /// visit" ask <see cref="IsLive"/> of the result.
    /// </summary>
    public static VisitParticipant? RowOf(IEnumerable<VisitParticipant> rows, ulong userId)
        => rows
            .Where(p => p.UserId == userId)
            .OrderBy(p => LivenessRank(p.Status))
            .ThenByDescending(p => p.ParticipantId)
            .FirstOrDefault();
}
