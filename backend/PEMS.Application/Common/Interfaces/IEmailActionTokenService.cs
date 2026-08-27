namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Mints and resolves one-time email-action tokens (email_action_tokens). The raw token is only
/// ever embedded in an email link — the DB stores its SHA-256 hash, never the raw value. Building
/// the public link URL needs the API base address, so the concrete service lives in Infrastructure
/// (alongside <see cref="IEmailService"/>).
/// </summary>
public interface IEmailActionTokenService
{
    /// <summary>Generates a cryptographically-random URL-safe opaque token.</summary>
    string GenerateRawToken();

    /// <summary>Deterministic SHA-256 hex hash of a raw token (stable for lookup by hash).</summary>
    string Hash(string rawToken);

    /// <summary>Public, no-login URL an email button points to (GET shows a confirm page, POST
    /// executes). Same URL for ACCEPT and DECLINE — the action is encoded in the token itself.</summary>
    string BuildPublicActionUrl(string rawToken);

    /// <summary>
    /// Internal, login-required URL for a Department Staff member's own participant-assignment row
    /// (accept/decline "Gán nhân sự", and the Department-Leader "Gán nhân sự" action) — the one route
    /// that actually exists for it in the SPA (<c>/dashboard/visit/department-tasks/:participantId</c>).
    /// </summary>
    string BuildVisitParticipantAssignmentUrl(ulong participantId);

    /// <summary>
    /// Internal, login-required URL to a logistics item for a Department **Staff** recipient — the
    /// dashboard query-param shape <c>DeptStaffDashboard</c> actually consumes.
    /// </summary>
    string BuildDepartmentStaffLogisticsTaskUrl(ulong logisticsItemId);

    /// <summary>
    /// Internal, login-required URL to a logistics item for a Department **Leader** recipient — the
    /// dashboard query-param shape <c>SharedDashboardView</c> (under the Leader's visit tasks page)
    /// actually consumes.
    /// </summary>
    string BuildDepartmentLeaderLogisticsTaskUrl(ulong logisticsItemId);

    /// <summary>
    /// Internal, login-required URL to a campus visit instance's process screen — the single-id route
    /// (<c>/dashboard/visit/process/:id</c>) that actually exists. Used for Host-facing detail links:
    /// reminders, and the now-detail-only logistics-proposal email.
    /// </summary>
    string BuildHostVisitProcessUrl(ulong visitInstanceId);

    /// <summary>
    /// Internal, login-required URL to a campus visit instance's contribution screen — the route
    /// (<c>/dashboard/visit/contribution/:id</c>) a non-Host recipient (participant) lands on. Used
    /// wherever a reminder or notification addresses someone who is not the current Host: they must
    /// never be sent to <see cref="BuildHostVisitProcessUrl"/>, which is a Host-only operational screen.
    /// </summary>
    string BuildVisitContributionUrl(ulong visitInstanceId);
}
