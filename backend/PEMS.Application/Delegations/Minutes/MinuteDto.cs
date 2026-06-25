namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// One meeting-minutes record for a campus instance (UC biên bản). There is at most ONE per
/// visit_instance. Carries the edit-lock state + backend-computed action flags so the frontend
/// renders the right buttons without re-deriving permission from status text.
/// <para><see cref="EditLockToken"/> is ONLY populated in responses to the user who just acquired
/// the lock (create-or-lock / acquire-lock); it is never returned by the read query.</para>
/// </summary>
public sealed class MinuteDto
{
    public bool Exists { get; set; }
    public ulong? MinutesId { get; set; }
    public ulong VisitInstanceId { get; set; }

    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Status { get; set; }
    public uint RowVersion { get; set; }

    public ulong? EditLockedBy { get; set; }
    public string? EditLockedByName { get; set; }
    public DateTime? EditLockedAt { get; set; }
    public DateTime? EditLockExpiresAt { get; set; }

    /// <summary>True when an unexpired lock is held by someone other than the caller.</summary>
    public bool IsLockedByOther { get; set; }
    /// <summary>True when an unexpired lock is held by the caller.</summary>
    public bool IsLockedByMe { get; set; }

    /// <summary>Lock session token — only present for the caller who currently holds the lock.</summary>
    public string? EditLockToken { get; set; }

    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }

    /// <summary>Snapshot attendance list (minute_participants). Empty when the minutes does not exist yet.</summary>
    public List<MinuteParticipantDto> Participants { get; set; } = new();
    /// <summary>Action items (minute_action_items). Empty when the minutes does not exist yet.</summary>
    public List<MinuteActionItemDto> ActionItems { get; set; } = new();
}
