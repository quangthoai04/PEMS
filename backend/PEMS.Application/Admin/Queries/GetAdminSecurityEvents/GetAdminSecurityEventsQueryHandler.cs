using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Admin.Queries.GetAdminSecurityEvents;

public sealed class GetAdminSecurityEventsQueryHandler
    : IRequestHandler<GetAdminSecurityEventsQuery, PaginatedResult<AdminSecurityEventDetailDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminSecurityEventsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<AdminSecurityEventDetailDto>> Handle(
        GetAdminSecurityEventsQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _db.SecurityEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim().ToLower();
            query = query.Where(e =>
                (e.EmailSnapshot != null && e.EmailSnapshot.ToLower().Contains(kw)) ||
                (e.User != null && (e.User.Email.ToLower().Contains(kw) || e.User.FullName.ToLower().Contains(kw))));
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim().ToUpperInvariant();
            query = query.Where(e => e.Severity == severity);
        }

        if (!string.IsNullOrWhiteSpace(request.Result))
        {
            var result = request.Result.Trim().ToUpperInvariant();
            query = query.Where(e => e.Result == result);
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            var eventType = request.EventType.Trim().ToUpperInvariant();
            query = query.Where(e => e.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(request.Portal))
        {
            var portal = request.Portal.Trim().ToUpperInvariant();
            query = query.Where(e => e.LoginPortal == portal);
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim().ToUpperInvariant();
            query = query.Where(e => e.ProviderType == provider);
        }

        if (!string.IsNullOrWhiteSpace(request.IpAddress))
        {
            var ip = request.IpAddress.Trim();
            query = query.Where(e => e.IpAddress != null && e.IpAddress.Contains(ip));
        }

        if (request.FromDate.HasValue)
            query = query.Where(e => e.CreatedAt >= request.FromDate.Value);
        if (request.ToDate.HasValue)
        {
            var toExclusive = request.ToDate.Value.Date.AddDays(1);
            query = query.Where(e => e.CreatedAt < toExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.SecurityEventId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AdminSecurityEventDetailDto
            {
                SecurityEventId = e.SecurityEventId,
                UserId = e.UserId,
                Email = e.EmailSnapshot ?? (e.User != null ? e.User.Email : null),
                EventType = e.EventType,
                Result = e.Result,
                FailureReasonCode = e.FailureReasonCode,
                Severity = e.Severity,
                IpAddress = e.IpAddress,
                UserAgent = e.UserAgent,
                LoginPortal = e.LoginPortal,
                ProviderType = e.ProviderType,
                SessionId = e.SessionId,
                DetailText = e.DetailText,
                CreatedAt = e.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // detail_text may embed serialized payloads — scrub any credential-looking JSON keys.
        foreach (var item in items)
            item.DetailText = SensitiveDataMask.MaskValue("detail_text", item.DetailText);

        return PaginatedResult<AdminSecurityEventDetailDto>.Create(items, page, pageSize, totalItems);
    }
}
