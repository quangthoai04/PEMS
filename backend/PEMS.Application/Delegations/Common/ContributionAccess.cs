using System;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Chứa logic phân quyền truy cập Contribution dùng chung cho Department Leader/Staff
/// (đảm bảo đồng nhất rule giữa Contribution endpoint và assignments progress).
///
/// <para>
/// Quy tắc: quyền đóng góp đến từ việc THAM GIA TIẾP ĐOÀN, không đến từ nhiệm vụ hậu cần.
/// Đơn vị lo phòng họp / màn LED / xe chịu trách nhiệm cho hạng mục đó, không phải là thành phần
/// đón tiếp, nên không có gì để báo cáo ở trang đóng góp.
/// </para>
/// <para>
/// Vì vậy ở đây chỉ còn MỘT rule. Bản trước có thêm <c>IsDepartmentContributorForLogistics</c>
/// (assign + status ACCEPTED/IN_PROGRESS/DONE/COMPLETED); nó đã bị bỏ khỏi
/// <c>GetAssignmentsProgressList</c> nhưng vẫn còn hiệu lực ở
/// <c>GetVisitInstanceContributionQueryHandler</c>, khiến nút bị ẩn trong khi endpoint vẫn mở.
/// Method đó bị xóa hẳn thay vì để lại không dùng — để lại là mời gọi gắn lại đúng lỗ hổng vừa vá.
/// </para>
/// </summary>
public static class ContributionAccess
{
    /// <summary>
    /// Quyền đóng góp cho Thư mời (Invitation) — rule DUY NHẤT cấp quyền đóng góp.
    /// </summary>
    public static bool IsDepartmentContributorForInvitation(ulong userId, ulong participantUserId, string status)
    {
        if (participantUserId != userId)
            return false;
        
        // Theo VisitInstanceAccess và Contribution rule: ACCEPTED hoặc ASSIGNED
        return status is "ACCEPTED" or "ASSIGNED";
    }
}
