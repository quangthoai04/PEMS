namespace PEMS.Application.Emails.Common;

/// <summary>
/// Configuration for the recipient rules. Bound from the <c>Email</c> configuration section.
///
/// The ceiling lives here and in <c>appsettings*.json</c> — nowhere else. Spreading a literal like 50
/// across a validator, a handler and a frontend constant is how the three drift apart and the UI starts
/// promising something the backend rejects.
/// </summary>
public sealed class EmailRecipientOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Maximum TO + CC + BCC on a single message. 50 is a deliberately conservative ceiling for
    /// transactional mail: high enough for a department-wide report, low enough that the compose screen
    /// cannot be turned into a bulk-mail tool (out of scope by design).
    /// </summary>
    public int MaxRecipients { get; set; } = 50;
}
