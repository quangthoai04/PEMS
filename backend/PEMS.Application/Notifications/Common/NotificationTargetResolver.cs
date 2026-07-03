using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Notifications.Common;

public class NotificationTargetResolver : INotificationTargetResolver
{
    private readonly IApplicationDbContext _context;

    public NotificationTargetResolver(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(string? targetUrl, bool canOpen, string? disabledReason)> ResolveTargetAsync(
        ulong currentUserId, 
        string notificationType, 
        string? relatedType, 
        ulong? relatedId, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(relatedType) || !relatedId.HasValue)
        {
            return (null, false, "Thông báo hệ thống hoặc không có thông tin chi tiết.");
        }

        string? targetUrl = null;
        bool canOpen = true;
        string? disabledReason = null;

        var currentUserEntity = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);
        bool isDept = currentUserEntity?.Role?.RoleCode == "DEPARTMENT";
        ulong? deptId = currentUserEntity?.DepartmentId;

        try
        {
            switch (relatedType)
            {
                case "VISIT_REQUEST":
                    var request = await _context.VisitRequests.FirstOrDefaultAsync(r => r.VisitRequestId == relatedId.Value, cancellationToken);
                    if (request == null)
                    {
                        canOpen = false;
                        disabledReason = "Yêu cầu đoàn thăm không còn tồn tại hoặc đã bị xóa.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/visit";
                    }
                    break;

                case "VISIT_INSTANCE":
                    var instance = await _context.VisitRequestCampuses.FirstOrDefaultAsync(i => i.VisitInstanceId == relatedId.Value, cancellationToken);
                    if (instance == null)
                    {
                        canOpen = false;
                        disabledReason = "Chuyến thăm không còn tồn tại hoặc đã bị xóa.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/visit/process/{relatedId}";
                    }
                    break;

                case "VISIT_PARTICIPANT":
                case "PARTICIPATION":
                    var participant = await _context.VisitParticipants.FirstOrDefaultAsync(p => p.ParticipantId == relatedId.Value, cancellationToken);
                    if (participant == null)
                    {
                        canOpen = false;
                        disabledReason = "Người tham gia không còn tồn tại hoặc đã bị xóa khỏi đoàn.";
                    }
                    else
                    {
                        if (isDept && deptId.HasValue)
                            targetUrl = $"/dashboard/departments/{deptId.Value}/invitations/{relatedId}";
                        else
                            targetUrl = $"/dashboard/visit/process/{participant.VisitInstanceId}?tab=participants&participantId={relatedId}";
                    }
                    break;

                case "LOGISTICS_ITEM":
                    var logisticsItem = await _context.VisitLogisticsItems.FirstOrDefaultAsync(l => l.LogisticsItemId == relatedId.Value, cancellationToken);
                    if (logisticsItem == null)
                    {
                        canOpen = false;
                        disabledReason = "Nhiệm vụ logistics không còn tồn tại hoặc đã bị xóa.";
                    }
                    else
                    {
                        if (isDept && deptId.HasValue)
                            targetUrl = $"/dashboard/departments/{deptId.Value}/tasks/{relatedId}";
                        else
                            targetUrl = $"/dashboard/visit/process/{logisticsItem.VisitInstanceId}?tab=logistics&itemId={relatedId}";
                    }
                    break;

                case "LOGISTICS_HANDOVER":
                    var handover = await _context.VisitLogisticsItemHandovers
                        .Include(h => h.LogisticsItem)
                        .FirstOrDefaultAsync(h => h.HandoverId == relatedId.Value, cancellationToken);
                    if (handover == null || handover.LogisticsItem == null)
                    {
                        canOpen = false;
                        disabledReason = "Biên bản bàn giao hoặc nhiệm vụ logistics không còn tồn tại.";
                    }
                    else
                    {
                        if (isDept && deptId.HasValue)
                            targetUrl = $"/dashboard/departments/{deptId.Value}/tasks/{handover.LogisticsItemId}";
                        else
                            targetUrl = $"/dashboard/visit/process/{handover.LogisticsItem.VisitInstanceId}?tab=logistics&handoverId={relatedId}";
                    }
                    break;

                case "AGENDA":
                    var agenda = await _context.VisitAgendas.FirstOrDefaultAsync(a => a.AgendaId == relatedId.Value, cancellationToken);
                    if (agenda == null)
                    {
                        canOpen = false;
                        disabledReason = "Lịch trình không còn tồn tại hoặc đã bị xóa.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/visit/process/{agenda.VisitInstanceId}?tab=agenda&agendaId={relatedId}";
                    }
                    break;

                case "MINUTES":
                    var minute = await _context.Minutes.FirstOrDefaultAsync(m => m.MinutesId == relatedId.Value, cancellationToken);
                    if (minute == null)
                    {
                        canOpen = false;
                        disabledReason = "Biên bản không còn tồn tại hoặc đã bị xóa.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/minutes";
                    }
                    break;

                case "MINUTE_ACTION_ITEM":
                    var actionItem = await _context.MinuteActionItems
                        .Include(a => a.Minute)
                        .FirstOrDefaultAsync(a => a.ActionItemId == relatedId.Value, cancellationToken);
                    if (actionItem == null || actionItem.Minute == null)
                    {
                        canOpen = false;
                        disabledReason = "Hành động hoặc biên bản không còn tồn tại.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/minutes";
                    }
                    break;

                case "NEWS":
                    var news = await _context.News.FirstOrDefaultAsync(n => n.NewsId == relatedId.Value, cancellationToken);
                    if (news == null)
                    {
                        canOpen = false;
                        disabledReason = "Bản tin không còn tồn tại hoặc đã bị xóa.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/news/{relatedId}";
                    }
                    break;

                case "PARTNER":
                    var partner = await _context.Partners.FirstOrDefaultAsync(p => p.PartnerId == relatedId.Value, cancellationToken);
                    if (partner == null)
                    {
                        canOpen = false;
                        disabledReason = "Đối tác không còn tồn tại hoặc đã bị xóa.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/partners/{relatedId}";
                    }
                    break;

                case "CALENDAR_EVENT":
                    var evt = await _context.CalendarEvents.FirstOrDefaultAsync(e => e.CalendarEventId == relatedId.Value, cancellationToken);
                    if (evt == null)
                    {
                        canOpen = false;
                        disabledReason = "Sự kiện lịch không còn tồn tại hoặc đã bị xóa.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/calendar?eventId={relatedId}";
                    }
                    break;

                case "ACCOUNT":
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == relatedId.Value, cancellationToken);
                    if (user == null)
                    {
                        canOpen = false;
                        disabledReason = "Tài khoản không còn tồn tại.";
                    }
                    else
                    {
                        targetUrl = $"/dashboard/accounts/{relatedId}";
                    }
                    break;

                case "SYSTEM":
                default:
                    canOpen = false;
                    disabledReason = "Không có đường dẫn chi tiết cho thông báo hệ thống.";
                    break;
            }
        }
        catch (Exception)
        {
            canOpen = false;
            targetUrl = null;
            disabledReason = "Nội dung liên quan không còn tồn tại hoặc đã bị hủy.";
        }

        return (targetUrl, canOpen, disabledReason);
    }
}
