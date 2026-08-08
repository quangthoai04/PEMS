import { VisitScopeFilterOption, VisitStatusFilterOption, VisitRelationFilterOption, VisitFilterConfig } from '../types/delegations.types';

/** Minimal shape of react-i18next's `t` — avoids importing the whole lib into this pure config module. */
type TranslateFn = (key: string, options?: Record<string, unknown>) => string;

export function getVisitRequestFilterConfig({
  roleCode,
  subRole,
  activeTab,
  isVisitor,
  t,
}: {
  roleCode: string;
  subRole: string;
  activeTab: string;
  isVisitor: boolean;
  /** Only consumed for the Visitor branch — every other role's labels stay Vietnamese (backend-driven UI, out of i18n scope). */
  t?: TranslateFn;
}): VisitFilterConfig {
  const isAdmin = roleCode === 'ADMIN';
  const isHO = roleCode === 'HO';
  const isStaff = roleCode === 'STAFF';
  const isStaffLeader = isStaff && subRole === 'LEADER';
  const isRegularStaff = isStaff && subRole === 'STAFF';
  const isDept = roleCode === 'DEPARTMENT' || roleCode === 'DEPT';
  const isDeptLeader = isDept && subRole === 'LEADER';
  const isStudent = roleCode === 'STUDENT';

  // Admin
  if (isAdmin) {
    return {
      showKeyword: false,
      showStatus: false,
      showScope: false,
      showRelation: false,
      statusOptions: [],
      scopeOptions: [],
      relationOptions: [],
    };
  }

  // Visitor
  if (isVisitor) {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: false,
      // Visitor lọc "Đơn của tôi" với bộ filter tương tự HO (gồm cả Cơ sở). Backend LUÔN
      // ép ownership (visit_requests.visitor_user_id/created_by = currentUser) bất kể filter
      // params — campusId/scope/status chỉ thu hẹp TRONG tập đơn của chính Visitor, không thể
      // dùng để lộ đơn của Visitor khác (xem ViewGuestDelegationListQueryHandler.QueryRequestLevelAsync).
      showCampus: true,
      showRelation: false,
      statusLabel: t ? t('visitRequestV2:list.filterVisitor.statusLabel') : 'Trạng thái',
      scopeLabel: t ? t('visitRequestV2:list.filterVisitor.scopeLabel') : 'Phạm vi đơn',
      scopeOptions: [
        { value: '', label: t ? t('visitRequestV2:list.allScopes') : 'Tất cả phạm vi' },
        { value: 'SINGLE_CAMPUS', label: t ? t('visitRequestV2:list.filterVisitor.scopeSingle') : 'Đơn một cơ sở' },
        { value: 'MULTI_CAMPUS', label: t ? t('visitRequestV2:list.filterVisitor.scopeMulti') : 'Đơn liên cơ sở' },
      ],
      relationOptions: [],
      statusOptions: [
        { value: '', label: t ? t('visitRequestV2:list.allStatuses') : 'Tất cả trạng thái' },
        { value: 'PENDING_CONTACT_CONFIRMATION', label: t ? t('visitRequestV2:list.statusOptions.awaitingConfirmation.label') : 'Chờ xác nhận', description: t ? t('visitRequestV2:list.statusOptions.awaitingConfirmation.description') : 'Cơ sở đang chờ đầu mối đoàn khách xác nhận qua link email', requestStatus: 'PENDING_CONTACT_CONFIRMATION' },
        { value: 'PENDING_APPROVAL', label: t ? t('visitRequestV2:list.statusOptions.pendingApproval.label') : 'Chờ duyệt', description: t ? t('visitRequestV2:list.statusOptions.pendingApproval.description') : 'Đơn đang trong quá trình duyệt', requestStatus: 'PENDING_APPROVAL' },
        { value: 'APPROVED', label: t ? t('visitRequestV2:list.statusOptions.approved.label') : 'Đã duyệt', description: t ? t('visitRequestV2:list.statusOptions.approved.description') : 'Đơn đã được duyệt (bao gồm đang chuẩn bị, đang diễn ra, chờ đánh giá hoặc đã hoàn tất)', requestStatus: 'APPROVED' },
        { value: 'BEFORE_VISIT', label: t ? t('visitRequestV2:list.statusOptions.beforeVisit.label') : 'Đang chuẩn bị', description: t ? t('visitRequestV2:list.statusOptions.beforeVisit.description') : 'Đơn đã được duyệt và đang trong quá trình chuẩn bị', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: t ? t('visitRequestV2:list.statusOptions.duringVisit.label') : 'Đang diễn ra', description: t ? t('visitRequestV2:list.statusOptions.duringVisit.description') : 'Đoàn đang trong quá trình thăm viếng tại Campus', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: t ? t('visitRequestV2:list.statusOptions.afterVisit.label') : 'Chờ đánh giá', description: t ? t('visitRequestV2:list.statusOptions.afterVisit.description') : 'Chuyến thăm đã kết thúc, đang chờ bạn đánh giá trải nghiệm', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: t ? t('visitRequestV2:list.statusOptions.closed.label') : 'Đã hoàn tất', description: t ? t('visitRequestV2:list.statusOptions.closed.description') : 'Đoàn đã hoàn tất toàn bộ chuyến thăm và thủ tục', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: t ? t('visitRequestV2:list.statusOptions.rejected.label') : 'Đã từ chối', description: t ? t('visitRequestV2:list.statusOptions.rejected.description') : 'Đơn đã bị từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: t ? t('visitRequestV2:list.statusOptions.cancelled.label') : 'Đã hủy', description: t ? t('visitRequestV2:list.statusOptions.cancelled.description') : 'Đơn đã bị hủy', cancelledOnly: true },
      ],
    };
  }

  // Common: participant tab (Lời mời tham dự / Nhiệm vụ được giao)
  if (activeTab === 'attending') {
    const isDeptStaff = isDept && subRole === 'STAFF';
    return {
      showKeyword: true,
      showStatus: true,
      showScope: false,
      showRelation: false,
      statusLabel: isDeptStaff ? 'Trạng thái nhiệm vụ' : 'Trạng thái lời mời',
      scopeLabel: 'Phạm vi đơn',
      scopeOptions: [
        { value: '', label: 'Tất cả phạm vi' },
        { value: 'SINGLE_CAMPUS', label: 'Đơn một cơ sở' },
        { value: 'MULTI_CAMPUS', label: 'Đơn liên cơ sở' },
      ],
      relationOptions: [],
      statusOptions: isDeptStaff ? [
        { value: 'ALL', label: 'Tất cả nhiệm vụ' },
        { value: 'ASSIGNED', label: 'Mới được giao' },
        { value: 'ACCEPTED', label: 'Đã nhận' },
        { value: 'DECLINED', label: 'Đã từ chối' }
      ] : [
        { value: 'ALL', label: 'Tất cả lời mời' },
        { value: 'INVITED', label: 'Chờ phản hồi' },
        { value: 'ACCEPTED', label: 'Đã nhận lời' },
        { value: 'DECLINED', label: 'Đã từ chối' }
      ],
    };
  }

  // HO
  if (isHO) {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: false,
      showRelation: false,
      showCampus: true,
      statusLabel: 'Trạng thái',
      scopeLabel: 'Phạm vi đơn',
      scopeOptions: [
        { value: '', label: 'Tất cả phạm vi' },
        { value: 'SINGLE_CAMPUS', label: 'Đơn một cơ sở' },
        { value: 'MULTI_CAMPUS', label: 'Đơn liên cơ sở' },
      ],
      relationOptions: [],
      // Campus-independent approval: lọc theo trạng thái TỪNG campus instance (không gate
      // requestStatus=APPROVED nữa — PARTIALLY_APPROVED vẫn có campus đang vận hành).
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'WAITING_CONTACT_CONFIRMATION', label: 'Chờ xác nhận', description: 'Cơ sở đang chờ đầu mối đoàn khách xác nhận qua link email', campusStatus: 'WAITING_CONTACT_CONFIRMATION' },
        // Gộp thành 1 dòng lọc duy nhất — trước đây là 2 option riêng ("Chờ xử lý tại cơ sở" +
        // "Còn cơ sở chưa xử lý xong") cùng đọc chữ "Chờ duyệt" nên hiện trùng nhau trong dropdown.
        // `pendingApprovalAny` báo backend query OR cả 2 điều kiện cũ (xem
        // ViewGuestDelegationListQueryHandler.QueryRequestLevelAsync) — union đúng tập rows mà 2
        // option cũ từng trả về, không thêm/bớt gì.
        { value: 'PENDING_ANY', label: 'Chờ duyệt', description: 'Cơ sở đang chờ xử lý, hoặc đơn liên cơ sở còn cơ sở chưa xử lý xong', pendingApprovalAny: true },
        // Tương tự: gộp "Đã duyệt" (aggregate) + "Đã phân công người phụ trách" (từng cơ sở)
        // thành 1 dòng "Đã duyệt" duy nhất.
        { value: 'APPROVED_ANY', label: 'Đã duyệt', description: 'Cơ sở đã duyệt và có người phụ trách, hoặc đơn đã xử lý xong toàn bộ', approvedAny: true },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị', description: 'Các cơ sở đang trong giai đoạn chuẩn bị đón tiếp', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', description: 'Bao gồm các đoàn đang trong thời gian diễn ra', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng', description: 'Bao gồm các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục đóng đoàn', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Bao gồm các đoàn đã hoàn tất toàn bộ quy trình', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Đã từ chối', description: 'Các cơ sở/đơn đã bị từ chối', campusStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Bao gồm các đơn đã bị hủy bỏ', cancelledOnly: true },
      ],
    };
  }

  // Staff Leader
  if (isStaffLeader) {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: false,
      showRelation: false,
      statusLabel: 'Trạng thái',
      scopeLabel: 'Phạm vi đơn',
      scopeOptions: [
        { value: '', label: 'Tất cả phạm vi' },
        { value: 'SINGLE_CAMPUS', label: 'Đơn một cơ sở' },
        { value: 'MULTI_CAMPUS', label: 'Đơn liên cơ sở' },
      ],
      relationOptions: [],
      // Campus-independent approval: Staff Leader thấy instance campus mình ngay sau submit;
      // duyệt = duyệt + gán host một bước, nên không còn "Chờ chọn Host" riêng.
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'WAITING_CONTACT_CONFIRMATION', label: 'Chờ xác nhận', description: 'Cơ sở đang chờ đầu mối đoàn khách xác nhận qua link email', campusStatus: 'WAITING_CONTACT_CONFIRMATION' },
        { value: 'PENDING_APPROVAL', label: 'Chờ duyệt', description: 'Các cơ sở thuộc campus của bạn đang chờ duyệt & gán host', campusStatus: 'WAITING_REQUEST_APPROVAL' },
        { value: 'ASSIGNED', label: 'Đã duyệt', description: 'Các cơ sở đã duyệt và có người phụ trách, chưa bắt đầu chuẩn bị', campusStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị', description: 'Bao gồm các đơn đang trong giai đoạn chuẩn bị đón tiếp', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', description: 'Bao gồm các đoàn đang trong thời gian diễn ra', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng', description: 'Bao gồm các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục đóng đoàn', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Bao gồm các đoàn đã hoàn tất toàn bộ quy trình', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Từ chối', description: 'Các cơ sở bạn đã từ chối tiếp nhận', campusStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Bao gồm các đơn đã bị hủy bỏ', cancelledOnly: true },
      ],
    };
  }

  // Regular Staff - Tab "Đơn phụ trách". Không có "Chờ duyệt"/"Từ chối": nguồn dữ liệu của
  // tab này là các instance họ ĐÃ được gán làm host, nên 2 trạng thái đó không bao giờ xảy ra.
  if (isRegularStaff && activeTab === 'responsible') {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: false,
      showRelation: false,
      statusLabel: 'Trạng thái',
      scopeLabel: 'Phạm vi đơn',
      scopeOptions: [
        { value: '', label: 'Tất cả phạm vi' },
        { value: 'SINGLE_CAMPUS', label: 'Đơn một cơ sở' },
        { value: 'MULTI_CAMPUS', label: 'Đơn liên cơ sở' },
      ],
      relationOptions: [],
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'ASSIGNED', label: 'Đã duyệt', description: 'Các đoàn đã phân công bạn làm người phụ trách nhưng chưa bắt đầu', campusStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị', description: 'Các đoàn đang trong giai đoạn chuẩn bị đón tiếp', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', description: 'Các đoàn đang trong thời gian diễn ra', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng', description: 'Các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Các đoàn đã hoàn tất toàn bộ quy trình', campusStatus: 'CLOSED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Các đoàn đã bị hủy', cancelledOnly: true },
      ],
    };
  }

  // Default fallback — trong thực tế đây LÀ bộ lọc chính của Staff thường (tab "Tất cả các loại
  // đơn", tab mặc định của họ): StaffLeader/Visitor luôn khớp branch riêng ở trên trước khi rơi
  // xuống đây (bất kể tab), còn Department/Student chỉ có đúng tab "attending" nên không bao giờ
  // chạm nhánh này. Giữ nguyên field lọc requestStatus/campusStatus hiện có — chỉ đổi CHỮ hiển thị
  // + thêm entry AFTER_VISIT ("Chờ đóng") vốn chưa có lựa chọn lọc riêng trước đây.
  return {
    showKeyword: true,
    showStatus: true,
    showScope: false,
    showRelation: false,
    statusLabel: 'Trạng thái',
    scopeLabel: 'Phạm vi đơn',
    scopeOptions: [
      { value: '', label: 'Tất cả phạm vi' },
      { value: 'SINGLE_CAMPUS', label: 'Đơn một cơ sở' },
      { value: 'MULTI_CAMPUS', label: 'Đơn liên cơ sở' },
    ],
    relationOptions: [],
    statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'WAITING_CONTACT_CONFIRMATION', label: 'Chờ xác nhận', description: 'Cơ sở đang chờ đầu mối đoàn khách xác nhận qua link email', campusStatus: 'WAITING_CONTACT_CONFIRMATION' },
        { value: 'PENDING_APPROVAL', label: 'Chờ duyệt', description: 'Đơn đang chờ được phê duyệt', requestStatus: 'PENDING_APPROVAL' },
        { value: 'APPROVED', label: 'Đã duyệt', description: 'Đơn đã được phê duyệt', requestStatus: 'APPROVED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị', description: 'Đơn đã duyệt và đang chuẩn bị', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', description: 'Đoàn đang được tiếp đón', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng', description: 'Đoàn đã kết thúc chuyến thăm, chờ hoàn tất thủ tục', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Chuyến thăm đã hoàn tất', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Từ chối', description: 'Đơn bị từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Đơn đã bị hủy', cancelledOnly: true },
    ]
  };
}
