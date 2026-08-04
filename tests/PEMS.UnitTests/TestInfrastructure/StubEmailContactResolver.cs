using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Emails.Contact;

namespace PEMS.UnitTests.TestInfrastructure;

/// <summary>
/// A contact resolver that answers from memory instead of the database.
///
/// <para>
/// Unit tests that build a handler by hand need SOMETHING here, and the real resolver reads four tables.
/// The default is a resolvable contact, because that is the ordinary case and a test about draft
/// preparation should not have to know about contact policy to get past it. Tests that care set
/// <see cref="Resolution"/> — including to a REQUIRED policy with no contact, which is how the fail-closed
/// path is exercised without standing up a schema.
/// </para>
/// </summary>
public sealed class StubEmailContactResolver : IEmailContactResolver
{
    public static readonly EmailContactInformation SampleHost = new(
        PEMS.Domain.Enums.EmailContactSource.HOST,
        "Nguyễn Văn A",
        RoleLabel: "Người phụ trách tiếp đón",
        CampusName: "FPT University HCM",
        Email: "host.a@fpt.edu.vn",
        Phone: "0900000001");

    /// <summary>What the next call returns. Defaults to a resolvable Host with the shipped policy.</summary>
    public EmailContactResolution Resolution { get; set; } = new(
        EmailContactPolicyDefaults.SystemBaseline with
        {
            Requirement = PEMS.Domain.Enums.EmailContactRequirement.REQUIRED,
            ContactSource = PEMS.Domain.Enums.EmailContactSource.HOST,
            ShowCampus = true,
        },
        SampleHost,
        "<table><tr><td>Nguyễn Văn A</td></tr></table>",
        new EmailContactAddress("host.a@fpt.edu.vn", "Nguyễn Văn A"));

    /// <summary>The last request seen, so a test can assert the campus scope that was passed.</summary>
    public EmailContactRequest? LastRequest { get; private set; }

    /// <summary>
    /// The last per-message override seen. Recorded rather than acted on: this stub answers from memory,
    /// and a test that asserts the dispatcher PASSED the sender's choice down is asking a different
    /// question from one that asserts what the real resolver DOES with it.
    /// </summary>
    public EmailContactOverrideInput? LastOverride { get; private set; }

    /// <summary>The acting user the dispatcher attributed the choice to.</summary>
    public ulong? LastActorUserId { get; private set; }

    public Task<EmailContactResolution> ResolveAsync(
        EmailContactRequest request, CancellationToken cancellationToken = default)
        => ResolveAsync(request, null, null, cancellationToken);

    public Task<EmailContactResolution> ResolveAsync(
        EmailContactRequest request,
        EmailContactOverrideInput? overrideInput,
        ulong? actorUserId,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        LastOverride = overrideInput;
        LastActorUserId = actorUserId;
        return Task.FromResult(Resolution);
    }
}
