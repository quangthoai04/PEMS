namespace PEMS.Domain.Constants;

// Maps visit_participants.participant_role ENUM (SQL: reduced to 4 values).
// UI labels: IC_HOST=Host, IC_SUPPORT=Staff hỗ trợ IC, DEPT_SUPPORT=Phòng ban hỗ trợ, STUDENT=Sinh viên hỗ trợ.
public static class ParticipantRoles
{
    /// <summary>Staff assigned as the main host of the campus instance (is_host = true).</summary>
    public const string IcHost = "IC_HOST";

    /// <summary>Another STAFF of the same IC/campus invited by the host to help.</summary>
    public const string IcSupport = "IC_SUPPORT";

    /// <summary>A DEPARTMENT (department) user invited/assigned to support.</summary>
    public const string DeptSupport = "DEPT_SUPPORT";

    /// <summary>A STUDENT invited to support.</summary>
    public const string Student = "STUDENT";
}

// Maps visit_participants.status ENUM.
public static class ParticipantStatuses
{
    public const string Invited  = "INVITED";
    public const string Accepted = "ACCEPTED";
    public const string Declined = "DECLINED";
    public const string Assigned = "ASSIGNED";
    public const string Removed  = "REMOVED";
}
