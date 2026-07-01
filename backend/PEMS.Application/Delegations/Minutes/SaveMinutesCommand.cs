using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Save the minutes content together with the full attendance snapshot and action items. Requires the
/// caller to currently hold the edit lock (matching <see cref="EditLockToken"/>) and an up-to-date
/// <see cref="RowVersion"/>. On success everything is persisted in one transaction, row_version is
/// bumped, status becomes SAVED and the lock is released.
/// <para><see cref="Participants"/>/<see cref="ActionItems"/> are reconciled against the DB: rows with
/// an id are updated, rows without an id are inserted, and existing rows missing from the list are
/// deleted (snapshot only — never touches visit_guest_members / visit_participants). When a list is
/// <c>null</c> that collection is left untouched.</para>
/// </summary>
public sealed record SaveMinutesCommand(
    ulong MinutesId,
    string Title,
    string? Content,
    string EditLockToken,
    uint RowVersion,
    IReadOnlyList<SaveMinuteParticipantInput>? Participants,
    IReadOnlyList<SaveMinuteActionItemInput>? ActionItems,
    bool IsDraft = false) : IRequest<MinuteDto>;

/// <summary>
/// One attendance row to persist. <see cref="MinuteParticipantId"/> null = new row.
/// For a NEW row: <see cref="UserId"/> set → links a system user (kind INTERNAL); both ids null →
/// pure manual snapshot (kind MANUAL). <see cref="GuestMemberId"/> is only carried for existing
/// prefilled guest rows; clients never create guest rows directly.
/// </summary>
public sealed record SaveMinuteParticipantInput(
    ulong? MinuteParticipantId,
    ulong? UserId,
    ulong? GuestMemberId,
    string? FullNameSnapshot,
    string? RoleSnapshot,
    string? OrganizationSnapshot,
    string? EmailSnapshot,
    string AttendanceStatus,
    string? AttendanceNote);

/// <summary><see cref="ActionItemId"/> null = new row.</summary>
public sealed record SaveMinuteActionItemInput(
    ulong? ActionItemId,
    string Title,
    string? Note,
    DateTime? DueDate,
    string Status);
