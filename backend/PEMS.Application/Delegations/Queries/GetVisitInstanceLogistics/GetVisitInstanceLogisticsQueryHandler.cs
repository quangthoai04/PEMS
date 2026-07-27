using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Utils;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceLogistics;

public sealed class GetVisitInstanceLogisticsQueryHandler
    : IRequestHandler<GetVisitInstanceLogisticsQuery, GetVisitInstanceLogisticsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInstanceLogisticsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetVisitInstanceLogisticsResponse> Handle(
        GetVisitInstanceLogisticsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (!VisitReminderAccess.CanView(_currentUser, instance))
            throw new ForbiddenException("Bạn không có quyền xem danh sách yêu cầu hậu cần.");

        var items = await _db.VisitLogisticsItems
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .OrderByDescending(l => l.LogisticsItemId)
            .Select(l => new VisitInstanceLogisticsItemDto
            {
                LogisticsItemId = l.LogisticsItemId,
                ItemType = l.ItemType,
                Title = l.Title,
                Description = l.Description,
                Quantity = l.Quantity,
                Status = l.Status,
                Priority = l.Priority,
                CoordinationMode = l.CoordinationMode,
                OfflineCoordinationNote = l.OfflineCoordinationNote,
                RequestedToDepartmentId = l.RequestedToDepartmentId,
                RequestedBy = l.RequestedBy,
                AssignedToUserId = l.AssignedToUserId,
                AssigneeResponseNote = l.AssigneeResponseNote,
                DecisionNote = l.DecisionNote,
                ProposedQuantity = l.ProposedQuantity,
                ProposedDescription = l.ProposedDescription,
                ProposalNote = l.ProposalNote,
                ProposalResponse = l.ProposalResponse,
                ProposalResponseNote = l.ProposalResponseNote,
            })
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new GetVisitInstanceLogisticsResponse();

        // Enrich department + assignee names + format wall-clock times in-memory (avoid correlated
        // subqueries on optional FKs — Pomelo translation pitfall).
        var rows = await _db.VisitLogisticsItems
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .Select(l => new { l.LogisticsItemId, l.RequestedAt, l.UsageStartAt, l.UsageEndAt, l.DueAt, l.CompletedAt, l.ProposedUsageStartAt, l.ProposedUsageEndAt })
            .ToListAsync(cancellationToken);
        var timeById = rows.ToDictionary(r => r.LogisticsItemId);

        var deptIds = items.Where(i => i.RequestedToDepartmentId.HasValue)
            .Select(i => i.RequestedToDepartmentId!.Value).Distinct().ToList();
        var deptNames = deptIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Departments.Where(d => deptIds.Contains(d.DepartmentId))
                .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, cancellationToken);

        // Borrow/return handover signatures for these items, mapped in-memory below.
        var itemIds = items.Select(i => i.LogisticsItemId).ToList();
        var handovers = await _db.VisitLogisticsItemHandovers
            .Where(h => itemIds.Contains(h.LogisticsItemId))
            .Select(h => new
            {
                h.LogisticsItemId, h.HandoverType, h.BorrowerSignedBy, h.BorrowerSignedAt,
                h.ProviderSignedBy, h.ProviderSignedAt, h.ItemCondition, h.ConditionNote,
            })
            .ToListAsync(cancellationToken);

        // Resolve assignee + requester + handover-signer names in one batch (avoid correlated
        // subqueries — Pomelo pitfall).
        var userIds = items.Where(i => i.AssignedToUserId.HasValue).Select(i => i.AssignedToUserId!.Value)
            .Concat(items.Where(i => i.RequestedBy.HasValue).Select(i => i.RequestedBy!.Value))
            .Concat(handovers.Where(h => h.BorrowerSignedBy.HasValue).Select(h => h.BorrowerSignedBy!.Value))
            .Concat(handovers.Where(h => h.ProviderSignedBy.HasValue).Select(h => h.ProviderSignedBy!.Value))
            .Distinct().ToList();
        var userNames = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        string? NameOf(ulong? id) => id.HasValue && userNames.TryGetValue(id.Value, out var n) ? n : null;
        var handoversByItem = handovers
            .GroupBy(h => h.LogisticsItemId)
            .ToDictionary(g => g.Key, g => g.Select(h => new LogisticsHandoverDto
            {
                HandoverType = h.HandoverType,
                BorrowerSignedByName = NameOf(h.BorrowerSignedBy),
                BorrowerSignedAt = h.BorrowerSignedAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                ProviderSignedByName = NameOf(h.ProviderSignedBy),
                ProviderSignedAt = h.ProviderSignedAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                ItemCondition = h.ItemCondition,
                ConditionNote = h.ConditionNote,
            }).ToList());

        foreach (var i in items)
        {
            if (handoversByItem.TryGetValue(i.LogisticsItemId, out var hs))
            {
                // condition_note của dòng BORROW là envelope {rows, note} cho MỌI loại hạng mục — tách
                // checklist qua ChecklistJson để Host render đúng bảng, và ghi chú tự do (đã gộp Bên
                // giao/Bên nhận) qua ConditionNote.
                foreach (var h in hs.Where(h => h.HandoverType == "BORROW"))
                {
                    var rawConditionNote = h.ConditionNote;
                    h.ChecklistJson = VehicleHandoverChecklistNote.ExtractRowsJson(rawConditionNote);
                    h.ConditionNote = VehicleHandoverChecklistNote.ExtractNote(rawConditionNote);
                }
                i.Handovers = hs;
            }
            if (i.RequestedToDepartmentId.HasValue && deptNames.TryGetValue(i.RequestedToDepartmentId.Value, out var dn))
                i.DepartmentName = dn;
            if (i.AssignedToUserId.HasValue && userNames.TryGetValue(i.AssignedToUserId.Value, out var un))
                i.AssignedToName = un;
            if (i.RequestedBy.HasValue && userNames.TryGetValue(i.RequestedBy.Value, out var rn))
                i.RequestedByName = rn;
            if (timeById.TryGetValue(i.LogisticsItemId, out var t))
            {
                i.RequestedAt = t.RequestedAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.UsageStartAt = t.UsageStartAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.UsageEndAt = t.UsageEndAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.DueAt = t.DueAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.CompletedAt = t.CompletedAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.ProposedUsageStartAt = t.ProposedUsageStartAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.ProposedUsageEndAt = t.ProposedUsageEndAt?.ToString("yyyy-MM-ddTHH:mm:ss");
            }
        }

        return new GetVisitInstanceLogisticsResponse { Items = items };
    }
}
