namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Exposes the identity of the caller for the current request, read from the
/// validated JWT claims. All properties are null when the request is anonymous.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    ulong? UserId { get; }
    string? Email { get; }
    ulong? RoleId { get; }
    string? RoleCode { get; }
    string? SubRole { get; }
    ulong? PrimaryCampusId { get; }
    ulong? DepartmentId { get; }
    ulong? SessionId { get; }
    string? LoginPortal { get; }
}
