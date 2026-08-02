using System;
using System.Collections.Generic;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// What one template variable means, in both languages, plus a sample value safe to show in a preview.
/// </summary>
public sealed record EmailVariableDescriptor(
    string Name,
    string LabelVi,
    string LabelEn,
    string SampleVi,
    string SampleEn);

/// <summary>
/// The label and preview-sample for every variable the system template registry declares — one table,
/// owned by the backend (G11-J).
///
/// <para>
/// This replaces a hard-coded list that used to live in the template-management screen. That list held
/// eleven names, five of them "common" and six "logistics", and it was applied to whichever template the
/// operator happened to open. The consequences were not cosmetic: opening
/// <c>ACCOUNT_EMAIL_CONFIRMATION</c> — whose real variables are fullName, roleName, campusName and
/// expiresInHours — matched none of them, so a canonical, untouched template greeted the operator with
/// "Một số biến chưa được định nghĩa hoặc sai định dạng" for every variable it legitimately used, while
/// the sidebar offered logistics variables that template can never receive a value for.
/// </para>
///
/// <para>
/// Preview samples live here rather than in the frontend for the same reason the renderer is shared
/// between preview and send: a preview substituted by different values, from a different table, is not
/// a preview of anything. Nothing in this file is a secret — the OTP sample is a fixed, obviously-fake
/// string, because a preview must never mint a real one.
/// </para>
/// </summary>
public static class EmailVariableCatalog
{
    private static readonly IReadOnlyDictionary<string, EmailVariableDescriptor> ByName =
        new[]
        {
            // ── people ───────────────────────────────────────────────────────────
            V("fullName", "Họ tên người nhận", "Recipient full name", "Nguyễn Văn An", "Nguyen Van An"),
            V("recipientName", "Tên người nhận", "Recipient name", "Nguyễn Văn An", "Nguyen Van An"),
            V("contactFullName", "Họ tên đầu mối", "Primary contact full name", "Trần Thị Bình", "Tran Thi Binh"),
            V("currentContactName", "Đầu mối hiện tại", "Current contact", "Lê Văn Cường", "Le Van Cuong"),
            V("hostName", "Người chủ trì", "Host name", "Phạm Thị Dung", "Pham Thi Dung"),
            V("hostEmail", "Email người chủ trì", "Host email", "dungpt@fpt.edu.vn", "dungpt@fpt.edu.vn"),
            V("assigneeName", "Người được phân công", "Assignee name", "Đỗ Văn Em", "Do Van Em"),
            V("requesterName", "Người gửi yêu cầu", "Requester name", "Vũ Thị Giang", "Vu Thi Giang"),
            V("departmentLeaderName", "Trưởng phòng ban", "Department leader", "Hoàng Văn Hải", "Hoang Van Hai"),
            V("successorName", "Người kế nhiệm", "Successor name", "Bùi Thị Lan", "Bui Thi Lan"),
            V("personName", "Tên nhân sự", "Person name", "Nguyễn Văn An", "Nguyen Van An"),

            // ── organisation ─────────────────────────────────────────────────────
            V("campusName", "Cơ sở", "Campus", "FPTU Hà Nội", "FPTU Hanoi"),
            V("departmentName", "Phòng ban", "Department", "Phòng Công tác sinh viên", "Student Affairs Office"),
            V("delegationName", "Tên đoàn", "Delegation name", "Đoàn Đại học Kyoto", "Kyoto University Delegation"),
            V("roleName", "Vai trò", "Role", "Cán bộ", "Staff"),
            V("oldRoleName", "Vai trò cũ", "Previous role", "Cán bộ", "Staff"),
            V("newRoleName", "Vai trò mới", "New role", "Trưởng bộ phận", "Staff Leader"),
            V("roleLabel", "Vai trò trong chuyến thăm", "Role in the visit", "Thành viên tiếp đón", "Host team member"),
            V("scopeLabel", "Phạm vi báo cáo", "Report scope", "Toàn bộ cơ sở Hà Nội", "All Hanoi campuses"),

            // ── request / visit ──────────────────────────────────────────────────
            V("requestCode", "Mã yêu cầu", "Request code", "VR-2026-0142", "VR-2026-0142"),
            V("plannedTime", "Thời gian dự kiến", "Planned time", "09:00 ngày 20/08/2026", "20 Aug 2026, 09:00"),
            V("plannedStart", "Bắt đầu dự kiến", "Planned start", "09:00 ngày 20/08/2026", "20 Aug 2026, 09:00"),
            V("plannedEnd", "Kết thúc dự kiến", "Planned end", "11:30 ngày 20/08/2026", "20 Aug 2026, 11:30"),
            V("hostMessage", "Lời nhắn của người chủ trì", "Message from the host",
              "Rất mong anh/chị thu xếp tham dự.", "We would be glad to have you join us."),

            // ── account lifecycle ────────────────────────────────────────────────
            V("effectiveDate", "Ngày hiệu lực", "Effective date", "01/09/2026", "1 Sep 2026"),
            V("reason", "Lý do", "Reason", "Điều chuyển công tác", "Internal reassignment"),
            V("oldEmailMasked", "Email cũ (đã che)", "Previous email (masked)", "n***@fpt.edu.vn", "n***@fpt.edu.vn"),
            V("expiresInHours", "Hiệu lực (giờ)", "Valid for (hours)", "24", "24"),
            V("expireMinutes", "Hiệu lực (phút)", "Valid for (minutes)", "10", "10"),

            // The only credential in the catalog. The sample is a fixed, obviously-fake string and is
            // never generated: a preview that minted a real code would be a preview that leaks one.
            V("otpCode", "Mã OTP", "OTP code", "000000", "000000"),

            // ── logistics ────────────────────────────────────────────────────────
            V("logisticsTitle", "Tên yêu cầu hậu cần", "Logistics request title",
              "Chuẩn bị phòng họp A201", "Prepare meeting room A201"),
            V("logisticsItemType", "Loại hạng mục", "Item type", "Thiết bị trình chiếu", "Presentation equipment"),
            V("itemTitle", "Tên hạng mục", "Item title", "Máy chiếu Epson EB-2250U", "Epson EB-2250U projector"),
            V("quantity", "Số lượng", "Quantity", "2", "2"),
            V("originalQuantity", "Số lượng đề nghị ban đầu", "Originally requested quantity", "2", "2"),
            V("proposedQuantity", "Số lượng đề xuất", "Proposed quantity", "1", "1"),
            V("usageStartAt", "Bắt đầu sử dụng", "Usage starts", "08:00 ngày 20/08/2026", "20 Aug 2026, 08:00"),
            V("usageEndAt", "Kết thúc sử dụng", "Usage ends", "11:00 ngày 20/08/2026", "20 Aug 2026, 11:00"),
            V("proposedUsageStartAt", "Bắt đầu đề xuất", "Proposed start", "09:00 ngày 20/08/2026", "20 Aug 2026, 09:00"),
            V("proposedUsageEndAt", "Kết thúc đề xuất", "Proposed end", "10:30 ngày 20/08/2026", "20 Aug 2026, 10:30"),
            V("proposedDescription", "Nội dung đề xuất", "Proposed change",
              "Dùng phòng A105 thay cho A201", "Use room A105 instead of A201"),
            V("proposalNote", "Ghi chú đề xuất", "Proposal note",
              "Phòng A201 đã có lịch trùng.", "Room A201 is already booked."),
            V("coordinationNote", "Ghi chú phối hợp", "Coordination note",
              "Liên hệ trước 1 ngày để nhận thiết bị.", "Please collect the equipment one day in advance."),
            V("dueAt", "Hạn xử lý", "Due at", "17:00 ngày 18/08/2026", "18 Aug 2026, 17:00"),

            // ── reporting ────────────────────────────────────────────────────────
            V("periodFrom", "Từ ngày", "Period from", "01/07/2026", "1 Jul 2026"),
            V("periodTo", "Đến ngày", "Period to", "31/07/2026", "31 Jul 2026"),
        }
        .ToDictionaryByName();

    private static EmailVariableDescriptor V(
        string name, string labelVi, string labelEn, string sampleVi, string sampleEn)
        => new(name, labelVi, labelEn, sampleVi, sampleEn);

    private static IReadOnlyDictionary<string, EmailVariableDescriptor> ToDictionaryByName(
        this IEnumerable<EmailVariableDescriptor> items)
    {
        var map = new Dictionary<string, EmailVariableDescriptor>(StringComparer.Ordinal);
        foreach (var item in items) map[item.Name] = item;
        return map;
    }

    /// <summary>The descriptor for a variable, or null when the catalog does not describe it.</summary>
    public static EmailVariableDescriptor? Find(string name)
        => ByName.TryGetValue(name, out var d) ? d : null;

    /// <summary>Every described variable name. A contract test asserts this covers the whole registry.</summary>
    public static IReadOnlyCollection<string> AllNames => (IReadOnlyCollection<string>)ByName.Keys;

    /// <summary>The preview sample for a variable in the given language, or the name itself if unknown.</summary>
    public static string Sample(string name, string language)
    {
        var d = Find(name);
        if (d is null) return name;
        return language == EmailLanguages.En ? d.SampleEn : d.SampleVi;
    }

    /// <summary>The human label for a variable in the given language.</summary>
    public static string Label(string name, string language)
    {
        var d = Find(name);
        if (d is null) return name;
        return language == EmailLanguages.En ? d.LabelEn : d.LabelVi;
    }
}
