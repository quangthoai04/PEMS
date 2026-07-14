using System;
using PEMS.Application.Campuses.Common;

namespace PEMS.Application.Campuses.Queries.ViewCampusDetails;

/// <summary>UC-84 §7 full campus detail projection (master data + audit + IC department).</summary>
public sealed class ViewCampusDetailsDto
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? City { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public ulong? IcHeadUserId { get; init; }
    public string? IcHeadName { get; init; }
    public string Status { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public ulong? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public ulong? UpdatedBy { get; init; }
    public string? UpdatedByName { get; init; }
    public IcDepartmentDetail? IcDepartment { get; init; }

    /// <summary>Computed operational availability (UC-86 §21).</summary>
    public CampusOperationalReadinessDto? Readiness { get; set; }

    public sealed class IcDepartmentDetail
    {
        public ulong DepartmentId { get; init; }
        public string Name { get; init; } = null!;
        public string DepartmentType { get; init; } = "IC";
        public string Status { get; init; } = null!;
        public ulong? HeadUserId { get; init; }
        public string? HeadUserName { get; init; }
    }
}
