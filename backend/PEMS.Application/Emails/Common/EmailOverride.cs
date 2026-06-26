namespace PEMS.Application.Emails.Common;

/// <summary>
/// Optional user-edited email content carried by send/invite commands. When
/// <see cref="UseEditedContent"/> is true the backend uses <see cref="Subject"/> + (sanitized)
/// <see cref="BodyHtml"/> as the message content, then ALWAYS injects the system action block with
/// real tokens — the edited body is never trusted to contain live action URLs.
/// </summary>
public sealed record EmailOverride(
    bool UseEditedContent,
    string? Subject,
    string? BodyHtml);

public static class EmailOverrideLimits
{
    public const int SubjectMax = 255;
    public const int BodyMax = 50_000;
}
