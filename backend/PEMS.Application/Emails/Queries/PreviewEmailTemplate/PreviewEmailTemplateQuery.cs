using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Emails.Queries.PreviewEmailTemplate;

/// <summary>
/// Renders an email template's subject/body from email_templates with the supplied variable context,
/// for a "Xem trước email" modal. Read-only: never inserts sent_emails / sent_email_recipients /
/// email_action_tokens and never sends SMTP.
/// </summary>
public sealed record PreviewEmailTemplateQuery(
    string TemplateCode,
    Dictionary<string, string>? Context,
    string? Language) : IRequest<PreviewEmailTemplateResponse>;

public sealed record PreviewEmailTemplateResponse(string Subject, string BodyHtml);
