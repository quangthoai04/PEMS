using MediatR;

namespace PEMS.Application.Departments.Queries.ViewDepartmentDetails;

/// <summary>
/// UC-105 View Department Details (Staff Leader). Returns general info of one department in the
/// caller's own campus. Scope is enforced in the handler (404 if missing, 403 if another campus).
/// </summary>
public sealed class ViewDepartmentDetailsQuery : IRequest<ViewDepartmentDetailsDto>
{
    public ulong DepartmentId { get; set; }
}
