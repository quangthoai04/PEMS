using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.UnitTests.TestInfrastructure;

/// <summary>
/// Records what a handler asked to send, and returns the outcome the test wants.
///
/// <para>
/// Asserting on the TEMPLATE CODE is the point of this fake. Subjects and bodies now live in
/// <c>email_templates</c>, so a handler that quietly went back to composing its own content would no
/// longer name a template here — the assertion fails rather than the email silently regressing.
/// </para>
/// </summary>
public sealed class FakeSystemEmailDispatcher : ISystemEmailDispatcher
{
    public List<SystemEmailRequest> Sent { get; } = new();

    /// <summary>What the sender reports back. Defaults to a successful send.</summary>
    public EmailDeliveryResult Outcome { get; set; } = EmailDeliveryResult.Sent();

    /// <summary>Per-message outcome, for call sites that send several and report a combined status.</summary>
    public Func<SystemEmailRequest, EmailDeliveryResult>? OutcomeFor { get; set; }

    /// <summary>When set, thrown instead of sending — for exercising best-effort call sites.</summary>
    public System.Exception? ThrowOnSend { get; set; }

    public Task<SystemEmailDispatchResult> SendAsync(
        SystemEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend is not null) throw ThrowOnSend;

        Sent.Add(request);
        return Task.FromResult(new SystemEmailDispatchResult(
            OutcomeFor?.Invoke(request) ?? Outcome, SentEmailId: (ulong)Sent.Count, EmailTemplateId: 1));
    }

    /// <summary>
    /// Records the message the way the real dispatcher would, so a two-phase caller gets a usable
    /// <c>SentEmailId</c> to link its own rows to. The outcome is not applied until
    /// <see cref="DeliverAsync"/>, matching the real split.
    /// </summary>
    public Task<PreparedSystemEmail> PrepareAsync(
        SystemEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend is not null) throw ThrowOnSend;

        Sent.Add(request);
        var id = (ulong)Sent.Count;

        Prepared.Add(request);
        return Task.FromResult(new PreparedSystemEmail(
            SentEmailId: id,
            SentEmailRecipientId: id,
            EmailTemplateId: 1,
            TemplateCode: request.TemplateCode,
            To: request.To,
            Subject: $"[{request.TemplateCode}]",
            Body: string.Empty,
            IsHtml: true)
        {
            Attachments = request.Attachments ?? System.Array.Empty<OutboundAttachment>(),
        });
    }

    public Task<EmailDeliveryResult> DeliverAsync(
        PreparedSystemEmail prepared, CancellationToken cancellationToken = default)
    {
        Delivered.Add(prepared);

        var request = Sent.ElementAtOrDefault((int)prepared.SentEmailId - 1);
        return Task.FromResult(
            (request is not null ? OutcomeFor?.Invoke(request) : null) ?? Outcome);
    }

    /// <summary>Messages recorded through the two-phase path, in order.</summary>
    public List<SystemEmailRequest> Prepared { get; } = new();

    /// <summary>Messages actually handed to delivery, in order.</summary>
    public List<PreparedSystemEmail> Delivered { get; } = new();

    /// <summary>The single message sent with this template code (fails when there is not exactly one).</summary>
    public SystemEmailRequest Single(string templateCode)
        => Sent.Single(r => r.TemplateCode == templateCode);

    /// <summary>Every message sent with this template code, in send order.</summary>
    public IReadOnlyList<SystemEmailRequest> All(string templateCode)
        => Sent.Where(r => r.TemplateCode == templateCode).ToList();
}
