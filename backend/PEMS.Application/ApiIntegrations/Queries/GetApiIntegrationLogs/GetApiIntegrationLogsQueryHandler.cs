using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationLogs;

public sealed class GetApiIntegrationLogsQueryHandler
    : IRequestHandler<GetApiIntegrationLogsQuery, ApiRequestLogListResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetApiIntegrationLogsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ApiRequestLogListResponse> Handle(
        GetApiIntegrationLogsQuery request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureRead(_currentUser);

        var exists = await _db.ApiConfigurations
            .AnyAsync(c => c.ApiConfigId == request.ApiConfigId && c.DeletedAt == null, cancellationToken);
        if (!exists) throw new NotFoundException("ApiConfiguration", request.ApiConfigId);

        var query = _db.ApiRequestLogs.AsNoTracking()
            .Where(l => l.ApiConfigId == request.ApiConfigId);
        if (request.Success is { } success)
            query = query.Where(l => l.Success == success);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(l => l.ApiRequestLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = rows.Where(r => r.RequestedBy != null).Select(r => r.RequestedBy!.Value).Distinct().ToList();
        var names = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => userIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        return new ApiRequestLogListResponse
        {
            Items = rows.Select(l => new ApiRequestLogDto
            {
                ApiRequestLogId = l.ApiRequestLogId,
                ApiConfigId = l.ApiConfigId,
                Endpoint = l.Endpoint,
                Method = l.Method,
                HttpStatus = l.HttpStatus,
                ResponseTimeMs = l.ResponseTimeMs,
                Success = l.Success,
                ErrorCode = l.ErrorCode,
                ErrorMessage = l.ErrorMessage,
                RequestedByName = l.RequestedBy is { } uid && names.TryGetValue(uid, out var n) ? n : null,
                CreatedAt = l.CreatedAt,
            }).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}
