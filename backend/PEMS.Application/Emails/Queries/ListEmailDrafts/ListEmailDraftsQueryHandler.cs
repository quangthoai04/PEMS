using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Queries.ListEmailDrafts;

/// <summary>
/// Lists the caller's own unsent drafts.
///
/// <para>
/// Ownership is enforced here, in the predicate — not by the controller's role attribute. The roles on
/// <c>EmailsController</c> say who may use the mailbox at all; they do not say whose drafts these are,
/// and every role on that list is held by many people. Scoping in the query is what makes another
/// user's draft invisible rather than merely unreachable by URL.
/// </para>
/// </summary>
public sealed class ListEmailDraftsQueryHandler
    : IRequestHandler<ListEmailDraftsQuery, ListEmailDraftsResponse>
{
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ListEmailDraftsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ListEmailDraftsResponse> Handle(
        ListEmailDraftsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, MaxPageSize);

        // Own-scope + DRAFT only. SENT and DISCARDED are excluded by status, not by filtering them out
        // afterwards: a sent draft is history and must not reappear as something still editable.
        var query = _db.EmailDrafts
            .AsNoTracking()
            .Where(d => d.CreatedBy == userId && d.Status == EmailDraftStatus.DRAFT);

        var totalCount = await query.CountAsync(cancellationToken);

        // Counts are projected as subqueries so the database returns them with the page — loading the
        // recipient and attachment collections per row would be one round trip per draft.
        var items = await query
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .ThenByDescending(d => d.EmailDraftId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new EmailDraftSummaryDto
            {
                EmailDraftId = d.EmailDraftId,
                Subject = d.Subject,
                UpdatedAt = d.UpdatedAt ?? d.CreatedAt,
                RecipientCount = d.Recipients.Count,
                AttachmentCount = d.Attachments.Count,
            })
            .ToListAsync(cancellationToken);

        return new ListEmailDraftsResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }
}
