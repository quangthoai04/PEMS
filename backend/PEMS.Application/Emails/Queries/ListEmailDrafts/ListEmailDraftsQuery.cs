using System;
using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Emails.Queries.ListEmailDrafts;

/// <summary>
/// The current user's unsent drafts, for the "Nháp" tab.
///
/// <para>
/// Deliberately its own query rather than a branch of <c>ViewEmailList</c>: <c>email_drafts</c> and
/// <c>sent_emails</c> have different lifecycles (a draft can still be edited or discarded), different
/// shapes, and different access rules — a draft is Own-scope, a sent email is not. Unioning them would
/// have forced one DTO and one authorization predicate onto two things that share neither.
/// </para>
/// </summary>
public sealed class ListEmailDraftsQuery : IRequest<ListEmailDraftsResponse>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// A row in the drafts list. Intentionally minimal.
///
/// <para>
/// No body and no recipient addresses. The list only needs to let someone recognise and reopen a
/// draft, and a collection endpoint that returned every BCC address would put the blind-copy list into
/// a response the screen never asked for. Both are loaded by <c>GET /Emails/drafts/{id}</c> when the
/// draft is actually opened.
/// </para>
/// </summary>
public sealed class EmailDraftSummaryDto
{
    public ulong EmailDraftId { get; set; }
    public string? Subject { get; set; }

    /// <summary>Last edit, falling back to creation for a draft never edited since.</summary>
    public DateTime UpdatedAt { get; set; }

    public int RecipientCount { get; set; }
    public int AttachmentCount { get; set; }
}

public sealed class ListEmailDraftsResponse
{
    public IReadOnlyList<EmailDraftSummaryDto> Items { get; set; } = Array.Empty<EmailDraftSummaryDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
