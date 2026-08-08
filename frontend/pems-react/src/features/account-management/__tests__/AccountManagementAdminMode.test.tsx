import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';

vi.mock('../api/accountManagementApi', () => ({
  accountManagementApi: {
    getAccounts: vi.fn(),
    getStatistics: vi.fn(),
    getCampusDepartments: vi.fn(),
    getActiveCampuses: vi.fn(),
    getAccountDetails: vi.fn(),
    manageAccountStatus: vi.fn(),
    getRelatedVisitors: vi.fn(),
    getRelatedVisitorNationalities: vi.fn(),
    getRelatedVisitorDetails: vi.fn(),
    getRoleAssignmentOptions: vi.fn(),
    getStaffLeaderAvailability: vi.fn(),
    getHoCampusCheck: vi.fn(),
    resendEmailConfirmation: vi.fn(),
  },
}));

import { accountManagementApi } from '../api/accountManagementApi';
import { AccountManagement } from '../../../pages/dashboard/accounts/AccountManagement';

const api = accountManagementApi as unknown as Record<string, ReturnType<typeof vi.fn>>;

/**
 * One row per state ADMIN can encounter, plus ADMIN's own account. The capability flags are what the
 * backend actually sends (AccountListQueryExecutor): the page is required to render from THEM, not
 * to re-derive the ACTIVE→LOCKED / LOCKED→ACTIVE matrix in the browser.
 */
const ROWS = [
  row({ userId: '1001', fullName: 'Người Hoạt Động', email: 'active@fpt.edu.vn', status: 'ACTIVE', canSecurityLock: true }),
  row({ userId: '1002', fullName: 'Người Bị Khóa', email: 'locked@fpt.edu.vn', status: 'LOCKED', canSecurityUnlock: true }),
  row({ userId: '1003', fullName: 'Người Vô Hiệu', email: 'inactive@fpt.edu.vn', status: 'INACTIVE', securityActionDisabledReason: 'ACCOUNT_INACTIVE' }),
  row({ userId: '1004', fullName: 'Người Chờ Email', email: 'pending@fpt.edu.vn', status: 'PENDING_EMAIL_CONFIRMATION', securityActionDisabledReason: 'ACCOUNT_PENDING_EMAIL_CONFIRMATION' }),
  row({ userId: '700', fullName: 'Quản Trị Viên', email: 'admin@fpt.edu.vn', status: 'ACTIVE', isCurrentUser: true, securityActionDisabledReason: 'SELF_ACCOUNT' }),
];

function row(overrides: Record<string, unknown>) {
  return {
    userId: '0',
    email: 'x@fpt.edu.vn',
    fullName: 'X',
    roleCode: 'STUDENT',
    roleName: 'Sinh viên',
    campusId: '2',
    campusName: 'Quy Nhơn',
    status: 'ACTIVE',
    providers: [],
    createdAt: '2026-01-01T00:00:00',
    // ADMIN = global read + security control: every business capability is false.
    canViewDetails: true,
    canUpdateRole: false,
    canManageStatus: false,
    canEditBasicInfo: false,
    hideStatusToggleReason: 'ADMIN_SECURITY_ONLY',
    isCurrentUser: false,
    canSecurityLock: false,
    canSecurityUnlock: false,
    securityActionDisabledReason: null,
    ...overrides,
  };
}

function signInAsAdmin() {
  localStorage.setItem('currentUser', JSON.stringify({ role: 'ADMIN' }));
}

function signInAsStaffLeader() {
  localStorage.setItem('currentUser', JSON.stringify({
    role: 'STAFF', subRole: 'LEADER', campus: 'Quy Nhơn',
  }));
}

function renderPage() {
  return render(<MemoryRouter><AccountManagement /></MemoryRouter>);
}

/** Reports the router's current URL so a navigation can be asserted without mocking useNavigate. */
function LocationProbe() {
  const { pathname, search } = useLocation();
  return <span data-testid="url">{pathname + search}</span>;
}

function renderPageWithUrl() {
  return render(
    <MemoryRouter initialEntries={['/dashboard/accounts']}>
      <AccountManagement />
      <LocationProbe />
    </MemoryRouter>,
  );
}

/** The <tr> a given email is displayed in. */
async function rowFor(email: string) {
  const cell = await screen.findByText(email);
  return cell.closest('tr') as HTMLElement;
}

beforeEach(() => {
  localStorage.clear();
  Object.values(api).forEach((fn) => fn.mockReset());
  api.getAccounts.mockResolvedValue({
    items: ROWS, page: 1, pageSize: 20, totalItems: ROWS.length, totalPages: 1,
  });
  api.getStatistics.mockResolvedValue({
    totalAccounts: 5, activeAccounts: 2, inactiveAccounts: 1, lockedAccounts: 1,
  });
  api.getActiveCampuses.mockResolvedValue([
    { campusId: '1', campusCode: 'QN', campusName: 'Quy Nhơn' },
    { campusId: '2', campusCode: 'HN', campusName: 'Hà Nội' },
  ]);
  api.getCampusDepartments.mockResolvedValue([]);
  api.manageAccountStatus.mockResolvedValue({
    userId: '1001', status: 'LOCKED', revokedSessions: 2, message: 'ok',
  });
  signInAsAdmin();
});

describe('AccountManagement — ADMIN has no personnel controls', () => {
  it('offers no way to create an account', async () => {
    renderPage();
    await screen.findByText('active@fpt.edu.vn');

    expect(screen.queryByText('Tạo tài khoản mới')).toBeNull();
  });

  it('frames the screen as observation + security, not personnel management', async () => {
    renderPage();

    expect(await screen.findByText(/Theo dõi tài khoản và xử lý các vấn đề bảo mật/)).toBeInTheDocument();
  });

  it('renders no ACTIVE/INACTIVE toggle on any row', async () => {
    renderPage();
    await screen.findByText('active@fpt.edu.vn');

    // The business toggle is a checkbox; the security actions are buttons.
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
  });

  it('offers no edit affordance in the detail modal', async () => {
    api.getAccountDetails.mockResolvedValue({
      userId: '1001', fullName: 'Người Hoạt Động', email: 'active@fpt.edu.vn',
      roleCode: 'STUDENT', roleName: 'Sinh viên', displayRole: 'Sinh viên',
      status: 'ACTIVE', createdAt: '2026-01-01T00:00:00', campusName: 'Hà Nội',
      providers: ['LOCAL_PASSWORD'],
      canEditBasicInfo: false, canResendEmailConfirmation: false, canEditPendingEmail: false,
    });
    renderPage();

    await userEvent.click(within(await rowFor('active@fpt.edu.vn')).getByTitle('Xem tài khoản'));

    await screen.findByText('Thông tin chi tiết');
    expect(screen.queryByText('Chỉnh sửa tài khoản')).toBeNull();
    expect(screen.queryByText('Chỉnh sửa thông tin')).toBeNull();
    expect(screen.queryByText('Thay thế Staff Leader')).toBeNull();
    // Read-only security context is present instead.
    expect(await screen.findByText('Thông tin tài khoản & bảo mật')).toBeInTheDocument();
    expect(screen.getByText('LOCAL_PASSWORD')).toBeInTheDocument();

    // The two password-era boxes are gone. Both columns are written only by the password sign-in
    // handler, so under Google-SSO-only they could never move — and a counter frozen at 0 reads as
    // "this account has not been attacked" rather than "nothing is counting".
    expect(screen.queryByText('Số lần đăng nhập thất bại')).toBeNull();
    expect(screen.queryByText('Tạm khóa đến')).toBeNull();
  });
});

describe('AccountManagement — ADMIN filters', () => {
  it('opens on "Tất cả tài khoản" and asks the server for it', async () => {
    renderPage();
    await screen.findByText('active@fpt.edu.vn');

    // ADMIN observes the WHOLE system, so the first view must not silently exclude guest accounts.
    expect(screen.getByLabelText('Loại tài khoản')).toHaveValue('ALL');
    await waitFor(() => expect(api.getAccounts).toHaveBeenCalledWith(
      expect.objectContaining({ accountType: 'ALL' }),
    ));
  });

  it('labels every role option in Vietnamese, and names what the DEPARTMENT filter really returns', async () => {
    renderPage();

    const roleSelect = await screen.findByLabelText('Vai trò');
    expect(within(roleSelect).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Tất cả Vai trò', 'Quản trị viên', 'Cán bộ HO', 'Nhân sự phòng IC',
      'Nhân sự phòng ban khác', 'Sinh viên', 'Khách',
    ]);
    expect(within(roleSelect).queryByRole('option', { name: 'VISITOR' })).toBeNull();
  });

  it('keeps the account-type filter untouched when a role is picked', async () => {
    renderPage();
    await screen.findByText('active@fpt.edu.vn');

    const accountType = screen.getByLabelText('Loại tài khoản');
    expect(accountType).toHaveValue('ALL');

    await userEvent.selectOptions(await screen.findByLabelText('Vai trò'), 'VISITOR');

    // The whole point: "Tất cả tài khoản" survives the role pick, and the role dropdown keeps
    // offering the internal roles so the choice can be undone.
    expect(accountType).toHaveValue('ALL');
    expect(within(screen.getByLabelText('Vai trò')).getByRole('option', { name: 'Sinh viên' }))
      .toBeInTheDocument();

    await waitFor(() => expect(api.getAccounts).toHaveBeenLastCalledWith(
      expect.objectContaining({ accountType: 'ALL', roleCode: 'VISITOR' }),
    ));
  });

  it('drops the role filter entirely while the list is scoped to guest accounts', async () => {
    renderPage();
    await screen.findByText('active@fpt.edu.vn');

    await userEvent.selectOptions(screen.getByLabelText('Loại tài khoản'), 'VISITOR');

    // A guest has no sub-role, so the control is gone — not left showing two redundant entries.
    await waitFor(() => expect(screen.queryByLabelText('Vai trò')).toBeNull());

    // And it is gone because it was cleared, not because it is hiding an active narrowing: the
    // request carries the account type alone.
    await waitFor(() => expect(api.getAccounts).toHaveBeenLastCalledWith(
      expect.not.objectContaining({ roleCode: expect.anything() }),
    ));
    expect(api.getAccounts).toHaveBeenLastCalledWith(
      expect.objectContaining({ accountType: 'VISITOR' }),
    );
  });

  it('drops the campus filter too, and forgets the campus that was picked', async () => {
    renderPage();
    await screen.findByText('active@fpt.edu.vn');

    // Start from a narrowed campus — this is the state that would silently empty the guest list.
    await userEvent.selectOptions(screen.getByLabelText('Cơ sở'), 'Quy Nhơn');
    await waitFor(() => expect(api.getAccounts).toHaveBeenLastCalledWith(
      expect.objectContaining({ campusId: '1' }),
    ));

    await userEvent.selectOptions(screen.getByLabelText('Loại tài khoản'), 'VISITOR');

    // A guest belongs to no campus, so the control goes away — and the campus it was holding goes
    // with it, otherwise the list would come back empty with nothing on screen to explain why.
    await waitFor(() => expect(screen.queryByLabelText('Cơ sở')).toBeNull());
    await waitFor(() => expect(api.getAccounts).toHaveBeenLastCalledWith(
      expect.not.objectContaining({ campusId: expect.anything() }),
    ));
  });

  it('brings the campus filter back on "Toàn quốc" when the account type leaves guests', async () => {
    renderPage();
    await screen.findByText('active@fpt.edu.vn');

    await userEvent.selectOptions(screen.getByLabelText('Cơ sở'), 'Quy Nhơn');
    await userEvent.selectOptions(screen.getByLabelText('Loại tài khoản'), 'VISITOR');
    await waitFor(() => expect(screen.queryByLabelText('Cơ sở')).toBeNull());

    await userEvent.selectOptions(screen.getByLabelText('Loại tài khoản'), 'ALL');

    // Reappears at its neutral value rather than restoring the old campus — what the operator sees
    // is what is being applied.
    expect(await screen.findByLabelText('Cơ sở')).toHaveValue('');
  });

  it('brings the role filter back, cleared, when the account type leaves "Tài khoản khách"', async () => {
    renderPage();
    await screen.findByText('active@fpt.edu.vn');

    await userEvent.selectOptions(screen.getByLabelText('Loại tài khoản'), 'VISITOR');
    await waitFor(() => expect(screen.queryByLabelText('Vai trò')).toBeNull());

    await userEvent.selectOptions(screen.getByLabelText('Loại tài khoản'), 'INTERNAL');

    const roleSelect = await screen.findByLabelText('Vai trò');
    expect(roleSelect).toHaveValue('');
    // "Khách" is not on offer in internal mode, so the cleared value is the only coherent one.
    expect(within(roleSelect).queryByRole('option', { name: 'Khách' })).toBeNull();
  });
});

describe('AccountManagement — the detail drawer reads as read-only', () => {
  /** The wrapper <div> a labelled field renders into, inside the read-only grid. */
  function fieldBox(grid: HTMLElement, label: string) {
    return within(grid).getByText(label).parentElement as HTMLElement;
  }

  async function openDetail(email: string, details: Record<string, unknown>) {
    api.getAccountDetails.mockResolvedValue(details);
    renderPage();
    await userEvent.click(within(await rowFor(email)).getByTitle('Xem tài khoản'));
    return screen.findByTestId('detail-view-grid');
  }

  const STUDENT_DETAIL = {
    userId: '1001', fullName: 'Người Hoạt Động', email: 'active@fpt.edu.vn',
    roleCode: 'STUDENT', roleName: 'Sinh viên', displayRole: 'Sinh viên',
    status: 'ACTIVE', createdAt: '2026-01-01T00:00:00', campusName: 'Hà Nội',
    studentId: 'QN01', gender: 'MALE', phone: '0902000121', nationality: 'Việt Nam',
    providers: ['LOCAL_PASSWORD'],
    canEditBasicInfo: false, canResendEmailConfirmation: false, canEditPendingEmail: false,
  };

  it('locks every field box, and only the status pill escapes', async () => {
    const grid = await openDetail('active@fpt.edu.vn', STUDENT_DETAIL);

    // Nothing in ADMIN's drawer is editable, so nothing in it may LOOK editable — the previous
    // styling put most of these on a near-white box that read as an input to click into.
    for (const label of ['Họ và tên', 'Email', 'Giới tính', 'Số điện thoại', 'Vai trò',
      'Mã số sinh viên (MSSV)', 'Cơ sở trực thuộc']) {
      const box = fieldBox(grid, label);
      expect(box.querySelector('.bg-slate-100'), `${label} phải là ô khóa`).not.toBeNull();
      expect(box.querySelector('svg.text-slate-400'), `${label} phải có icon khóa`).not.toBeNull();
    }

    // The one exception, and deliberately so: the status is a pill, and the operator DOES change it
    // — from the list row. A lock here would claim otherwise.
    const statusBox = fieldBox(grid, 'Trạng thái tài khoản');
    expect(statusBox.querySelector('svg')).toBeNull();
    expect(within(statusBox).getByText('Hoạt động')).toBeInTheDocument();

    // No editable control survived anywhere in the grid.
    expect(grid.querySelector('input, select, textarea')).toBeNull();
  });

  it('drops "Quốc tịch" for an internal account and keeps it for a guest', async () => {
    const internal = await openDetail('active@fpt.edu.vn', STUDENT_DETAIL);
    expect(within(internal).queryByText('Quốc tịch')).toBeNull();
    expect(within(internal).queryByText('Việt Nam')).toBeNull();
  });

  it('keeps "Quốc tịch" and drops "Đơn vị công tác / Doanh nghiệp" for a guest account', async () => {
    const grid = await openDetail('active@fpt.edu.vn', {
      userId: '1001', fullName: 'Khách Nước Ngoài', email: 'active@fpt.edu.vn',
      roleCode: 'VISITOR', roleName: 'Khách', displayRole: 'Khách',
      status: 'ACTIVE', createdAt: '2026-01-01T00:00:00', campusName: null,
      nationality: 'Nhật Bản', organization: 'Sony Corp',
      providers: [],
      canEditBasicInfo: false, canResendEmailConfirmation: false, canEditPendingEmail: false,
    });

    expect(within(grid).queryByText('Đơn vị công tác / Doanh nghiệp')).toBeNull();
    expect(screen.queryByText('Sony Corp')).toBeNull();
    // Nationality is the one field a guest keeps it for — that is the whole reason it exists here.
    expect(within(grid).getByText('Nhật Bản')).toBeInTheDocument();
  });

  it('carries the account to the sessions and security modules instead of opening them unfiltered', async () => {
    api.getAccountDetails.mockResolvedValue(STUDENT_DETAIL);
    renderPageWithUrl();
    await userEvent.click(within(await rowFor('active@fpt.edu.vn')).getByTitle('Xem tài khoản'));
    await screen.findByTestId('detail-view-grid');

    await userEvent.click(screen.getByRole('button', { name: /Xem phiên đăng nhập/ }));
    expect(screen.getByTestId('url')).toHaveTextContent(
      '/dashboard/admin/sessions?keyword=active%40fpt.edu.vn',
    );
  });

  it('carries the account to the security log too, and says so on the drawer', async () => {
    api.getAccountDetails.mockResolvedValue(STUDENT_DETAIL);
    renderPageWithUrl();
    await userEvent.click(within(await rowFor('active@fpt.edu.vn')).getByTitle('Xem tài khoản'));
    await screen.findByTestId('detail-view-grid');

    // The caption has to match the behaviour — it used to promise the opposite ("chưa lọc sẵn").
    expect(screen.getByText(/đã lọc sẵn theo/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /Xem nhật ký bảo mật/ }));
    expect(screen.getByTestId('url')).toHaveTextContent(
      '/dashboard/admin/security?keyword=active%40fpt.edu.vn',
    );
  });

  it('leaves a Staff Leader\'s drawer on the original, unlocked chrome', async () => {
    signInAsStaffLeader();
    api.getAccounts.mockResolvedValue({
      items: [row({
        userId: '1001', fullName: 'Nhân sự IC', email: 'ic@fpt.edu.vn', status: 'ACTIVE',
        canUpdateRole: true, canManageStatus: true, canEditBasicInfo: true, hideStatusToggleReason: null,
      })],
      page: 1, pageSize: 20, totalItems: 1, totalPages: 1,
    });
    api.getRelatedVisitors.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 });
    api.getRelatedVisitorNationalities.mockResolvedValue({ items: [] });

    const grid = await openDetail('ic@fpt.edu.vn', {
      userId: '1001', fullName: 'Nhân sự IC', email: 'ic@fpt.edu.vn',
      roleCode: 'STAFF', roleName: 'Nhân sự IC', displayRole: 'Nhân sự IC', subRole: 'STAFF',
      status: 'ACTIVE', createdAt: '2026-01-01T00:00:00', campusName: 'Quy Nhơn',
      department: 'Phòng Hợp tác Quốc tế', gender: 'MALE', phone: '0902000122',
      providers: ['LOCAL_PASSWORD'],
      canEditBasicInfo: true, canResendEmailConfirmation: false, canEditPendingEmail: false,
    });

    // The locked look is ADMIN's alone: a Staff Leader reaches an edit mode from this very drawer,
    // so dressing every field as permanently locked would contradict the edit button above it.
    for (const label of ['Họ và tên', 'Email', 'Vai trò', 'Cơ sở trực thuộc', 'Phòng ban']) {
      const box = fieldBox(grid, label);
      expect(box.querySelector('.bg-slate-100'), `${label} không được là ô khóa`).toBeNull();
      expect(box.querySelector('svg'), `${label} không được có icon khóa`).toBeNull();
    }
  });
});

describe('AccountManagement — the campus column says "none" instead of nothing', () => {
  it('renders a dash for an account that belongs to no campus', async () => {
    api.getAccounts.mockResolvedValue({
      items: [
        row({ userId: '2001', fullName: 'Khách Nước Ngoài', email: 'guest@partner.test', roleCode: 'VISITOR', campusId: null, campusName: null }),
        row({ userId: '2002', fullName: 'Sinh Viên', email: 'student@fpt.edu.vn', campusName: 'Quy Nhơn' }),
      ],
      page: 1, pageSize: 20, totalItems: 2, totalPages: 1,
    });
    renderPage();

    // Cột: STT · Họ và tên · Email · Cơ sở · Vai trò · …
    // An em dash, not a hyphen: it has to read as a deliberate "none" from across the table, next
    // to 13px bold campus names.
    const guestCampusCell = (await rowFor('guest@partner.test')).querySelectorAll('td')[3];
    expect(guestCampusCell).toHaveTextContent('—');

    // The row that DOES have a campus still shows it — the dash is a fallback, not a blanket.
    const studentCampusCell = (await rowFor('student@fpt.edu.vn')).querySelectorAll('td')[3];
    expect(studentCampusCell).toHaveTextContent('Quy Nhơn');
  });
});

describe('AccountManagement — ADMIN security actions follow the backend capability', () => {
  it('shows Lock on an ACTIVE row and Unlock on a LOCKED row', async () => {
    renderPage();

    expect(within(await rowFor('active@fpt.edu.vn'))
      .getByRole('button', { name: 'Khóa bảo mật active@fpt.edu.vn' })).toBeInTheDocument();
    expect(within(await rowFor('active@fpt.edu.vn'))
      .queryByRole('button', { name: /^Mở khóa bảo mật/ })).toBeNull();

    expect(within(await rowFor('locked@fpt.edu.vn'))
      .getByRole('button', { name: 'Mở khóa bảo mật locked@fpt.edu.vn' })).toBeInTheDocument();
    expect(within(await rowFor('locked@fpt.edu.vn'))
      .queryByRole('button', { name: /^Khóa bảo mật/ })).toBeNull();
  });

  it.each([
    ['inactive@fpt.edu.vn'],
    ['pending@fpt.edu.vn'],
    ['admin@fpt.edu.vn'],
  ])('offers no security action on %s', async (email) => {
    renderPage();

    const tr = within(await rowFor(email));
    expect(tr.queryByRole('button', { name: /^Khóa bảo mật/ })).toBeNull();
    expect(tr.queryByRole('button', { name: /^Mở khóa bảo mật/ })).toBeNull();
  });

  it('requires a reason before it will lock, then sends the exact payload', async () => {
    renderPage();

    await userEvent.click(within(await rowFor('active@fpt.edu.vn'))
      .getByRole('button', { name: 'Khóa bảo mật active@fpt.edu.vn' }));

    const dialog = within(await screen.findByRole('dialog'));
    // The account is restated so a mis-clicked row is caught before the action.
    expect(dialog.getByText('Người Hoạt Động')).toBeInTheDocument();

    await userEvent.click(dialog.getByRole('button', { name: 'Khóa bảo mật' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Vui lòng chọn lý do khóa.');
    expect(api.manageAccountStatus).not.toHaveBeenCalled();

    await userEvent.selectOptions(dialog.getByLabelText(/Lý do khóa/), 'Nghi ngờ tài khoản bị xâm nhập');
    await userEvent.click(dialog.getByRole('button', { name: 'Khóa bảo mật' }));

    await waitFor(() => expect(api.manageAccountStatus).toHaveBeenCalledWith({
      userId: '1001',
      status: 'LOCKED',
      reason: 'Nghi ngờ tài khoản bị xâm nhập',
    }));
  });

  it('requires a reason before it will unlock, and sends status ACTIVE', async () => {
    api.manageAccountStatus.mockResolvedValue({
      userId: '1002', status: 'ACTIVE', revokedSessions: 0, message: 'ok',
    });
    renderPage();

    await userEvent.click(within(await rowFor('locked@fpt.edu.vn'))
      .getByRole('button', { name: 'Mở khóa bảo mật locked@fpt.edu.vn' }));

    const dialog = within(await screen.findByRole('dialog'));
    await userEvent.click(dialog.getByRole('button', { name: 'Mở khóa' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Vui lòng chọn lý do mở khóa.');
    expect(api.manageAccountStatus).not.toHaveBeenCalled();

    await userEvent.selectOptions(dialog.getByLabelText(/Lý do mở khóa/), 'Điều tra hoàn tất');
    await userEvent.click(dialog.getByRole('button', { name: 'Mở khóa' }));

    await waitFor(() => expect(api.manageAccountStatus).toHaveBeenCalledWith({
      userId: '1002',
      status: 'ACTIVE',
      reason: 'Điều tra hoàn tất',
    }));
  });

  it('sends the typed description when the reason is "Khác"', async () => {
    renderPage();

    await userEvent.click(within(await rowFor('active@fpt.edu.vn'))
      .getByRole('button', { name: 'Khóa bảo mật active@fpt.edu.vn' }));

    const dialog = within(await screen.findByRole('dialog'));
    await userEvent.selectOptions(dialog.getByLabelText(/Lý do khóa/), 'Khác');

    // "Khác" with an empty description is still no reason at all.
    await userEvent.click(dialog.getByRole('button', { name: 'Khóa bảo mật' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Vui lòng mô tả lý do.');
    expect(api.manageAccountStatus).not.toHaveBeenCalled();

    await userEvent.type(dialog.getByLabelText('Mô tả lý do'), 'Theo yêu cầu của phòng an ninh');
    await userEvent.click(dialog.getByRole('button', { name: 'Khóa bảo mật' }));

    await waitFor(() => expect(api.manageAccountStatus).toHaveBeenCalledWith({
      userId: '1001',
      status: 'LOCKED',
      reason: 'Theo yêu cầu của phòng an ninh',
    }));
  });
});

describe('AccountManagement — HO / Staff Leader regression', () => {
  it('a Staff Leader still opens on "Tài khoản nội bộ", which is the only default their options allow', async () => {
    // The ADMIN default above is deliberately NOT shared: a Staff Leader has no "Tất cả tài khoản"
    // option at all, so 'ALL' would leave their <select> pointing at a value that does not exist.
    signInAsStaffLeader();
    api.getRelatedVisitors.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 });
    api.getRelatedVisitorNationalities.mockResolvedValue({ items: [] });

    renderPage();

    const select = await screen.findByLabelText('Loại tài khoản');
    expect(select).toHaveValue('INTERNAL');
    expect(within(select).queryByRole('option', { name: 'Tất cả tài khoản' })).toBeNull();
  });

  it('a Staff Leader still gets the create button and the business toggle, and no security action', async () => {
    signInAsStaffLeader();
    api.getAccounts.mockResolvedValue({
      items: [row({
        userId: '1001', fullName: 'Nhân sự IC', email: 'ic@fpt.edu.vn', status: 'ACTIVE',
        canUpdateRole: true, canManageStatus: true, hideStatusToggleReason: null,
      })],
      page: 1, pageSize: 20, totalItems: 1, totalPages: 1,
    });
    api.getRelatedVisitors.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 });
    api.getRelatedVisitorNationalities.mockResolvedValue({ items: [] });

    renderPage();

    expect(await screen.findByText('Tạo tài khoản mới')).toBeInTheDocument();
    expect(within(await rowFor('ic@fpt.edu.vn')).getByRole('checkbox')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Khóa bảo mật/ })).toBeNull();
  });
});
