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

  // Common: participant tab
  if (activeTab === 'attending') {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: false,
      showRelation: false,
      statusLabel: 'Trạng thái',
      scopeOptions: [],
      relationOptions: [],
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'UPCOMING', label: 'Sắp diễn ra', timing: 'UPCOMING' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'CLOSED', label: 'Đã kết thúc', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
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
      statusLabel: 'Trạng thái',
      scopeLabel: 'Phạm vi',
      relationLabel: 'Loại xử lý',
      scopeOptions: [
        { value: '', label: 'Tất cả phạm vi' },
        { value: 'MULTI_CAMPUS', label: 'Liên cơ sở' },
        { value: 'SINGLE_CAMPUS', label: 'Đơn cơ sở' },
      ],
      relationOptions: [
        { value: '', label: 'Tất cả' },
        { value: 'ACTION_REQUIRED', label: 'Cần xử lý' },
        { value: 'READ_ONLY', label: 'Chỉ theo dõi' },
      ],
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'HO_PENDING', label: 'Cần HO duyệt', requestStatus: 'PENDING_APPROVAL', visitScopes: ['MULTI_CAMPUS'], actionableOnly: true },
        { value: 'SINGLE_PENDING_READONLY', label: 'Đơn cơ sở chờ duyệt', requestStatus: 'PENDING_APPROVAL', visitScopes: ['SINGLE_CAMPUS'], readOnlyOnly: true },
        { value: 'APPROVED', label: 'Đã duyệt', requestStatus: 'APPROVED' },
        { value: 'REJECTED', label: 'Từ chối', requestStatus: 'REJECTED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
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
      scopeLabel: 'Phạm vi',
      relationLabel: 'Loại xử lý',
      scopeOptions: [
        { value: '', label: 'Tất cả trong campus tôi' },
        { value: 'SINGLE_CAMPUS', label: 'Đơn một cơ sở' },
        { value: 'MULTI_CAMPUS', label: 'Liên cơ sở có campus tôi' },
      ],
      relationOptions: [
        { value: '', label: 'Tất cả' },
        { value: 'ACTION_REQUIRED', label: 'Cần xử lý' },
        { value: 'READ_ONLY', label: 'Theo dõi' },
      ],
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'PENDING_APPROVAL', label: 'Chờ duyệt tại campus', requestStatus: 'PENDING_APPROVAL' },
        { value: 'WAITING_REQUEST_APPROVAL', label: 'Cần phân công Host', requestStatus: 'APPROVED', campusStatus: 'WAITING_REQUEST_APPROVAL' },
        { value: 'ASSIGNED', label: 'Đã phân công Host', requestStatus: 'APPROVED', campusStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Trong tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
      ],
    };
  }

  // Regular Staff - Tab "Đơn phụ trách"
  if (isRegularStaff && activeTab === 'responsible') {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: false,
      showRelation: true,
      statusLabel: 'Trạng thái',
      relationLabel: 'Vai trò của tôi',
      scopeOptions: [],
      relationOptions: [
        { value: '', label: 'Tất cả' },
        { value: 'HOST', label: 'Tôi là Host' },
        { value: 'TASK_ASSIGNEE', label: 'Tôi được giao việc' },
      ],
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'ASSIGNED', label: 'Đã phân công', requestStatus: 'APPROVED', campusStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Trong tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
      ],
    };
  }

  // Visitor
  if (isVisitor) {
    return {
      showKeyword: true,
      showStatus: true,
      showScope: false,
      showRelation: false,
      statusLabel: 'Trạng thái',
      scopeOptions: [],
      relationOptions: [],
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'PENDING_APPROVAL', label: 'Đã gửi, chờ xử lý', requestStatus: 'PENDING_APPROVAL' },
        { value: 'APPROVED', label: 'Đã được duyệt', requestStatus: 'APPROVED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Bị từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
      ],
    };
  }

  // Default / Department / Student fallback on responsible tab
  return {
    showKeyword: true,
    showStatus: true,
    showScope: false,
    showRelation: false, // fallback logic
    statusLabel: 'Trạng thái',
    scopeOptions: [],
    relationOptions: [],
    statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'PENDING_APPROVAL', label: 'Chờ xử lý', requestStatus: 'PENDING_APPROVAL' },
        { value: 'APPROVED', label: 'Đã duyệt', requestStatus: 'APPROVED' },
        { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Trong tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
        { value: 'CLOSED', label: 'Đã kết thúc', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Đã từ chối', requestStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
    ]
  };
}
