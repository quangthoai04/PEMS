using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Sender;

/// <summary>
/// Default <see cref="IEmailSenderVariableResolver"/>: the acting account, read fresh, or the configured
/// system sender.
///
/// <para>
/// <b>Read at send time, not carried from the preview.</b> A preview may have run minutes earlier, and in
/// between the sender could have been moved between departments or deactivated. The values that go into
/// the message are the ones true when it leaves — which is also why the preview and the send call the same
/// resolver rather than the send trusting what the preview produced.
/// </para>
/// <para>
/// <b>Whatever it returns is data.</b> The values reach the renderer as ordinary variable values, which are
/// HTML-encoded and substituted in a single pass. A profile containing <c>{{visitCode}}</c>, an ampersand
/// or a script tag is printed as those characters and interpreted as nothing.
/// </para>
/// </summary>
public sealed class EmailSenderVariableResolver : IEmailSenderVariableResolver
{
    private readonly IApplicationDbContext _db;
    private readonly EmailSystemSenderOptions _system;

    public EmailSenderVariableResolver(
        IApplicationDbContext db, IOptions<EmailSystemSenderOptions> system)
    {
        _db = db;
        _system = system?.Value ?? new EmailSystemSenderOptions();
    }

    public async Task<EmailSenderVariables> ResolveAsync(
        ulong? actorUserId,
        string? templateCode,
        CancellationToken cancellationToken = default)
    {
        // Capability first, and it outranks the actor. A template that may not name a person must not name
        // one even when a person did trigger it — an administrator resetting somebody's password is an
        // actor, and printing their name and telephone number on the OTP mail is precisely the disclosure
        // NOT_AVAILABLE exists to prevent.
        if (!EmailSenderVariableCapabilities.AllowsVariables(templateCode))
            return SystemSender();

        if (actorUserId is not { } id) return SystemSender();

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == id && u.Status == "ACTIVE")
            .Select(u => new
            {
                u.FullName,
                u.Email,
                u.Phone,
                u.SubRole,
                RoleName = u.Role != null ? u.Role.Name : null,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                CampusName = u.PrimaryCampus != null ? u.PrimaryCampus.Name : null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        // No row, or an account that is no longer active. Falling back to the system sender rather than
        // throwing is the right failure here: the message is legitimate and the recipient needs it, and
        // "from the PEMS support unit" is true, while blocking a notice because the administrator who
        // triggered it has since left would punish the wrong person.
        if (user is null) return SystemSender();

        // A blank name would leave "{{senderName}}" rendering as nothing in the middle of a sentence, so
        // the unit name stands in — the same value the system sender would have used.
        var displayName = string.IsNullOrWhiteSpace(user.FullName)
            ? SystemName()
            : user.FullName.Trim();

        return new EmailSenderVariables(
            Name: displayName,
            Role: ComposeRole(user.RoleName, user.SubRole),
            Email: Clean(user.Email),
            Phone: Clean(user.Phone),
            Department: Clean(user.DepartmentName),
            Campus: Clean(user.CampusName),
            IsSystemSender: false);
    }

    /// <summary>
    /// The identity used when nobody pressed send.
    ///
    /// <para>
    /// Campus is deliberately left empty rather than set to "Toàn hệ thống". Two account notices go to an
    /// address that may belong to a stranger reached by a typo, and naming a campus on those would disclose
    /// which campus the account belongs to — the same leak their empty variable list already avoids. A
    /// template that wants the words prints them itself.
    /// </para>
    /// </summary>
    private EmailSenderVariables SystemSender() => new(
        Name: SystemName(),
        // A role, not null. The shipped bodies print {{senderRole}} on its own line, and an empty one
        // would leave a blank gap in the middle of a signature — the reader cannot tell a missing value
        // from a formatting fault. "Hệ thống PEMS" is also the truth: nobody pressed send.
        Role: "Hệ thống PEMS",
        Email: Clean(_system.Email),
        Phone: Clean(_system.Phone),
        Department: Clean(_system.Department) ?? "PEMS",
        Campus: null,
        IsSystemSender: true);

    private string SystemName()
        => string.IsNullOrWhiteSpace(_system.Name) ? "Bộ phận hỗ trợ PEMS" : _system.Name!.Trim();

    /// <summary>
    /// The business role as a recipient would recognise it: the system role, narrowed by the sub-role when
    /// there is one ("Department Staff — Leader"). Both come from reference data an administrator
    /// maintains, so neither is invented here.
    /// </summary>
    private static string? ComposeRole(string? roleName, string? subRole)
    {
        var role = Clean(roleName);
        var sub = Clean(subRole);

        if (role is null) return sub;
        if (sub is null) return role;

        return string.Equals(role, sub, StringComparison.OrdinalIgnoreCase) ? role : $"{role} — {sub}";
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
