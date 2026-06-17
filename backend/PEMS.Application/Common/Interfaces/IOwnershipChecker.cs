namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Helper for data-scope authorization checks performed inside command/query
/// handlers (the backend is always the final authority on scope).
/// </summary>
public interface IOwnershipChecker
{
    /// <summary>True when the resource belongs to the current user (Own / "O" scope).</summary>
    bool IsOwner(string? resourceOwnerUserId);

    /// <summary>True when the current user may act within the given campus.</summary>
    bool CanAccessCampus(string? campusId);

    /// <summary>True when the current user may act within the given department.</summary>
    bool CanAccessDepartment(string? departmentId);
}
