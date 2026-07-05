using System;
using MediatR;

namespace PEMS.Application.Dashboard.Queries.GetStaffCalendar;

/// <summary>
/// Dashboard bảng lịch cho STAFF (Staff Leader + Staff thường).
/// ViewMode:
///   • "office" — Lịch văn phòng: mọi yêu cầu đến thăm thuộc campus của user.
///   • "mine"   — Lịch của tôi: chỉ các yêu cầu tham quan mà user hiện tại là host
///                (visit_request_campuses.current_host_user_id = userId).
/// Khoảng thời gian [From, To] là bắt buộc (theo tháng/tuần/ngày đang hiển thị).
/// Scope được siết ở handler — frontend không bao giờ tự lọc dữ liệu nhạy cảm.
/// </summary>
public sealed record GetStaffCalendarQuery(
    string? ViewMode,
    DateTime From,
    DateTime To) : IRequest<StaffCalendarResponse>;
