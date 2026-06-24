namespace PEMS.Application.Campuses.Commands.AddNewCampus;

/// <summary>UC-81 §7 response — the new campus plus the auto-created IC department.</summary>
public sealed class AddNewCampusResponse
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string City { get; init; } = null!;
    public string Address { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string Email { get; init; } = null!;
    public ulong? IcHeadUserId { get; init; }
    public string Status { get; init; } = "ACTIVE";
    public IcDepartmentInfo IcDepartment { get; init; } = null!;

    public sealed class IcDepartmentInfo
    {
        public ulong DepartmentId { get; init; }
        public ulong CampusId { get; init; }
        public string Name { get; init; } = null!;
        public string DepartmentType { get; init; } = "IC";
        public string Status { get; init; } = "ACTIVE";
    }
}
