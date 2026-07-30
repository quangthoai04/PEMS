using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.Minutes.Queries.GetMyActionItems;

public sealed class GetMyActionItemsQueryHandler : IRequestHandler<GetMyActionItemsQuery, GetMyActionItemsDto>
{
    private static readonly int[] AllowedPageSizes = { 5, 10, 20, 50 };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyActionItemsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetMyActionItemsDto> Handle(GetMyActionItemsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var userId = _currentUser.UserId.Value;

        var baseQuery =
            from ai in _db.MinuteActionItems
            join m in _db.Minutes on ai.MinutesId equals m.MinutesId
            join vrc in _db.VisitRequestCampuses on m.VisitInstanceId equals vrc.VisitInstanceId
            where ai.AssignedToUserId == userId
            select new
            {
                ai,
                vrc.VisitInstanceId,
                vrc.VisitRequestId,
                DelegationName = vrc.FormDetail != null ? vrc.FormDetail.DelegationName : null,
            };

        if (request.ActionItemId.HasValue)
        {
            // Deep-link từ thông báo: trả đúng 1 item của chính người này, bỏ qua mọi filter khác
            // (item có thể đã hủy/quá hạn — vẫn phải hiện ra vì user bấm thẳng từ chuông thông báo).
            baseQuery = baseQuery.Where(x => x.ai.ActionItemId == request.ActionItemId.Value);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var q = request.Q.Trim().ToLower();
                baseQuery = baseQuery.Where(x =>
                    x.ai.Title.ToLower().Contains(q) || (x.DelegationName != null && x.DelegationName.ToLower().Contains(q)));
            }

            // "Chưa làm" gộp cả IN_PROGRESS (dữ liệu cũ) — UI chỉ cho chọn tay TODO/DONE, không hiển thị
            // IN_PROGRESS như 1 lựa chọn filter riêng.
            var status = (request.Status ?? "ALL").Trim().ToUpperInvariant();
            baseQuery = status switch
            {
                "TODO" => baseQuery.Where(x => x.ai.Status == "TODO" || x.ai.Status == "IN_PROGRESS"),
                "DONE" => baseQuery.Where(x => x.ai.Status == "DONE"),
                "CANCELLED" => baseQuery.Where(x => x.ai.Status == "CANCELLED"),
                _ => baseQuery,
            };

            if (DateTime.TryParse(request.FromDate, out var fromDate))
                baseQuery = baseQuery.Where(x => x.ai.DueDate != null && x.ai.DueDate >= fromDate);
            if (DateTime.TryParse(request.ToDate, out var toDate))
                baseQuery = baseQuery.Where(x => x.ai.DueDate != null && x.ai.DueDate <= toDate);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        bool desc = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        baseQuery = desc
            ? baseQuery.OrderByDescending(x => x.ai.DueDate).ThenByDescending(x => x.ai.ActionItemId)
            : baseQuery.OrderBy(x => x.ai.DueDate).ThenBy(x => x.ai.ActionItemId);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = AllowedPageSizes.Contains(request.PageSize) ? request.PageSize : 10;

        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MyActionItemDto
            {
                ActionItemId = x.ai.ActionItemId,
                Title = x.ai.Title,
                Note = x.ai.Note,
                DueDate = x.ai.DueDate,
                Status = x.ai.Status,
                VisitInstanceId = x.VisitInstanceId,
                VisitRequestId = x.VisitRequestId,
                DelegationName = x.DelegationName ?? "Đoàn khách",
            })
            .ToListAsync(cancellationToken);

        return new GetMyActionItemsDto { Items = items, TotalCount = totalCount };
    }
}
