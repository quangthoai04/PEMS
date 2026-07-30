using MediatR;
using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.Emails.Commands.ReplytoEmail;

public class ReplytoEmailCommand : IRequest<ReplytoEmailResponse>, PEMS.Application.Emails.Idempotency.IIdempotentEmailSend
{
    /// <summary>
    /// Reply and Reply All reserve under DIFFERENT codes, so replaying one against the other is two
    /// independent reservations rather than a false "already sent" — they address different people.
    /// </summary>
    public string OperationCode => ReplyAll
        ? PEMS.Application.Emails.Idempotency.EmailSendOperations.ManualReplyAll
        : PEMS.Application.Emails.Idempotency.EmailSendOperations.ManualReply;

    /// <summary>
    /// The parent message, the mode, the body, and this author's own copies. The derived recipients are
    /// not described: they are resolved by the server from the parent, so they cannot vary between two
    /// requests that agree on everything named here.
    /// </summary>
    public void DescribeRequest(PEMS.Application.Emails.Idempotency.EmailSendFingerprintBuilder builder) =>
        builder.Id("original", OriginalEmailId)
               .Flag("replyAll", ReplyAll)
               .Text("body", Body)
               .Recipients("cc", Cc?.Select(r => r?.Email ?? string.Empty))
               .Recipients("bcc", Bcc?.Select(r => r?.Email ?? string.Empty));

    public ulong OriginalEmailId { get; set; }
    public string Body { get; set; } = null!;
    public List<EmailRecipientInput>? Cc { get; set; }
    public List<EmailRecipientInput>? Bcc { get; set; }

    /// <summary>
    /// True for Reply All: the visible recipients of the original message are carried into the reply
    /// alongside its sender. The blind copies of the original are not, and cannot be — the handler does
    /// not read them.
    ///
    /// <para>
    /// Deliberately NOT a list of addresses the caller supplies. Letting the client post "the people who
    /// were on the original message" would make the client the authority on who those were, and the one
    /// thing that must never be client-assertable is a recipient the sender was not allowed to see. The
    /// server reads the original's recipient rows itself.
    /// </para>
    /// </summary>
    public bool ReplyAll { get; set; }
}

public class EmailRecipientInput
{
    public string Email { get; set; } = null!;
    public string? Name { get; set; }
}