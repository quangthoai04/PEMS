using System.Collections.Generic;
using MediatR;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Commands.UpdateEmailDraft;

/// <summary>
/// Updates an existing DRAFT (autosave). Replaces subject/body and the full recipient/attachment
/// sets. Only the draft owner may edit it, and only while it is still in DRAFT status.
/// </summary>
public sealed class UpdateEmailDraftCommand : IRequest<EmailDraftDto>
{
    public ulong EmailDraftId { get; set; }
    public ulong? EmailTemplateId { get; set; }
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public string? Subject { get; set; }
    public string? BodyContent { get; set; }
    public string? BodyFormat { get; set; }
    public List<EmailDraftRecipientInput> Recipients { get; set; } = new();
    public List<EmailDraftAttachmentInput> Attachments { get; set; } = new();
}
