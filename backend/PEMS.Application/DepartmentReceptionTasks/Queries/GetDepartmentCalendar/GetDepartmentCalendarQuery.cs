using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
using PEMS.Application.Delegations.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Queries.GetDepartmentCalendar
{
    public class GetDepartmentCalendarQuery : IRequest<List<DepartmentCalendarItemDto>>
    {
        public string Month { get; set; } // Format: YYYY-MM
    }

    public class DepartmentCalendarItemDto
    {
        public ulong Id { get; set; }
        public string ItemType { get; set; } // INVITATION, REQUEST, PERSONAL
        public string Title { get; set; }
        public string FullTitle { get; set; }
        public string Date { get; set; } // YYYY-MM-DD
        public string StartAt { get; set; } // HH:mm
        public string EndAt { get; set; } // HH:mm
        public string Status { get; set; }
        public string LatestAttemptStatus { get; set; }
        public ulong? VisitInstanceId { get; set; }
        public ulong? VisitRequestId { get; set; }
        public ulong? LogisticsItemId { get; set; }
        /// <summary>Hàng participant được HIỂN THỊ cho ô lịch này.</summary>
        public ulong? ParticipantId { get; set; }
        /// <summary>
        /// Hàng participant của CHÍNH người đang đăng nhập, nếu có — id phải gửi lên khi đồng ý/từ chối
        /// thư mời. <see cref="ParticipantId"/> có thể là hàng của người khác (nhân viên được giao).
        /// </summary>
        public ulong? CurrentUserParticipantId { get; set; }
        public string DelegationName { get; set; }
        public ulong? RelatedUserId { get; set; }
        public string SenderName { get; set; }
        public string? CancelReason { get; set; }
        public string? Description { get; set; }
    }

    public class GetDepartmentCalendarQueryHandler : IRequestHandler<GetDepartmentCalendarQuery, List<DepartmentCalendarItemDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetDepartmentCalendarQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<DepartmentCalendarItemDto>> Handle(GetDepartmentCalendarQuery request, CancellationToken cancellationToken)
        {
            var items = new List<DepartmentCalendarItemDto>();
            if (string.IsNullOrEmpty(request.Month)) return items;

            DateTime startDate;
            DateTime endDate;
            var parts = request.Month.Split('-');
            if (parts.Length == 1 && int.TryParse(parts[0], out int yearOnly))
            {
                startDate = new DateTime(yearOnly, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                endDate = startDate.AddYears(1);
            }
            else if (parts.Length == 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
            {
                startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                endDate = startDate.AddMonths(1);
            }
            else
            {
                return items;
            }

            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || user.DepartmentId == null) return items;
            ulong deptId = user.DepartmentId.Value;
            var isDepartmentStaff = string.Equals(_currentUserService.RoleCode, RoleCodes.Department, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_currentUserService.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase);

            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DepartmentId == deptId, cancellationToken);
            var leaderUserId = department?.HeadUserId;

            // 1. INVITATIONS (visit_participants) — mỗi visit_instance chỉ đại diện bởi 1 dòng
            // (ưu tiên staff đang phụ trách, rồi tới staff vừa từ chối, rồi tới bản ghi leader)
            // để tránh hiện trùng nhiều dòng khi phòng ban đã ủy quyền qua lại nhiều lượt.
            var invitationsQuery = from p in _context.VisitParticipants
                                   join u in _context.Users on p.UserId equals u.UserId
                                   join r in _context.Roles on u.RoleId equals r.RoleId
                                   join c in _context.VisitRequestCampuses on p.VisitInstanceId equals c.VisitInstanceId
                                   join vr in _context.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                                   where u.DepartmentId == deptId
                                         && r.RoleCode == "DEPARTMENT"
                                         && p.Status != "REMOVED"
                                         && c.PlannedStartAt >= startDate
                                         && c.PlannedStartAt < endDate
                                   // EffectiveDelegationName: a MIXED per-campus v2 request shows THIS
                                   // instance's detail (no global fallback); v1/non-mixed keep the projection.
                                   select new
                                   {
                                       p, u, r, c, vr,
                                       EffectiveDelegationName = c.FormDetail != null ? c.FormDetail.DelegationName : null,
                                   };
            var invitationsList = await invitationsQuery.ToListAsync(cancellationToken);
            var senderIds = invitationsList.Where(x => x.p.InvitedBy != null).Select(x => x.p.InvitedBy).Distinct().ToList();
            var senders = await _context.Users.Where(u => senderIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var invitationGroups = invitationsList
                .GroupBy(x => x.p.VisitInstanceId)
                .Select(g =>
                {
                    var activeStaff = g
                        .Where(x => x.p.AssignedBy != null && x.p.Status != "DECLINED" && x.p.Status != "REMOVED")
                        .OrderByDescending(x => x.p.AssignedAt ?? x.p.InvitedAt ?? x.p.CreatedAt)
                        .FirstOrDefault();
                    var leaderRow = g
                        .Where(x => leaderUserId != null && x.p.UserId == leaderUserId.Value)
                        .OrderByDescending(x => x.p.InvitedAt ?? x.p.CreatedAt)
                        .FirstOrDefault();
                    var nonDeclined = g
                        .Where(x => x.p.Status != "DECLINED" && x.p.Status != "REMOVED")
                        .OrderByDescending(x => x.p.InvitedAt ?? x.p.CreatedAt)
                        .FirstOrDefault();
                    var declinedStaff = g
                        .Where(x => x.p.AssignedBy != null && x.p.Status == "DECLINED")
                        .OrderByDescending(x => x.p.RespondedAt ?? x.p.AssignedAt ?? x.p.CreatedAt)
                        .FirstOrDefault();
                    // The row this calendar entry DISPLAYS, and — separately — the caller's own row.
                    // One entry per instance means the displayed row is often somebody else's (the
                    // delegated staff member), and Accept/Decline is about the caller's row, not that
                    // one. See VisitInvitationResponse: answering a colleague's invitation is refused.
                    var currentUserRow = g
                        .Where(x => x.p.UserId == userId)
                        .OrderBy(x => VisitParticipantRelation.LivenessRank(x.p.Status))
                        .ThenByDescending(x => x.p.ParticipantId)
                        .FirstOrDefault();
                    var display = activeStaff ?? leaderRow ?? nonDeclined ?? declinedStaff ?? g.First();
                    return new { display, currentUserRow };
                })
                .ToList();

            foreach (var group in invitationGroups)
            {
                var item = group.display;
                var camp = item.c;
                string senderName = item.p.InvitedBy != null && senders.ContainsKey(item.p.InvitedBy.Value) ? senders[item.p.InvitedBy.Value] : "Hệ thống";

                var status = NormalizeInvitationStatus(
                    item.p.Status,
                    item.p.AssignedBy != null,
                    camp.Status,
                    camp.PlannedStartAt,
                    camp.PlannedEndAt,
                    VietnamTime.Now());

                items.Add(new DepartmentCalendarItemDto
                {
                    Id = item.p.ParticipantId,
                    ItemType = "INVITATION",
                    Title = "Thư mời: " + item.EffectiveDelegationName,
                    FullTitle = "Thư mời: " + item.EffectiveDelegationName,
                    Date = camp.PlannedStartAt.ToString("yyyy-MM-dd"),
                    StartAt = camp.PlannedStartAt.ToString("o"),
                    EndAt = camp.PlannedEndAt.ToString("o"),
                    Status = status,
                    VisitInstanceId = camp.VisitInstanceId, // This is visit_instance_id ! SQL says vrc.visit_instance_id
                    VisitRequestId = camp.VisitRequestId,
                    ParticipantId = item.p.ParticipantId,
                    CurrentUserParticipantId = group.currentUserRow?.p.ParticipantId,
                    DelegationName = item.EffectiveDelegationName,
                    RelatedUserId = item.p.UserId,
                    SenderName = senderName,
                    CancelReason = camp.CancellationReason ?? item.vr.CancellationReason
                });
            }

            // 2. REQUESTS (visit_logistics_items)
            var logisticsQuery = from l in _context.VisitLogisticsItems
                                 join c in _context.VisitRequestCampuses on l.VisitInstanceId equals c.VisitInstanceId
                                 join vr in _context.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                                 let startAt = l.UsageStartAt ?? c.PlannedStartAt
                                 let latestAttemptStatus = _context.VisitLogisticsAssignmentAttempts
                                     .Where(a => a.LogisticsItemId == l.LogisticsItemId)
                                     .OrderByDescending(a => a.AssignedAt)
                                     .Select(a => a.Status)
                                     .FirstOrDefault()
                                 let borrowSigned = _context.VisitLogisticsItemHandovers
                                     .Any(h => h.LogisticsItemId == l.LogisticsItemId
                                               && h.HandoverType == "BORROW"
                                               && h.BorrowerSignedAt != null
                                               && h.ProviderSignedAt != null)
                                 let returnSigned = _context.VisitLogisticsItemHandovers
                                     .Any(h => h.LogisticsItemId == l.LogisticsItemId
                                               && h.HandoverType == "RETURN"
                                               && h.BorrowerSignedAt != null
                                               && h.ProviderSignedAt != null)
                                 where l.RequestedToDepartmentId == deptId
                                       && startAt >= startDate
                                       && startAt < endDate
                                 select new
                                 {
                                     l, c, vr, startAt, latestAttemptStatus, borrowSigned, returnSigned,
                                     EffectiveDelegationName = c.FormDetail != null ? c.FormDetail.DelegationName : null,
                                 };
            
            var logisticsList = await logisticsQuery.ToListAsync(cancellationToken);
            var reqIds = logisticsList.Where(x => x.l.RequestedBy != null).Select(x => x.l.RequestedBy).Distinct().ToList();
            var requesters = await _context.Users.Where(u => reqIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName);

            foreach (var item in logisticsList)
            {
                var l = item.l;
                var endAt = l.UsageEndAt ?? item.c.PlannedEndAt;
                string senderName = l.RequestedBy != null && requesters.ContainsKey(l.RequestedBy.Value) ? requesters[l.RequestedBy.Value] : "Hệ thống";
                
                items.Add(new DepartmentCalendarItemDto
                {
                    Id = l.LogisticsItemId,
                    ItemType = "REQUEST",
                    Title = "Yêu cầu: " + l.Title,
                    FullTitle = "Đơn yêu cầu hỗ trợ: " + l.Title,
                    Date = item.startAt.ToString("yyyy-MM-dd"),
                    StartAt = item.startAt.ToString("o"),
                    EndAt = endAt.ToString("o"),
                    Status = NormalizeRequestStatus(l.Status, item.c.Status, item.vr.Status, item.borrowSigned, item.returnSigned),
                    LatestAttemptStatus = item.latestAttemptStatus ?? "",
                    VisitInstanceId = item.c.VisitInstanceId,
                    VisitRequestId = item.c.VisitRequestId,
                    LogisticsItemId = l.LogisticsItemId,
                    DelegationName = item.EffectiveDelegationName ?? "N/A",
                    RelatedUserId = l.AssignedToUserId ?? l.ReceivedBy ?? l.UpdatedBy,
                    SenderName = senderName,
                    CancelReason = item.c.CancellationReason ?? item.vr.CancellationReason,
                    Description = l.Description
                });
            }

            // 3. PERSONAL (calendar_events)
            var events = await _context.CalendarEvents
                .Where(e => e.OwnerUserId == userId
                            && e.Status == "ACTIVE"
                            && e.StartAt >= startDate
                            && e.StartAt < endDate)
                .ToListAsync(cancellationToken);

            foreach (var e in events)
            {
                items.Add(new DepartmentCalendarItemDto
                {
                    Id = e.CalendarEventId,
                    ItemType = "PERSONAL",
                    Title = e.Title,
                    FullTitle = e.Title + (string.IsNullOrEmpty(e.Description) ? "" : " - " + e.Description),
                    Date = e.StartAt.ToString("yyyy-MM-dd"),
                    StartAt = e.StartAt.ToString("o"),
                    EndAt = e.EndAt.ToString("o"),
                    Status = e.Status
                });
            }

            return items;
        }

        private static string NormalizeRequestStatus(string status, string instanceStatus, string requestStatus, bool borrowSigned, bool returnSigned)
        {
            if (requestStatus == "CANCELLED" || instanceStatus == "CANCELLED" || status == "CANCELLED") return "CANCELLED";
            if (returnSigned || status == "DONE") return "DONE";
            if (borrowSigned || status == "IN_PROGRESS") return "IN_PROGRESS";
            return status;
        }

        private static string NormalizeInvitationStatus(string status, bool isStaffAssignment, string instanceStatus, DateTime startAt, DateTime endAt, DateTime now)
        {
            if (instanceStatus == "CANCELLED") return "CANCELLED";
            if (status == "INVITED") return "REQUESTED";
            if (status == "ASSIGNED") return "ASSIGNED";
            if (status == "DECLINED") return isStaffAssignment ? "DECLINED" : "REJECTED";
            if (status == "ACCEPTED")
            {
                if (instanceStatus == "CLOSED" || now > endAt) return "DONE";
                if (now >= startAt && now <= endAt) return "IN_PROGRESS";
                return "ACCEPTED";
            }
            return status;
        }
    }
}
