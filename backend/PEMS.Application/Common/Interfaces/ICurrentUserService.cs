namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Exposes the identity of the caller for the current request, read from the
/// validated JWT claims. All properties are null when the request is anonymous.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? Email { get; }
    string? RoleId { get; }
    string? RoleCode { get; }
    string? SubRole { get; }
    string? PrimaryCampusId { get; }
    string? DepartmentId { get; }
    string? SessionId { get; }
    string? LoginPortal { get; }
}
