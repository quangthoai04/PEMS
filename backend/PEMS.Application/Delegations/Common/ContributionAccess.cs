using System;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Chứa logic phân quyền truy cập Contribution dùng chung cho Department Leader/Staff
/// (đảm bảo đồng nhất rule giữa Contribution endpoint và assignments progress).
/// </summary>
public static class ContributionAccess
{
    /// <summary>
    /// Quyền đóng góp cho Nhiệm vụ hậu cần (Logistics Request).
    /// </summary>
    public static bool IsDepartmentContributorForLogistics(ulong userId, ulong? assignedToUserId, string status)
    {
        // 1. Phải được assign
        if (assignedToUserId != userId)
            return false;

        // 2. Status hợp lệ (đã accept)
        // REQUESTED, ASSIGNED (mới giao, chưa nhận), REJECTED, CANCELLED -> false
        // ACCEPTED, IN_PROGRESS, DONE, COMPLETED -> true
        return status is "ACCEPTED" or "IN_PROGRESS" or "DONE" or "COMPLETED";
    }

    /// <summary>
    /// Quyền đóng góp cho Thư mời (Invitation).
    /// </summary>
    public static bool IsDepartmentContributorForInvitation(ulong userId, ulong participantUserId, string status)
    {
        if (participantUserId != userId)
            return false;
        
        // Theo VisitInstanceAccess và Contribution rule: ACCEPTED hoặc ASSIGNED
        return status is "ACCEPTED" or "ASSIGNED";
    }
}
