import { VisitScopeFilterOption, VisitStatusFilterOption, VisitRelationFilterOption, VisitFilterConfig } from '../types/delegations.types';

export function getVisitRequestFilterConfig({
  roleCode,
  subRole,
  activeTab,
  isVisitor,
}: {
  roleCode: string;
  subRole: string;
  activeTab: string;
  isVisitor: boolean;
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
      showScope: true,
      // Visitor lọc "Đơn của tôi" với bộ filter tương tự HO (gồm cả Cơ sở). Backend LUÔN
      // ép ownership (visit_requests.visitor_user_id/created_by = currentUser) bất kể filter
      // params — campusId/scope/status chỉ thu hẹp TRONG tập đơn của chính Visitor, không thể
      // dùng để lộ đơn của Visitor khác (xem ViewGuestDelegationListQueryHandler.QueryRequestLevelAsync).
      showCampus: true,
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
        { value: 'PENDING_APPROVAL', label: 'Đã gửi, chờ xử lý', description: 'Đơn đang trong quá trình duyệt', requestStatus: 'PENDING_APPROVAL' },
        { value: 'APPROVED', label: 'Tất cả đơn đã duyệt', description: 'Đơn đã được duyệt (bao gồm đang chuẩn bị, đang diễn ra hoặc đã kết thúc)', requestStatus: 'APPROVED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị tiếp khách', description: 'Đơn đã được duyệt và đang trong quá trình chuẩn bị', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang tiếp khách', description: 'Đoàn đang trong quá trình thăm viếng tại Campus', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Đoàn đã hoàn tất toàn bộ chuyến thăm và thủ tục', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Bị từ chối', description: 'Đơn đã bị từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Đơn đã bị hủy', cancelledOnly: true },
      ],
    };
  }

  // Common: participant tab (Lời mời tham dự / Nhiệm vụ được giao)
  if (activeTab === 'attending') {
    const isDeptStaff = isDept && subRole === 'STAFF';
    return {
      showKeyword: true,
      showStatus: true,
      showScope: true,
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
      showScope: true,
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
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'PENDING_APPROVAL', label: 'Chờ duyệt', description: 'Các đơn đang chờ được phê duyệt', requestStatus: 'PENDING_APPROVAL', campusStatus: 'WAITING_REQUEST_APPROVAL' },
        { value: 'APPROVED', label: 'Tất cả đơn đã duyệt', description: 'Các đơn đã được phê duyệt (bao gồm chờ phân công, chuẩn bị, vận hành, đã đóng)', requestStatus: 'APPROVED' },
        { value: 'WAITING_HOST_ASSIGNMENT', label: 'Chờ chọn Host', description: 'Bao gồm các đơn đã duyệt và đang chờ phân công người đón tiếp', requestStatus: 'APPROVED', campusStatus: 'WAITING_HOST_ASSIGNMENT' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị tiếp khách', description: 'Bao gồm các đơn đã duyệt và đang trong giai đoạn chuẩn bị đón tiếp', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang tiếp khách', description: 'Bao gồm các đoàn đang trong thời gian diễn ra', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', description: 'Bao gồm các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã đóng đoàn', description: 'Bao gồm các đoàn đã hoàn tất toàn bộ quy trình', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Từ chối', description: 'Các đơn đã bị từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Bao gồm các đơn đã bị hủy bỏ', cancelledOnly: true },
      ],
    };
  }

  // Staff Leader
  if (isStaffLeader) {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: true,
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
        { value: 'PENDING_APPROVAL', label: 'Chờ duyệt', description: 'Các đơn đang chờ duyệt', requestStatus: 'PENDING_APPROVAL', campusStatus: 'WAITING_REQUEST_APPROVAL' },
        { value: 'APPROVED', label: 'Tất cả đơn đã duyệt', description: 'Bao gồm mọi đơn đã được duyệt (cả những đơn đang chuẩn bị, vận hành hoặc đã đóng)', requestStatus: 'APPROVED' },
        { 
          value: 'PENDING_HOST_ASSIGNMENT',
          label: 'Cần chọn Host chính thức',
          requestStatus: 'APPROVED',
          visitScope: 'MULTI_CAMPUS',
          campusStatuses: ['ASSIGNED', 'BEFORE_VISIT'],
          relation: 'PENDING_HOST_ASSIGNMENT',
          description: 'Đơn liên cơ sở đã được HO duyệt, đang chờ Staff Leader chọn Host chính thức.'
        },
        { value: 'WAITING_HOST_ASSIGNMENT', label: 'Chờ chọn Host', description: 'Các đoàn đang chờ chọn người phụ trách', requestStatus: 'APPROVED', campusStatus: 'WAITING_HOST_ASSIGNMENT' },
        { value: 'ASSIGNED', label: 'Đã phân công Host', description: 'Các đoàn đã được chọn Host nhưng chưa bắt đầu chuẩn bị', requestStatus: 'APPROVED', campusStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', description: 'Bao gồm các đơn đang trong giai đoạn chuẩn bị đón tiếp', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Trong tiếp khách', description: 'Bao gồm các đoàn đang trong thời gian diễn ra', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', description: 'Bao gồm các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã đóng đoàn', description: 'Bao gồm các đoàn đã hoàn tất toàn bộ quy trình', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Từ chối', description: 'Các đơn đã bị từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Bao gồm các đơn đã bị hủy bỏ', cancelledOnly: true },
      ],
    };
  }

  // Regular Staff - Tab "Đơn phụ trách"
  if (isRegularStaff && activeTab === 'responsible') {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: true,
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
        { value: 'ASSIGNED', label: 'Đã phân công', description: 'Các đoàn đã phân công bạn làm Host nhưng chưa bắt đầu', requestStatus: 'APPROVED', campusStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', description: 'Các đoàn đang trong giai đoạn chuẩn bị đón tiếp', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Trong tiếp khách', description: 'Các đoàn đang trong thời gian diễn ra', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', description: 'Các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã đóng đoàn', description: 'Các đoàn đã hoàn tất toàn bộ quy trình', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Các đoàn đã bị hủy', cancelledOnly: true },
      ],
    };
  }

  // Default fallback
  return {
    showKeyword: true,
    showStatus: true,
    showScope: true,
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
        { value: 'PENDING_APPROVAL', label: 'Chờ xử lý', description: 'Đơn đang chờ được phê duyệt', requestStatus: 'PENDING_APPROVAL' },
        { value: 'APPROVED', label: 'Tất cả đơn đã duyệt', description: 'Đơn đã được phê duyệt', requestStatus: 'APPROVED' },
        { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', description: 'Đơn đã duyệt và đang chuẩn bị', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Trong tiếp khách', description: 'Đoàn đang được tiếp đón', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'CLOSED', label: 'Đã kết thúc', description: 'Chuyến thăm đã hoàn tất', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Đã từ chối', description: 'Đơn bị từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Đơn đã bị hủy', cancelledOnly: true },
    ]
  };
}
