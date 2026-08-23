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
        { value: 'PENDING_CONTACT_CONFIRMATION', label: t ? t('visitRequestV2:list.statusOptions.awaitingConfirmation.label') : 'Chờ xác nhận', description: t ? t('visitRequestV2:list.statusOptions.awaitingConfirmation.description') : 'Cơ sở đang chờ đầu mối đoàn khách xác nhận qua link email', effectiveStatus: 'WAITING_CONTACT_CONFIRMATION' },
        { value: 'PENDING_APPROVAL', label: t ? t('visitRequestV2:list.statusOptions.pendingApproval.label') : 'Chờ duyệt', description: t ? t('visitRequestV2:list.statusOptions.pendingApproval.description') : 'Đơn đang trong quá trình duyệt', effectiveStatus: 'WAITING_REQUEST_APPROVAL' },
        // REQUEST_ANY_CAMPUS semantics (docs/CanhIter3FixBug/GopYCQuyen/
        // PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md §3.2): Visitor's request-level views
        // (responsible/registered/all) include a request the moment ANY of its own campuses carries
        // the chosen status — so "Đã duyệt" maps to ONLY the campus code ASSIGNED, never a broad union
        // (BEFORE_VISIT/DURING_VISIT/AFTER_VISIT/CLOSED each have their own option right below, and a
        // request with one campus BEFORE_VISIT and another ASSIGNED correctly appears under BOTH).
        { value: 'APPROVED', label: t ? t('visitRequestV2:list.statusOptions.approved.label') : 'Đã duyệt', description: t ? t('visitRequestV2:list.statusOptions.approved.description') : 'Có ít nhất một cơ sở đã duyệt và có người phụ trách, chưa bắt đầu chuẩn bị', effectiveStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: t ? t('visitRequestV2:list.statusOptions.beforeVisit.label') : 'Đang chuẩn bị', description: t ? t('visitRequestV2:list.statusOptions.beforeVisit.description') : 'Đơn đã được duyệt và đang trong quá trình chuẩn bị', effectiveStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: t ? t('visitRequestV2:list.statusOptions.duringVisit.label') : 'Đang diễn ra', description: t ? t('visitRequestV2:list.statusOptions.duringVisit.description') : 'Đoàn đang trong quá trình thăm viếng tại Campus', effectiveStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: t ? t('visitRequestV2:list.statusOptions.afterVisit.label') : 'Chờ đánh giá', description: t ? t('visitRequestV2:list.statusOptions.afterVisit.description') : 'Chuyến thăm đã kết thúc, đang chờ bạn đánh giá trải nghiệm', effectiveStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: t ? t('visitRequestV2:list.statusOptions.closed.label') : 'Đã hoàn tất', description: t ? t('visitRequestV2:list.statusOptions.closed.description') : 'Đoàn đã hoàn tất toàn bộ chuyến thăm và thủ tục', effectiveStatus: 'CLOSED' },
        { value: 'REJECTED', label: t ? t('visitRequestV2:list.statusOptions.rejected.label') : 'Đã từ chối', description: t ? t('visitRequestV2:list.statusOptions.rejected.description') : 'Đơn đã bị từ chối', effectiveStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: t ? t('visitRequestV2:list.statusOptions.cancelled.label') : 'Đã hủy', description: t ? t('visitRequestV2:list.statusOptions.cancelled.description') : 'Đơn đã bị hủy', effectiveStatus: 'CANCELLED' },
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
      // HO monitors the WHOLE request across every campus of it (REQUEST_ANY_CAMPUS — docs/
      // CanhIter3FixBug/GopYCQuyen/PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md §3.1): a
      // request is INCLUDED under a chosen status the moment AT LEAST ONE of its campus instances
      // carries that exact status — never a single aggregate/canonical bucket for the whole request.
      // A request with Hà Nội ASSIGNED and Đà Nẵng still WAITING_REQUEST_APPROVAL correctly appears
      // under BOTH "Đã duyệt" and "Chờ duyệt" at once; that is intended behavior, not a duplicate row
      // (still exactly 1 parent row either way — HO never sees per-campus rows, only the accordion).
      statusOptions: [
        { value: '', label: 'Tất cả trạng thái' },
        { value: 'WAITING_CONTACT_CONFIRMATION', label: 'Chờ xác nhận', description: 'Cơ sở đang chờ đầu mối đoàn khách xác nhận qua link email', effectiveStatus: 'WAITING_CONTACT_CONFIRMATION' },
        // Maps to exactly ONE campus code — WAITING_REQUEST_APPROVAL. Matches whenever any campus of
        // the request is still waiting, including a PARTIALLY_APPROVED multi-campus request (one of
        // its campuses is, by definition, still WAITING_REQUEST_APPROVAL) — no separate aggregate
        // union needed under ANY-campus semantics.
        { value: 'PENDING_ANY', label: 'Chờ duyệt', description: 'Có ít nhất một cơ sở đang chờ xử lý', effectiveStatus: 'WAITING_REQUEST_APPROVAL' },
        // Maps to exactly ONE campus code — ASSIGNED. NOT a union with the request aggregate
        // (dropped 2026-08-23): "Đã duyệt" means "có ít nhất một cơ sở ASSIGNED", full stop — a
        // request whose every campus has moved past ASSIGNED (or was cancelled/rejected) surfaces
        // correctly under ITS OWN stage's filter instead.
        { value: 'APPROVED_ANY', label: 'Đã duyệt', description: 'Có ít nhất một cơ sở đã duyệt và có người phụ trách', effectiveStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị', description: 'Các cơ sở đang trong giai đoạn chuẩn bị đón tiếp', effectiveStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', description: 'Bao gồm các đoàn đang trong thời gian diễn ra', effectiveStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng', description: 'Bao gồm các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục đóng đoàn', effectiveStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Bao gồm các đoàn đã hoàn tất toàn bộ quy trình', effectiveStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Đã từ chối', description: 'Các cơ sở/đơn đã bị từ chối', effectiveStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Bao gồm các đơn đã bị hủy bỏ', effectiveStatus: 'CANCELLED' },
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
        { value: 'WAITING_CONTACT_CONFIRMATION', label: 'Chờ xác nhận', description: 'Cơ sở đang chờ đầu mối đoàn khách xác nhận qua link email', effectiveStatus: 'WAITING_CONTACT_CONFIRMATION' },
        { value: 'PENDING_APPROVAL', label: 'Chờ duyệt', description: 'Các cơ sở thuộc campus của bạn đang chờ duyệt & gán host', effectiveStatus: 'WAITING_REQUEST_APPROVAL' },
        { value: 'ASSIGNED', label: 'Đã duyệt', description: 'Các cơ sở đã duyệt và có người phụ trách, chưa bắt đầu chuẩn bị', effectiveStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị', description: 'Bao gồm các đơn đang trong giai đoạn chuẩn bị đón tiếp', effectiveStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', description: 'Bao gồm các đoàn đang trong thời gian diễn ra', effectiveStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng', description: 'Bao gồm các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục đóng đoàn', effectiveStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Bao gồm các đoàn đã hoàn tất toàn bộ quy trình', effectiveStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Từ chối', description: 'Các cơ sở bạn đã từ chối tiếp nhận', effectiveStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Bao gồm các đơn đã bị hủy bỏ', effectiveStatus: 'CANCELLED' },
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
        { value: 'ASSIGNED', label: 'Đã duyệt', description: 'Các đoàn đã phân công bạn làm người phụ trách nhưng chưa bắt đầu', effectiveStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị', description: 'Các đoàn đang trong giai đoạn chuẩn bị đón tiếp', effectiveStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', description: 'Các đoàn đang trong thời gian diễn ra', effectiveStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng', description: 'Các đoàn đã kết thúc chuyến thăm và chờ hoàn tất thủ tục', effectiveStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Các đoàn đã hoàn tất toàn bộ quy trình', effectiveStatus: 'CLOSED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Các đoàn đã bị hủy', effectiveStatus: 'CANCELLED' },
      ],
    };
  }

  // Default fallback — trong thực tế đây LÀ bộ lọc chính của Staff thường (tab "Tất cả các loại
  // đơn", tab mặc định của họ, tab=all): StaffLeader/Visitor luôn khớp branch riêng ở trên trước
  // khi rơi xuống đây (bất kể tab), còn Department/Student chỉ có đúng tab "attending" nên không
  // bao giờ chạm nhánh này. Đây CHÍNH LÀ nhánh của bug P0-01 gốc (`tab=all&status=APPROVED` trả
  // về badge "Đang chuẩn bị"/"Đã hủy") — cũ dùng requestStatus=APPROVED (aggregate, đứng yên suốt
  // vòng đời sau khi duyệt) làm filter trong khi badge đọc campusStatus; nay cả hai cùng đọc
  // effectiveStatus/effectiveStatusCode nên không thể lệch nhau nữa.
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
        { value: 'WAITING_CONTACT_CONFIRMATION', label: 'Chờ xác nhận', description: 'Cơ sở đang chờ đầu mối đoàn khách xác nhận qua link email', effectiveStatus: 'WAITING_CONTACT_CONFIRMATION' },
        { value: 'PENDING_APPROVAL', label: 'Chờ duyệt', description: 'Đơn đang chờ được phê duyệt', effectiveStatus: 'WAITING_REQUEST_APPROVAL' },
        // REQUEST_ANY_CAMPUS on this branch's real callers ("registered" và "all" cho Regular Staff —
        // xem PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md §3.5/§3.6): map đúng 1 mã campus
        // ASSIGNED, không union với aggregate request nữa.
        { value: 'APPROVED', label: 'Đã duyệt', description: 'Có ít nhất một cơ sở đã được phê duyệt và có người phụ trách, chưa bắt đầu chuẩn bị', effectiveStatus: 'ASSIGNED' },
        { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị', description: 'Đơn đã duyệt và đang chuẩn bị', effectiveStatus: 'BEFORE_VISIT' },
        { value: 'DURING_VISIT', label: 'Đang diễn ra', description: 'Đoàn đang được tiếp đón', effectiveStatus: 'DURING_VISIT' },
        { value: 'AFTER_VISIT', label: 'Chờ đóng', description: 'Đoàn đã kết thúc chuyến thăm, chờ hoàn tất thủ tục', effectiveStatus: 'AFTER_VISIT' },
        { value: 'CLOSED', label: 'Đã hoàn tất', description: 'Chuyến thăm đã hoàn tất', effectiveStatus: 'CLOSED' },
        { value: 'REJECTED', label: 'Từ chối', description: 'Đơn bị từ chối', effectiveStatus: 'REJECTED' },
        { value: 'CANCELLED_ANY', label: 'Đã hủy', description: 'Đơn đã bị hủy', effectiveStatus: 'CANCELLED' },
    ]
  };
}
