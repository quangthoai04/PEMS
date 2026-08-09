/**
 * dashboardRouteAccess
 *
 * NGUỒN CHUẨN DUY NHẤT cho phân quyền route trong khu vực /dashboard.
 *
 * Trước file này, cùng một quyết định "role X có được vào màn Y không" được viết lại ba
 * lần — trong App.tsx (đọc localStorage), trong Sidebar.tsx (điều kiện riêng), và trong
 * backend controller. Ba bản luôn lệch nhau, và lệch ở đâu thì ở đó là lỗ hổng: menu ẩn
 * nhưng gõ URL vẫn vào được, hoặc menu hiện nhưng vào thì bị 403.
 *
 * Nay mọi nơi đều đọc bảng POLICY dưới đây:
 *   - RouteAccessGuard  → canAccessDashboardRoute()
 *   - Sidebar           → getVisibleSidebarItems()
 *   - ForbiddenPage     → getDefaultDashboardRoute()
 *   - Test parity       → so khớp hai hàm trên với nhau
 *
 * Đây KHÔNG phải lớp bảo mật cuối. Backend vẫn kiểm tra role (RoleAuthorizeAttribute) và
 * scope theo bản ghi (campus/department/ownership/participant) trong handler. Bảng này
 * quyết định điều hướng và hiển thị; nó chỉ được phép NGHIÊM NGẶT BẰNG HOẶC HƠN backend,
 * không bao giờ lỏng hơn.
 *
 * Nguồn nghiệp vụ: docs/permissions/PERMISSION_MATRIX.md §5, đối chiếu với
 * [RoleAuthorize] thật trong backend/PEMS.Api/Controllers/*.
 */

import type { EffectiveRole } from './resolveEffectiveRole';

export type DashboardRouteKey =
  | 'DASHBOARD_HOME'
  | 'PROFILE'
  | 'NEWS_LIST'
  | 'NEWS_CREATE'
  | 'NEWS_EDIT'
  | 'NEWS_DETAIL'
  | 'EMAIL_LIST'
  | 'EMAIL_CREATE'
  | 'EMAIL_DETAIL'
  | 'EMAIL_EDIT'
  | 'PARTNER_LIST'
  | 'PARTNER_CREATE'
  | 'PARTNER_DETAIL'
  | 'PARTNER_EDIT'
  | 'DEPARTMENT_LIST'
  | 'DEPARTMENT_DETAIL'
  | 'MY_DEPARTMENT'
  | 'ACCOUNT_LIST'
  | 'CAMPUS_LIST'
  | 'CAMPUS_DETAIL'
  | 'FAQ_LIST'
  | 'FAQ_DETAIL'
  | 'VISIT_LIST'
  | 'VISIT_CREATE'
  | 'VISIT_DETAIL'
  | 'VISIT_EDIT'
  | 'VISIT_PROCESS'
  | 'VISIT_INVITATION'
  | 'VISIT_CONTACT_INVITATIONS'
  | 'VISIT_FEEDBACK'
  | 'AGENDA_TEMPLATE'
  | 'VISIT_PHOTOS'
  | 'DOCUMENTS'
  | 'GALLERY'
  | 'GALLERY_LOCATIONS'
  | 'MINUTES'
  | 'POST_VISIT_TASKS'
  | 'REPORTS'
  | 'FEEDBACK'
  | 'API_MANAGEMENT'
  | 'ADMIN_SESSIONS'
  | 'ADMIN_SECURITY'
  | 'ADMIN_AUDIT_LOGS';

export type DashboardRoutePolicy = {
  key: DashboardRouteKey;
  /** Đường dẫn tuyệt đối dùng cho menu và điều hướng mặc định. */
  path: string;
  allowedRoles: readonly EffectiveRole[];
  /** Hiện trên Sidebar. Route chi tiết/động không lên menu nhưng vẫn phải có policy. */
  showInSidebar?: boolean;
  /**
   * Role vào được route nhưng KHÔNG hiện mục menu. Dùng cho trường hợp menu sẽ tự
   * chuyển hướng đi chỗ khác nên bấm vào là vô nghĩa (xem DASHBOARD_HOME).
   * Đây KHÔNG phải cơ chế phân quyền — quyền vẫn nằm ở allowedRoles.
   */
  hideInSidebarForRoles?: readonly EffectiveRole[];
  /** Nhãn menu (i18n-free — Sidebar hiện dùng chuỗi tiếng Việt cố định). */
  sidebarLabel?: string;
  /** Route mặc định của các role này (dùng cho nút quay lại ở trang 403). */
  defaultForRoles?: readonly EffectiveRole[];
};

// ── Nhóm role dùng lại, đặt tên theo nghiệp vụ để đọc bảng dưới không phải đếm ──
const ALL_ROLES: readonly EffectiveRole[] = [
  'ADMIN', 'HO', 'STAFF_LEADER', 'STAFF', 'DEPARTMENT_LEAD', 'DEPARTMENT', 'STUDENT', 'VISITOR',
];

/** Mọi role nghiệp vụ. ADMIN là quản trị kỹ thuật, không có mặt trong nghiệp vụ tiếp khách. */
const BUSINESS_ROLES: readonly EffectiveRole[] = [
  'HO', 'STAFF_LEADER', 'STAFF', 'DEPARTMENT_LEAD', 'DEPARTMENT', 'STUDENT', 'VISITOR',
];

const ADMIN_ONLY: readonly EffectiveRole[] = ['ADMIN'];
const HO_ONLY: readonly EffectiveRole[] = ['HO'];
const STAFF_LEADER_ONLY: readonly EffectiveRole[] = ['STAFF_LEADER'];

/**
 * Nhóm soạn/đọc email. Hẹp hơn [RoleAuthorize] cấp class của EmailsController một cách
 * có chủ đích — frontend được phép nghiêm ngặt hơn backend, không được lỏng hơn:
 *  - Student/Visitor: matrix §5.5 ghi `O` nhưng backend không cho vào controller.
 *  - Department Staff: backend cho vào, nhưng nghiệp vụ hiện hành không mở màn email
 *    cho họ (giữ đúng hành vi trước đây, chốt với chủ dự án 2026-08-05).
 */
const EMAIL_ROLES: readonly EffectiveRole[] = [
  'HO', 'STAFF_LEADER', 'STAFF', 'DEPARTMENT_LEAD',
];

/** Vận hành tin tức — khớp NewsController (Staff Leader duyệt, Staff/Student soạn). */
const NEWS_AUTHOR_ROLES: readonly EffectiveRole[] = ['STAFF_LEADER', 'STAFF', 'STUDENT'];
const NEWS_READ_ROLES: readonly EffectiveRole[] = ['HO', 'STAFF_LEADER', 'STAFF', 'STUDENT'];

/** Nghiệp vụ cấp campus do khối Staff vận hành. */
const STAFF_OFFICE_ROLES: readonly EffectiveRole[] = ['STAFF_LEADER', 'STAFF'];

/**
 * Tài liệu / biên bản / feedback: khối Staff vận hành, HO theo dõi.
 * PERMISSION_MATRIX §5.7/§5.9/§5.13 ghi HO = `—`, nhưng HO đang thực sự dùng ba màn này
 * và trước nay vẫn thấy chúng trên menu. Giữ HO và coi matrix là chỗ cần cập nhật, thay vì
 * âm thầm cắt quyền của một role đang hoạt động (chốt với chủ dự án 2026-08-05).
 */
const STAFF_OFFICE_WITH_HO: readonly EffectiveRole[] = ['HO', 'STAFF_LEADER', 'STAFF'];

/**
 * Bảng policy. Thứ tự trong mảng chính là thứ tự hiển thị trên Sidebar.
 *
 * Mỗi dòng phải trả lời được: quyền này lấy từ đâu (matrix §mấy / controller nào).
 */
const POLICIES: readonly DashboardRoutePolicy[] = [
  {
    key: 'DASHBOARD_HOME',
    path: '/dashboard',
    // Mọi role đều được PHÉP ở /dashboard — cần thế để STUDENT/VISITOR gõ thẳng URL đó
    // không bị 403 trước khi kịp redirect sang workspace của họ.
    // Nhưng KHÔNG hiện mục menu cho hai role đó: App.tsx điều hướng họ ngay sang
    // /dashboard/visit, nên nút "Dashboard" sẽ chỉ nhảy sang đúng mục ngay bên dưới nó.
    allowedRoles: ALL_ROLES,
    showInSidebar: true,
    hideInSidebarForRoles: ['STUDENT', 'VISITOR'],
    sidebarLabel: 'Dashboard',
    defaultForRoles: ['ADMIN', 'HO', 'STAFF_LEADER', 'STAFF', 'DEPARTMENT_LEAD', 'DEPARTMENT'],
  },

  // ── Tiếp khách: trang chính của sản phẩm — đặt ngay sau Dashboard trong mọi menu, đánh
  // dấu bằng icon sao ở Sidebar (xem ROUTE_ICONS/rendering riêng cho VISIT_LIST). Mọi role
  // nghiệp vụ, ADMIN bị loại (matrix §5.4). Route ở đây chỉ là cổng thô — xem/sửa đúng bản
  // ghi nào là việc của backend (host hiện tại, participant đã accept, campus của instance,
  // chủ đơn...).
  {
    key: 'VISIT_LIST',
    path: '/dashboard/visit',
    // Department Staff bị loại khỏi MÀN DANH SÁCH (giữ đúng hành vi trước đây). Họ vẫn
    // vào được các route tiếp khách theo phân công bên dưới (lời mời, nhiệm vụ, chi tiết) —
    // đó là chỗ công việc thật của họ, và trước nay cũng không bị chặn.
    allowedRoles: ['HO', 'STAFF_LEADER', 'STAFF', 'DEPARTMENT_LEAD', 'STUDENT', 'VISITOR'],
    showInSidebar: true,
    sidebarLabel: 'Quản lý tiếp khách',
    defaultForRoles: ['STUDENT', 'VISITOR'],
  },

  // ── System Administration Console (matrix §5.18, §5.19) ──
  {
    key: 'ACCOUNT_LIST',
    path: '/dashboard/accounts',
    // ADMIN quản trị tài khoản/role (nhiệm vụ quản trị hệ thống); HO và Staff Leader
    // quản lý tài khoản trong phạm vi nghiệp vụ của mình (matrix §5.16 UC-95..100).
    // Phạm vi theo từng tài khoản đích do RoleAccessPolicy.CanManageAccount quyết định.
    allowedRoles: ['ADMIN', 'HO', 'STAFF_LEADER'],
    showInSidebar: true,
    sidebarLabel: 'Quản lý tài khoản',
  },
  {
    key: 'ADMIN_SESSIONS',
    path: '/dashboard/admin/sessions',
    allowedRoles: ADMIN_ONLY,
    showInSidebar: true,
    sidebarLabel: 'Phiên đăng nhập',
  },
  {
    key: 'ADMIN_SECURITY',
    path: '/dashboard/admin/security',
    allowedRoles: ADMIN_ONLY,
    showInSidebar: true,
    sidebarLabel: 'Bảo mật',
  },
  {
    key: 'API_MANAGEMENT',
    path: '/dashboard/apis',
    allowedRoles: ADMIN_ONLY,
    showInSidebar: true,
    sidebarLabel: 'Quản lý API',
  },
  {
    key: 'ADMIN_AUDIT_LOGS',
    path: '/dashboard/admin/audit-logs',
    allowedRoles: ADMIN_ONLY,
    showInSidebar: true,
    sidebarLabel: 'Nhật ký kiểm toán',
  },

  // ── Tin tức (matrix §5.15, NewsController) ──
  {
    key: 'NEWS_LIST',
    path: '/dashboard/news',
    allowedRoles: NEWS_READ_ROLES,
    showInSidebar: true,
    sidebarLabel: 'Quản lý tin tức',
  },
  { key: 'NEWS_CREATE', path: '/dashboard/news/create', allowedRoles: NEWS_AUTHOR_ROLES },
  { key: 'NEWS_EDIT', path: '/dashboard/news/:id/edit', allowedRoles: NEWS_AUTHOR_ROLES },
  { key: 'NEWS_DETAIL', path: '/dashboard/news/:id', allowedRoles: NEWS_READ_ROLES },

  // ── Email (matrix §5.5, EmailsController) ──
  // Department Staff không có màn email. Trước đây App.tsx đá họ về /dashboard im lặng;
  // quyền giữ nguyên như cũ, chỉ đổi cách từ chối thành 403 tường minh.
  {
    key: 'EMAIL_LIST',
    path: '/dashboard/email',
    allowedRoles: EMAIL_ROLES,
    showInSidebar: true,
    sidebarLabel: 'Quản lý email',
  },
  { key: 'EMAIL_CREATE', path: '/dashboard/email/create', allowedRoles: EMAIL_ROLES },
  { key: 'EMAIL_DETAIL', path: '/dashboard/email/:id', allowedRoles: EMAIL_ROLES },
  { key: 'EMAIL_EDIT', path: '/dashboard/email/:id/edit', allowedRoles: EMAIL_ROLES },

  // ── Đối tác (matrix §5.6 + PartnerAccess.CanViewPartnerModule) ──
  {
    key: 'PARTNER_LIST',
    path: '/dashboard/partners',
    allowedRoles: ['HO', 'STAFF_LEADER', 'STAFF'],
    showInSidebar: true,
    sidebarLabel: 'Quản lý đối tác',
  },
  { key: 'PARTNER_CREATE', path: '/dashboard/partners/create', allowedRoles: STAFF_OFFICE_ROLES },
  { key: 'PARTNER_DETAIL', path: '/dashboard/partners/:id', allowedRoles: ['HO', 'STAFF_LEADER', 'STAFF'] },
  { key: 'PARTNER_EDIT', path: '/dashboard/partners/:id/edit', allowedRoles: STAFF_OFFICE_ROLES },

  // ── Phòng ban (matrix §5.17) ──
  {
    key: 'DEPARTMENT_LIST',
    path: '/dashboard/departments',
    // UC-101..106: Staff Leader sở hữu master data phòng ban.
    allowedRoles: STAFF_LEADER_ONLY,
    showInSidebar: true,
    sidebarLabel: 'Quản lý phòng ban',
  },
  {
    key: 'DEPARTMENT_DETAIL',
    path: '/dashboard/departments/:id',
    // UC-105 View Department Details cho phép cả Department Lead/Staff xem.
    allowedRoles: ['STAFF_LEADER', 'DEPARTMENT_LEAD', 'DEPARTMENT'],
  },
  {
    key: 'MY_DEPARTMENT',
    path: '/dashboard/my-department',
    // UC-107..116. Không có :id trên URL — phòng ban được suy từ Leader đang đăng nhập
    // ở server, nên không có id nào để sửa. Backend còn recheck head_user_id.
    allowedRoles: ['DEPARTMENT_LEAD'],
    showInSidebar: true,
    sidebarLabel: 'Quản lý phòng ban',
  },

  // ── Campus: HO-only (matrix §5.14 UC-81..87) ──
  {
    key: 'CAMPUS_LIST',
    path: '/dashboard/campus',
    allowedRoles: HO_ONLY,
    showInSidebar: true,
    sidebarLabel: 'Quản lý campus',
  },
  { key: 'CAMPUS_DETAIL', path: '/dashboard/campus/:id', allowedRoles: HO_ONLY },

  { key: 'VISIT_CREATE', path: '/dashboard/visit/create', allowedRoles: BUSINESS_ROLES },
  { key: 'VISIT_DETAIL', path: '/dashboard/visit/v2/:visitRequestId', allowedRoles: BUSINESS_ROLES },
  { key: 'VISIT_EDIT', path: '/dashboard/visit/v2/:visitRequestId/edit', allowedRoles: BUSINESS_ROLES },
  { key: 'VISIT_PROCESS', path: '/dashboard/visit/process/:id', allowedRoles: BUSINESS_ROLES },
  { key: 'VISIT_INVITATION', path: '/dashboard/visit/invitations/:participantId', allowedRoles: BUSINESS_ROLES },
  {
    // "Lời mời đầu mối của tôi". Mọi role nghiệp vụ: người được mời làm đầu mối vận hành có thể là
    // khách bên ngoài (VISITOR) hoặc nhân sự nội bộ — quyền nhận vai trò không gắn với role.
    // KHÔNG lên sidebar: lối vào là dải thông báo ở màn Quản lý tiếp khách, chỉ hiện khi thật sự có
    // lời mời chờ trả lời. Route chỉ là cổng thô — lời mời NÀO thuộc về ai do backend quyết định
    // theo email đã xác thực của tài khoản, không theo URL.
    key: 'VISIT_CONTACT_INVITATIONS',
    path: '/dashboard/visit/contact-invitations',
    allowedRoles: BUSINESS_ROLES,
  },
  { key: 'VISIT_FEEDBACK', path: '/dashboard/visit/feedback/:visitInstanceId', allowedRoles: BUSINESS_ROLES },
  {
    key: 'AGENDA_TEMPLATE',
    path: '/dashboard/visit/agenda-templates',
    // Matrix §5.20 UC-131..135: mẫu agenda là dữ liệu dùng chung do HO sở hữu.
    allowedRoles: HO_ONLY,
  },
  {
    key: 'VISIT_PHOTOS',
    path: '/dashboard/visit-photos',
    // UC-38/39: Staff upload và gắn thẻ mặt, Student upload ảnh được giao.
    // HO theo dõi. Ảnh của instance nào do backend quyết định theo assignment.
    allowedRoles: ['HO', 'STAFF_LEADER', 'STAFF', 'STUDENT'],
    showInSidebar: true,
    sidebarLabel: 'Quản lý ảnh đoàn khách',
  },
  {
    key: 'POST_VISIT_TASKS',
    path: '/dashboard/post-visit-tasks',
    allowedRoles: ['STAFF_LEADER', 'STAFF', 'DEPARTMENT_LEAD', 'DEPARTMENT', 'STUDENT'],
    showInSidebar: true,
    sidebarLabel: 'Việc sau tiếp khách',
  },

  // ── Nghiệp vụ khối Staff (matrix §5.7, §5.9, §5.13) ──
  {
    key: 'DOCUMENTS',
    path: '/dashboard/documents',
    allowedRoles: STAFF_OFFICE_WITH_HO,
    showInSidebar: true,
    sidebarLabel: 'Quản lý tài liệu',
  },
  {
    key: 'GALLERY',
    path: '/dashboard/gallery',
    allowedRoles: STAFF_LEADER_ONLY,
    showInSidebar: true,
    sidebarLabel: 'Quản lý Gallery',
  },
  {
    key: 'GALLERY_LOCATIONS',
    path: '/dashboard/gallery/locations',
    // Không lên menu: điều hướng vào từ bên trong màn Gallery, đúng như trước đây.
    allowedRoles: STAFF_LEADER_ONLY,
  },
  {
    key: 'MINUTES',
    path: '/dashboard/minutes',
    allowedRoles: STAFF_OFFICE_WITH_HO,
    showInSidebar: true,
    sidebarLabel: 'Quản lý biên bản',
  },
  {
    key: 'FEEDBACK',
    path: '/dashboard/feedback',
    allowedRoles: STAFF_OFFICE_WITH_HO,
    showInSidebar: true,
    sidebarLabel: 'Quản lý feedback',
  },

  // ── Báo cáo (matrix §5.11 + ReportsController) ──
  {
    key: 'REPORTS',
    path: '/dashboard/reports',
    allowedRoles: ['HO', 'STAFF_LEADER', 'DEPARTMENT_LEAD', 'DEPARTMENT'],
    showInSidebar: true,
    sidebarLabel: 'Quản lý báo cáo',
  },

  // ── FAQ: HO-only (matrix §5.10 + FaqsController) ──
  {
    key: 'FAQ_LIST',
    path: '/dashboard/faq',
    allowedRoles: HO_ONLY,
    showInSidebar: true,
    sidebarLabel: 'Quản lý FAQ',
  },
  { key: 'FAQ_DETAIL', path: '/dashboard/faq/:id', allowedRoles: HO_ONLY },

  // ── Hồ sơ cá nhân: mọi role hợp lệ (matrix §5.3, quyền O) ──
  // Không lên Sidebar vì đã có trong menu profile ở chân trang bên trái.
  { key: 'PROFILE', path: '/dashboard/profile', allowedRoles: ALL_ROLES },
];

const POLICY_BY_KEY: ReadonlyMap<DashboardRouteKey, DashboardRoutePolicy> = new Map(
  POLICIES.map((policy) => [policy.key, policy]),
);

/** Toàn bộ policy — dùng cho test matrix và cho kiểm tra đủ route. */
export const DASHBOARD_ROUTE_POLICIES = POLICIES;

export function getDashboardRoutePolicy(
  routeKey: DashboardRouteKey,
): DashboardRoutePolicy | undefined {
  return POLICY_BY_KEY.get(routeKey);
}

/**
 * Quyết định duy nhất cho "role này có vào được route này không".
 *
 * Fail-closed ở mọi nhánh: chưa resolve được role, routeKey lạ (kể cả khi bị ép kiểu từ
 * chuỗi runtime), hay policy chưa khai báo — đều trả false. Không có nhánh "mặc định cho vào".
 */
export function canAccessDashboardRoute(
  role: EffectiveRole | null | undefined,
  routeKey: DashboardRouteKey,
): boolean {
  if (!role) return false;
  const policy = POLICY_BY_KEY.get(routeKey);
  if (!policy) return false;
  return policy.allowedRoles.includes(role);
}

/**
 * Trang đích khi cần đưa người dùng "về chỗ hợp lệ" — nút quay lại ở trang 403 và
 * điều hướng sau đăng nhập. Luôn trả về một route mà chính role đó vào được, nếu không
 * nút 403 sẽ ném người dùng vào một 403 khác.
 */
export function getDefaultDashboardRoute(role: EffectiveRole | null | undefined): string {
  if (!role) return '/invalid-account';

  const declared = POLICIES.find((policy) => policy.defaultForRoles?.includes(role));
  if (declared) return declared.path;

  // Không khai báo mặc định: lấy route đầu tiên trong menu mà role này vào được.
  const firstVisible = POLICIES.find(
    (policy) => policy.showInSidebar && policy.allowedRoles.includes(role),
  );
  return firstVisible?.path ?? '/dashboard';
}

/** Menu Sidebar của một role — cùng bảng policy với route guard, nên không thể lệch. */
export function getVisibleSidebarItems(
  role: EffectiveRole | null | undefined,
): readonly DashboardRoutePolicy[] {
  if (!role) return [];
  return POLICIES.filter(
    (policy) =>
      policy.showInSidebar
      && policy.allowedRoles.includes(role)
      && !policy.hideInSidebarForRoles?.includes(role),
  );
}
