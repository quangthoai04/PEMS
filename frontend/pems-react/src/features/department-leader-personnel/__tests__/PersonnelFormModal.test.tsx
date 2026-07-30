import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PersonnelFormModal } from '../components/PersonnelFormModal';
import type { PersonnelDetail, PersonnelStatus } from '../types/departmentLeaderPersonnel.types';

/**
 * The add / edit personnel modals (spec §8, §9, §10, §17.2, §17.3).
 *
 * The property under test throughout: **the account's status decides what an email change COSTS,
 * never which addresses are legal.** The same validator runs in create and in edit, and it runs
 * identically for ACTIVE, INACTIVE, PENDING_EMAIL_CONFIRMATION and LOCKED.
 */

const DOMAIN_ERROR = 'Chỉ chấp nhận @gmail.com và @fpt.edu.vn.';
const CURRENT_EMAIL = 'nhansu@fpt.edu.vn';

const ALL_STATUSES: PersonnelStatus[] = [
  'ACTIVE',
  'INACTIVE',
  'PENDING_EMAIL_CONFIRMATION',
  'LOCKED',
];

function personnel(status: PersonnelStatus): PersonnelDetail {
  return {
    userId: 901,
    fullName: 'Nguyễn Văn A',
    email: CURRENT_EMAIL,
    phone: '0912345678',
    gender: 'MALE',
    status,
    roleCode: 'DEPARTMENT',
    subRole: 'STAFF',
    position: 'Nhân viên',
    avatarUrl: null,
    departmentId: 10,
    departmentName: 'Phòng Hành chính',
    campusId: 1,
    campusName: 'FPT HCM',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    lastLoginAt: null,
    canEdit: true,
    canDisable: true,
    canEnable: false,
    canTransferLeadershipTo: false,
    canResendEmailConfirmation: false,
    isCurrentDepartmentLeader: false,
  };
}

/** The modal has no htmlFor/id wiring, so fields are addressed by their placeholders. */
const emailInput = () => screen.getByPlaceholderText('nhansu@fpt.edu.vn');
const nameInput = () => screen.getByPlaceholderText('Nguyễn Văn A');
const phoneInput = () => screen.getByPlaceholderText('0912345678');

function renderCreate(overrides: Partial<React.ComponentProps<typeof PersonnelFormModal>> = {}) {
  const onSubmit = vi.fn();
  render(
    <PersonnelFormModal
      open
      mode="create"
      submitting={false}
      onClose={vi.fn()}
      onSubmit={onSubmit}
      {...overrides}
    />,
  );
  return { onSubmit };
}

function renderEdit(
  status: PersonnelStatus,
  overrides: Partial<React.ComponentProps<typeof PersonnelFormModal>> = {},
) {
  const onSubmit = vi.fn();
  render(
    <PersonnelFormModal
      open
      mode="edit"
      personnel={personnel(status)}
      submitting={false}
      onClose={vi.fn()}
      onSubmit={onSubmit}
      {...overrides}
    />,
  );
  return { onSubmit };
}

async function typeEmail(user: ReturnType<typeof userEvent.setup>, value: string) {
  await user.clear(emailInput());
  if (value) await user.type(emailInput(), value);
}

async function fillCreateForm(user: ReturnType<typeof userEvent.setup>, email: string) {
  await user.type(nameInput(), 'Nguyễn Văn A');
  await user.type(emailInput(), email);
  await user.type(phoneInput(), '0912345678');
  await user.selectOptions(screen.getByRole('combobox'), 'MALE');
}

const submitCreate = (user: ReturnType<typeof userEvent.setup>) =>
  user.click(screen.getByRole('button', { name: 'Thêm nhân sự' }));

const submitEdit = (user: ReturnType<typeof userEvent.setup>) =>
  user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

// ── Create modal (§17.2) ────────────────────────────────────────────────────

describe('create modal', () => {
  it('states only the two accepted domains and never mentions fe.edu.vn', () => {
    renderCreate();

    expect(screen.getByText(DOMAIN_ERROR)).toBeInTheDocument();
    expect(screen.queryByText(/fe\.edu\.vn/)).not.toBeInTheDocument();
  });

  it('refuses @fe.edu.vn with a field error and does not call the API', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderCreate();

    await fillCreateForm(user, 'nhansu@fe.edu.vn');
    await submitCreate(user);

    expect(screen.getByText(DOMAIN_ERROR)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('marks the email input as errored so the failure is visible, not just readable', async () => {
    const user = userEvent.setup();
    renderCreate();

    await fillCreateForm(user, 'nhansu@fe.edu.vn');
    await submitCreate(user);

    expect(emailInput().className).toContain('border-red-400');
  });

  it('keeps every other field the operator already typed', async () => {
    const user = userEvent.setup();
    renderCreate();

    await fillCreateForm(user, 'nhansu@fe.edu.vn');
    await submitCreate(user);

    expect(nameInput()).toHaveValue('Nguyễn Văn A');
    expect(phoneInput()).toHaveValue('0912345678');
    expect(screen.getByRole('combobox')).toHaveValue('MALE');
    expect(emailInput()).toHaveValue('nhansu@fe.edu.vn');
  });

  it.each(['nhansu@gmail.com', 'nhansu@fpt.edu.vn'])('submits %s', async (email) => {
    const user = userEvent.setup();
    const { onSubmit } = renderCreate();

    await fillCreateForm(user, email);
    await submitCreate(user);

    expect(onSubmit).toHaveBeenCalledWith({
      fullName: 'Nguyễn Văn A',
      email,
      phone: '0912345678',
      gender: 'MALE',
    });
  });

  it('normalizes an uppercase address before sending it', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderCreate();

    await fillCreateForm(user, 'NHANSU@GMAIL.COM');
    await submitCreate(user);

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ email: 'nhansu@gmail.com' }));
  });

  it('clears the error as soon as the address becomes valid', async () => {
    const user = userEvent.setup();
    renderCreate();

    await fillCreateForm(user, 'nhansu@fe.edu.vn');
    await submitCreate(user);
    expect(screen.getAllByText(DOMAIN_ERROR).length).toBeGreaterThan(0);

    await typeEmail(user, 'nhansu@gmail.com');

    // The hint is the same sentence, so "no error" is asserted through the input's styling.
    expect(emailInput().className).not.toContain('border-red-400');
  });

  it('cannot be submitted twice while a request is in flight', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderCreate({ submitting: true });

    await user.click(screen.getByRole('button', { name: 'Thêm nhân sự' }));

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('renders a server-side rejection on the email field', () => {
    renderCreate({ serverErrors: { email: 'Email này đã được sử dụng bởi một tài khoản khác.' } });

    expect(screen.getByText('Email này đã được sử dụng bởi một tài khoản khác.')).toBeInTheDocument();
  });
});

// ── Edit modal, every status (§9.1, §17.3) ──────────────────────────────────

describe.each(ALL_STATUSES)('edit modal — %s', (status) => {
  it('refuses @fe.edu.vn', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderEdit(status);

    await typeEmail(user, 'nhansu@fe.edu.vn');
    await submitEdit(user);

    expect(screen.getByText(DOMAIN_ERROR)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('never opens the confirmation step for an invalid address', async () => {
    const user = userEvent.setup();
    renderEdit(status);

    await typeEmail(user, 'nhansu@fe.edu.vn');
    await submitEdit(user);

    expect(screen.queryByRole('button', { name: /Xác nhận đổi email/ })).not.toBeInTheDocument();
  });

  it.each(['moi@gmail.com', 'moi@fpt.edu.vn'])(
    'accepts %s and confirms before submitting',
    async (email) => {
      const user = userEvent.setup();
      const { onSubmit } = renderEdit(status);

      await typeEmail(user, email);
      await submitEdit(user);

      // A valid, changed address must be confirmed rather than sent straight through.
      const confirm = screen.getByRole('button', { name: /Xác nhận đổi email/ });
      expect(onSubmit).not.toHaveBeenCalled();

      await user.click(confirm);
      expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ email }));
    },
  );

  it('submits directly when the address did not change', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderEdit(status);

    await user.clear(nameInput());
    await user.type(nameInput(), 'Nguyễn Văn B');
    await submitEdit(user);

    expect(screen.queryByRole('button', { name: /Xác nhận đổi email/ })).not.toBeInTheDocument();
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ fullName: 'Nguyễn Văn B', email: CURRENT_EMAIL }),
    );
  });

  it('treats a case-only edit as no change at all', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderEdit(status);

    await typeEmail(user, CURRENT_EMAIL.toUpperCase());
    await submitEdit(user);

    expect(screen.queryByRole('button', { name: /Xác nhận đổi email/ })).not.toBeInTheDocument();
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ email: CURRENT_EMAIL }));
  });

  it('offers the same domain rule as the create modal', () => {
    renderEdit(status);

    expect(screen.getByText(/Chỉ chấp nhận @gmail\.com và @fpt\.edu\.vn\./)).toBeInTheDocument();
    expect(screen.queryByText(/fe\.edu\.vn/)).not.toBeInTheDocument();
  });
});

// ── The status matrix as one assertion (§2.3) ───────────────────────────────

describe('status never changes the domain rule', () => {
  it('produces the identical refusal in all four statuses', async () => {
    const messages: string[] = [];

    for (const status of ALL_STATUSES) {
      const user = userEvent.setup();
      const { unmount } = render(
        <PersonnelFormModal
          open
          mode="edit"
          personnel={personnel(status)}
          submitting={false}
          onClose={vi.fn()}
          onSubmit={vi.fn()}
        />,
      );

      await typeEmail(user, 'nhansu@fe.edu.vn');
      await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));
      messages.push(within(document.body).getByText(DOMAIN_ERROR).textContent ?? '');

      unmount();
    }

    expect(messages).toEqual(Array(ALL_STATUSES.length).fill(DOMAIN_ERROR));
  });
});
