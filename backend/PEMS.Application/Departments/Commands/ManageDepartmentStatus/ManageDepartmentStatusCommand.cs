using MediatR;

namespace PEMS.Application.Departments.Commands.ManageDepartmentStatus;

/// <summary>
/// Enable/disable a GENERAL department in the Staff Leader's own campus (the toggle shown in the
/// UC-104 list for GENERAL rows). IC departments are not toggleable. Status is ACTIVE/INACTIVE.
/// </summary>
public sealed class ManageDepartmentStatusCommand : IRequest<ManageDepartmentStatusResponse>
{
    public ulong DepartmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}
