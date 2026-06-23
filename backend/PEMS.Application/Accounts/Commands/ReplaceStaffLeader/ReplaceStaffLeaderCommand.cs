using MediatR;

namespace PEMS.Application.Accounts.Commands.ReplaceStaffLeader;

/// <summary>
/// Replace the Staff Leader (Trưởng phòng IC) of a campus (HO only). Two modes:
/// EXISTING_USER promotes an existing IC Staff; CREATE_NEW_USER creates a fresh Staff Leader.
/// The old leader is always demoted to STAFF/STAFF (status preserved). See REPLACE_STAFF_LEADER spec.
/// </summary>
public sealed class ReplaceStaffLeaderCommand : IRequest<ReplaceStaffLeaderResponse>
{
    public ulong CampusId { get; set; }

    /// <summary>EXISTING_USER | CREATE_NEW_USER.</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>Required for EXISTING_USER — the IC Staff to promote.</summary>
    public ulong? NewLeaderUserId { get; set; }

    // ── CREATE_NEW_USER fields ──
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public PEMS.Domain.Enums.Gender? Gender { get; set; }

    /// <summary>Mandatory reason for the replacement (audited).</summary>
    public string Reason { get; set; } = string.Empty;
}

public static class ReplaceStaffLeaderModes
{
    public const string ExistingUser = "EXISTING_USER";
    public const string CreateNewUser = "CREATE_NEW_USER";
}
