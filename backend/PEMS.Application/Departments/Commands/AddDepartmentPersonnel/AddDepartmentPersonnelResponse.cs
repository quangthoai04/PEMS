namespace PEMS.Application.Departments.Commands.AddDepartmentPersonnel;

public class AddDepartmentPersonnelResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ulong UserId { get; set; }

    /// <summary>Confirmation-email delivery outcome: SENT | SKIPPED | FAILED (truthful).</summary>
    public string EmailNotificationStatus { get; set; } = string.Empty;
}
