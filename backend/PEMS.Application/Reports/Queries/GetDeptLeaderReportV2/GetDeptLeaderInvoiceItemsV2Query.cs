using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Shared;

namespace PEMS.Application.Reports.Queries.GetDeptLeaderReportV2;

/// <summary>
/// Danh sách đơn yêu cầu hậu cần phòng ban ĐÃ HOÀN THÀNH (DONE hoặc biên bản nghiệm thu
/// đã ký đủ 2 bên) trong khoảng ngày — dùng cho panel "Xuất hóa đơn" trên trang báo cáo
/// của Department Leader. Kèm chữ ký biên bản bàn giao/nghiệm thu để mở modal biên bản.
/// </summary>
public sealed class GetDeptLeaderInvoiceItemsV2Query : IRequest<List<DeptLeaderInvoiceItemV2Dto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class DeptLeaderInvoiceItemV2Dto
{
    public ulong LogisticsItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestCode { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    public DateTime UsageStartAt { get; set; }
    public DateTime UsageEndAt { get; set; }
    public string? HostName { get; set; }
    public string? AssigneeName { get; set; }
    public string? BorrowNote { get; set; }
    public string? ReturnNote { get; set; }
    public DeptLeaderInvoiceSignatureV2Dto? BorrowProviderSignature { get; set; }
    public DeptLeaderInvoiceSignatureV2Dto? BorrowBorrowerSignature { get; set; }
    public DeptLeaderInvoiceSignatureV2Dto? ReturnProviderSignature { get; set; }
    public DeptLeaderInvoiceSignatureV2Dto? ReturnBorrowerSignature { get; set; }
}

public sealed class DeptLeaderInvoiceSignatureV2Dto
{
    public string? Name { get; set; }
    public string? SignedAt { get; set; }
}

public sealed class GetDeptLeaderInvoiceItemsV2QueryHandler
    : IRequestHandler<GetDeptLeaderInvoiceItemsV2Query, List<DeptLeaderInvoiceItemV2Dto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDeptLeaderInvoiceItemsV2QueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<DeptLeaderInvoiceItemV2Dto>> Handle(GetDeptLeaderInvoiceItemsV2Query request, CancellationToken cancellationToken)
    {
        var deptId = DeptLeaderReportV2Guard.RequireDepartmentLeader(_currentUser);
        var nowVn = VietnamTime.Now();
        var (fromVn, toVnExclusive) = DeptLeaderReportV2Guard.ResolvePeriodVn(
            "CUSTOM", request.FromDate, request.ToDate ?? nowVn, nowVn);

        var rows = await (
                from li in _db.VisitLogisticsItems.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on li.VisitInstanceId equals ci.VisitInstanceId
                let startAt = li.UsageStartAt ?? ci.PlannedStartAt
                where li.RequestedToDepartmentId == deptId
                      // Chỉ đơn ĐÃ HOÀN THÀNH: status DONE hoặc biên bản nghiệm thu (RETURN) đã ký đủ 2 bên.
                      && (li.Status == LogisticsItemStatus.Done
                          || _db.VisitLogisticsItemHandovers.Any(h => h.LogisticsItemId == li.LogisticsItemId
                              && h.HandoverType == "RETURN"
                              && h.BorrowerSignedAt != null && h.ProviderSignedAt != null))
                      && startAt >= fromVn && startAt < toVnExclusive
                orderby startAt
                select new
                {
                    li.LogisticsItemId,
                    li.Title,
                    li.ItemType,
                    li.Quantity,
                    li.Status,
                    ci.VisitRequest.RequestCode,
                    // Instance row: mixed v2 shows THIS instance's detail name.
                    DelegationName = ci.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                                     && ci.VisitRequest.HasMixedCampusDetails
                        ? (ci.FormDetail != null ? ci.FormDetail.DelegationName : null)
                        : ci.VisitRequest.DelegationName,
                    StartAt = startAt,
                    EndAt = li.UsageEndAt ?? ci.PlannedEndAt,
                    HostId = ci.CurrentHostUserId,
                    AssigneeId = li.AssignedToUserId,
                })
            .ToListAsync(cancellationToken);

        var itemIds = rows.Select(r => r.LogisticsItemId).ToList();
        var handovers = await _db.VisitLogisticsItemHandovers.AsNoTracking()
            .Where(h => itemIds.Contains(h.LogisticsItemId))
            .Select(h => new
            {
                h.LogisticsItemId,
                h.HandoverType,
                h.ProviderSignedBy,
                h.ProviderSignedAt,
                h.BorrowerSignedBy,
                h.BorrowerSignedAt,
                h.ConditionNote,
            })
            .ToListAsync(cancellationToken);

        var userIds = rows.SelectMany(r => new[] { r.HostId, r.AssigneeId })
            .Concat(handovers.SelectMany(h => new[] { h.ProviderSignedBy, h.BorrowerSignedBy }))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);
        string? NameOf(ulong? id) => id != null && userNames.TryGetValue(id.Value, out var n) ? n : null;

        DeptLeaderInvoiceSignatureV2Dto? Sig(ulong? userId, DateTime? signedAt) =>
            userId == null || signedAt == null
                ? null
                : new DeptLeaderInvoiceSignatureV2Dto { Name = NameOf(userId) ?? $"User #{userId}", SignedAt = signedAt.Value.ToString("O") };

        return rows.Select(r =>
        {
            var borrow = handovers.FirstOrDefault(h => h.LogisticsItemId == r.LogisticsItemId && h.HandoverType == "BORROW");
            var ret = handovers.FirstOrDefault(h => h.LogisticsItemId == r.LogisticsItemId && h.HandoverType == "RETURN");
            return new DeptLeaderInvoiceItemV2Dto
            {
                LogisticsItemId = r.LogisticsItemId,
                Title = r.Title,
                ItemType = r.ItemType,
                Quantity = r.Quantity ?? 1,
                Status = r.Status,
                RequestCode = r.RequestCode ?? "",
                DelegationName = r.DelegationName ?? "",
                UsageStartAt = r.StartAt,
                UsageEndAt = r.EndAt,
                HostName = NameOf(r.HostId),
                AssigneeName = NameOf(r.AssigneeId),
                BorrowNote = borrow?.ConditionNote,
                ReturnNote = ret?.ConditionNote,
                BorrowProviderSignature = Sig(borrow?.ProviderSignedBy, borrow?.ProviderSignedAt),
                BorrowBorrowerSignature = Sig(borrow?.BorrowerSignedBy, borrow?.BorrowerSignedAt),
                ReturnProviderSignature = Sig(ret?.ProviderSignedBy, ret?.ProviderSignedAt),
                ReturnBorrowerSignature = Sig(ret?.BorrowerSignedBy, ret?.BorrowerSignedAt),
            };
        }).ToList();
    }
}
