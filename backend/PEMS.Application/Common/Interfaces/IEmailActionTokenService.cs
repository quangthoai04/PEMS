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

    /// <summary>Internal, login-required URL for the Department-Leader "Gán nhân sự" action (never a
    /// public token — assignment requires authentication).</summary>
    string BuildDepartmentAssignmentUrl(ulong visitInstanceId, ulong participantId);

    /// <summary>Internal, login-required URL to a logistics request's detail page (Department flow).
    /// Used by the logistics-request email — no public token (reject/assign require authentication).</summary>
    string BuildLogisticsDetailUrl(ulong logisticsItemId);
}
