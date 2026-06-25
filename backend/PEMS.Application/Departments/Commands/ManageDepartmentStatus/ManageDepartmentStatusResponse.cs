namespace PEMS.Application.Departments.Commands.ManageDepartmentStatus;

public sealed class ManageDepartmentStatusResponse
{
    public ulong DepartmentId { get; init; }
    public string Status { get; init; } = default!;
    public string Message { get; init; } = default!;
}
