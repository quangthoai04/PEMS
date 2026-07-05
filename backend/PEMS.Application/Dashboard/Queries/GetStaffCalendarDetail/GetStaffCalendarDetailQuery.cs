using MediatR;

namespace PEMS.Application.Dashboard.Queries.GetStaffCalendarDetail;

/// <summary>
/// Chi tiết một yêu cầu đến thăm (campus visit instance) cho modal trên bảng lịch dashboard
/// Staff/Staff Leader. Scope: STAFF + LEADER hoặc STAFF + STAFF, đúng campus (hoặc là host
/// của instance). Kèm action flags backend-computed.
/// </summary>
public sealed record GetStaffCalendarDetailQuery(ulong VisitInstanceId)
    : IRequest<StaffCalendarDetailDto>;
