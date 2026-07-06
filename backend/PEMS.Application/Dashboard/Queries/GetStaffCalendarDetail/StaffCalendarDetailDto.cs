using System;
using System.Collections.Generic;
using PEMS.Application.Dashboard.Queries.GetStaffCalendar;

namespace PEMS.Application.Dashboard.Queries.GetStaffCalendarDetail;

/// <summary>Một phản hồi tham gia (đã nhận / đã từ chối) của thành phần hỗ trợ trong đoàn.</summary>
public sealed class StaffCalendarParticipantResponseDto
{
    public ulong ParticipantId { get; set; }
    public ulong UserId { get; set; }
    public string FullName { get; set; } = default!;
    /// <summary>IC_HOST | IC_SUPPORT | DEPT_SUPPORT | STUDENT.</summary>
    public string ParticipantRole { get; set; } = default!;
    /// <summary>INVITED | ACCEPTED | DECLINED | ASSIGNED.</summary>
    public string Status { get; set; } = default!;
    public DateTime? RespondedAt { get; set; }
    /// <summary>Ghi chú/lý do (đặc biệt là lý do từ chối nếu có).</summary>
    public string? Note { get; set; }
}

/// <summary>Chi tiết yêu cầu đến thăm cho modal bảng lịch (read-only, compact).</summary>
public sealed class StaffCalendarDetailDto
{
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string? RequestCode { get; set; }
    public string? DelegationName { get; set; }
    public string? VisitScope { get; set; }
    public string RequestStatus { get; set; } = default!;
    public string? CampusStatus { get; set; }
    public string DisplayStatus { get; set; } = default!;
    public string ColorType { get; set; } = default!;

    public ulong CampusId { get; set; }
    public string CampusName { get; set; } = default!;
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }

    // ── Người đăng ký / tổ chức ──
    public string? RegistrantFullName { get; set; }
    public string? RegistrantOrganization { get; set; }
    public string? RegistrantJobTitle { get; set; }
    public string? RegistrantNationality { get; set; }
    public string? RegistrantPhone { get; set; }
    public string? RegistrantEmail { get; set; }

    // ── Đầu mối liên hệ ──
    public string? ContactPersonFullName { get; set; }
    public string? ContactPersonPhone { get; set; }
    public string? ContactPersonEmail { get; set; }

    // ── Nội dung chuyến thăm ──
    public string? Purpose { get; set; }
    public string? WorkingContent { get; set; }
    public string? VisitType { get; set; }
    public string? VisitTypeOther { get; set; }
    public int GuestCount { get; set; }
    public string? WorkingLanguage { get; set; }
    public string? MediaConsentStatus { get; set; }
    public string? MediaConsentNote { get; set; }
    /// <summary>Free text the guest entered to identify the transportation to FPTU.</summary>
    public string? TransportationNote { get; set; }
    public string? NoteToFptu { get; set; }

    // ── Host hiện tại ──
    public ulong? CurrentHostUserId { get; set; }
    public string? CurrentHostName { get; set; }
    public string? CurrentHostEmail { get; set; }
    public DateTime? HostAssignedAt { get; set; }
    public string? HostAssignedByName { get; set; }
    public bool IsCurrentHost { get; set; }

    // ── Quyết định duyệt/từ chối (nếu có) ──
    public string? DecisionNote { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }

    // ── Hủy (nếu có) ──
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    public bool IsPast { get; set; }
    public bool IsExpired { get; set; }

    /// <summary>Phản hồi của các thành phần hỗ trợ (đã nhận / đã từ chối) — nếu có dữ liệu.</summary>
    public List<StaffCalendarParticipantResponseDto> ParticipantResponses { get; set; } = new();

    public StaffCalendarAllowedActionsDto AllowedActions { get; set; } = new();
}
