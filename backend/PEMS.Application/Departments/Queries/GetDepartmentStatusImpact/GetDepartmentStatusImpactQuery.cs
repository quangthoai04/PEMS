using MediatR;

namespace PEMS.Application.Departments.Queries.GetDepartmentStatusImpact;

/// <summary>
/// UC-106 impact preview for the confirmation modal: how many DEPARTMENT accounts / active
/// sessions a disable would affect and which business dependencies block it. Read-only —
/// the ManageDepartmentStatus command re-validates everything server-side regardless.
/// </summary>
public sealed class GetDepartmentStatusImpactQuery : IRequest<GetDepartmentStatusImpactResponse>
{
    public ulong DepartmentId { get; set; }

    /// <summary>Target status being previewed: ACTIVE or INACTIVE.</summary>
    public string NewStatus { get; set; } = string.Empty;
}
