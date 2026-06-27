using System.Collections.Generic;
using MediatR;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Commands.CreateEmailDraft;

/// <summary>
/// Creates a new editable email draft (status DRAFT) owned by the current user, similar to a
/// mail-compose draft. Subject/body may be empty (autosave-friendly). HTML bodies are sanitized
/// before persisting; attachments must reference files the current user owns.
/// </summary>
public sealed class CreateEmailDraftCommand : IRequest<EmailDraftDto>
{
    public ulong? EmailTemplateId { get; set; }
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public string? Subject { get; set; }
    public string? BodyContent { get; set; }
    /// <summary>HTML | PLAIN_TEXT (defaults to HTML).</summary>
    public string? BodyFormat { get; set; }
    public List<EmailDraftRecipientInput> Recipients { get; set; } = new();
    public List<EmailDraftAttachmentInput> Attachments { get; set; } = new();
}
