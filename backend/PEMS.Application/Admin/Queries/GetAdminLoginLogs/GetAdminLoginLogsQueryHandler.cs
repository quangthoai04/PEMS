using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Admin.Queries.GetAdminLoginLogs;

public sealed class GetAdminLoginLogsQueryHandler
    : IRequestHandler<GetAdminLoginLogsQuery, PaginatedResult<AdminLoginLogItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminLoginLogsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<AdminLoginLogItemDto>> Handle(
        GetAdminLoginLogsQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _db.LoginLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim().ToLower();
            query = query.Where(l =>
                l.Email.ToLower().Contains(kw) ||
                (l.User != null && l.User.FullName.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            query = status == "SUCCESS"
                ? query.Where(l => l.Status == "SUCCESS")
                : query.Where(l => l.Status != "SUCCESS");
        }

        if (!string.IsNullOrWhiteSpace(request.Portal))
        {
            var portal = request.Portal.Trim().ToUpperInvariant();
            query = query.Where(l => l.LoginPortal == portal);
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim().ToUpperInvariant();
            query = query.Where(l => l.ProviderType == provider);
        }

        if (!string.IsNullOrWhiteSpace(request.IpAddress))
        {
            var ip = request.IpAddress.Trim();
            query = query.Where(l => l.IpAddress != null && l.IpAddress.Contains(ip));
        }

        if (request.FromDate.HasValue)
            query = query.Where(l => l.CreatedAt >= request.FromDate.Value);
        if (request.ToDate.HasValue)
        {
            var toExclusive = request.ToDate.Value.Date.AddDays(1);
            query = query.Where(l => l.CreatedAt < toExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.LoginLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AdminLoginLogItemDto
            {
                LoginLogId = l.LoginLogId,
                UserId = l.UserId,
                Email = l.Email,
                FullName = l.User != null ? l.User.FullName : null,
                LoginPortal = l.LoginPortal,
                ProviderType = l.ProviderType,
                Status = l.Status,
                FailureReason = l.FailureReason,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                SessionId = l.SessionId,
                CreatedAt = l.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return PaginatedResult<AdminLoginLogItemDto>.Create(items, page, pageSize, totalItems);
    }
}
