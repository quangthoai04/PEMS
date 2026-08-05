using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Emails.Sender;

namespace PEMS.UnitTests.TestInfrastructure;

/// <summary>
/// A fixed sender, so a test that is about something else does not need a users table to render a body.
///
/// <para>
/// The values are deliberately recognisable rather than realistic: a test asserting that the Host's
/// address reached the message wants to see THIS address and no other, and a plausible-looking
/// <c>an.nv@fpt.edu.vn</c> would also match a body that had fallen back to a sample. The address uses the
/// reserved <c>.invalid</c> TLD, so a stubbed value that escaped into a real send could never be
/// delivered.
/// </para>
/// </summary>
public sealed class StubEmailSenderVariableResolver : IEmailSenderVariableResolver
{
    public const string Name = "Nguyễn Văn Chủ Trì";
    public const string Email = "chutri@stub.invalid";
    public const string Role = "Người phụ trách tiếp đón";

    /// <summary>The actor id each call was made with, so a test can assert WHOSE identity was resolved.</summary>
    public System.Collections.Generic.List<ulong?> Calls { get; } = new();

    public Task<EmailSenderVariables> ResolveAsync(
        ulong? actorUserId, string? templateCode, CancellationToken cancellationToken = default)
    {
        Calls.Add(actorUserId);

        // Capability is honoured even here: a template that may not name a sender must not get a person
        // out of a stub either, or a test could pass on behaviour production refuses.
        if (!EmailSenderVariableCapabilities.AllowsVariables(templateCode))
        {
            return Task.FromResult(new EmailSenderVariables(
                Name: "Bộ phận hỗ trợ PEMS",
                Role: "Hệ thống PEMS",
                Email: "support@stub.invalid",
                Department: "PEMS",
                IsSystemSender: true));
        }

        return Task.FromResult(new EmailSenderVariables(
            Name: Name,
            Role: Role,
            Email: Email,
            Phone: "0900000000",
            Department: "Phòng Hợp tác Quốc tế",
            Campus: "FPTU Hà Nội",
            IsSystemSender: false));
    }
}
