import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AccountStatusConfirmModal } from '../components/AccountStatusConfirmModal';

const account = {
  name: 'Nguyễn Văn An',
  email: 'student@fpt.edu.vn',
  avatar: 'https://example.test/avatar.png',
  roleName: 'Sinh viên',
  campus: 'FPT Hà Nội',
};

function renderModal(overrides: Partial<React.ComponentProps<typeof AccountStatusConfirmModal>> = {}) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const utils = render(
    <AccountStatusConfirmModal
      account={account}
      action="disable"
      submitting={false}
      error={null}
      onCancel={onCancel}
      onConfirm={onConfirm}
      {...overrides}
    />,
  );
  return { ...utils, onConfirm, onCancel };
}

describe('AccountStatusConfirmModal', () => {
  // The detail that has to register is WHICH account, and on a list of near-identical @fpt.edu.vn
  // addresses that cannot be a phrase buried in a sentence.
  describe('identifying the account', () => {
    it('shows name, address and role/campus together', () => {
      renderModal();
      expect(screen.getByText('Nguyễn Văn An')).toBeTruthy();
      expect(screen.getByText('student@fpt.edu.vn')).toBeTruthy();
      expect(screen.getByText('Sinh viên · FPT Hà Nội')).toBeTruthy();
    });

    it('joins nothing when role and campus are both missing', () => {
      renderModal({ account: { name: 'Không Rõ', email: 'x@fpt.edu.vn' } });
      // A dangling separator would read as missing data rather than absent data.
      expect(screen.queryByText('·')).toBeNull();
      expect(screen.queryByText(/·/)).toBeNull();
    });

    it('shows one side of the separator when only one is known', () => {
      renderModal({ account: { ...account, campus: '  ' } });
      expect(screen.getByText('Sinh viên')).toBeTruthy();
    });

    it('falls back to an initial when there is no avatar', () => {
      renderModal({ account: { ...account, avatar: null } });
      expect(screen.getByText('N')).toBeTruthy();
    });

    it('does not print "undefined" when the address is missing', () => {
      renderModal({ account: { name: null, email: null, avatar: null } });
      expect(screen.getByText('—')).toBeTruthy();
      expect(screen.queryByText(/undefined/)).toBeNull();
    });
  });

  // Disabling ends live sessions; re-enabling only restores what was there. The two are not
  // interchangeable and neither may be described in the other's words.
  describe('disable', () => {
    it('states the three consequences of cutting access', () => {
      renderModal({ action: 'disable' });
      expect(screen.getByText('Tất cả phiên đăng nhập hiện tại bị thu hồi ngay lập tức.')).toBeTruthy();
      expect(
        screen.getByText('Tài khoản không thể đăng nhập cho đến khi được kích hoạt lại.'),
      ).toBeTruthy();
      // Reassurance that matters: an operator who thinks this deletes the account will not click it.
      expect(
        screen.getByText('Dữ liệu, vai trò và lịch sử hoạt động của tài khoản được giữ nguyên.'),
      ).toBeTruthy();
    });

    // The button says what it does, so the confirmation cannot be misread as the opposite action.
    it('names the action on the confirm button', () => {
      renderModal({ action: 'disable' });
      expect(screen.getByRole('button', { name: 'Vô hiệu hóa' })).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Kích hoạt' })).toBeNull();
    });
  });

  describe('enable', () => {
    it('states what is restored, and nothing alarming', () => {
      renderModal({ action: 'enable' });
      expect(screen.getByText('Tài khoản đăng nhập lại được bằng thông tin hiện có.')).toBeTruthy();
      expect(screen.getByText('Vai trò và phạm vi quyền trước đây được giữ nguyên.')).toBeTruthy();
      expect(screen.queryByText(/thu hồi/)).toBeNull();
    });

    it('names the action on the confirm button', () => {
      renderModal({ action: 'enable' });
      expect(screen.getByRole('button', { name: 'Kích hoạt' })).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Vô hiệu hóa' })).toBeNull();
    });
  });

  describe('submitting', () => {
    it('confirms once when clicked', () => {
      const { onConfirm } = renderModal();
      fireEvent.click(screen.getByRole('button', { name: 'Vô hiệu hóa' }));
      expect(onConfirm).toHaveBeenCalledTimes(1);
    });

    // The parent's `submitting` flag has not been applied yet when a second click lands in the same
    // tick, so the disabled attribute alone would not stop a duplicate request.
    it('sends one request for a double-click', () => {
      const { onConfirm } = renderModal();
      const button = screen.getByRole('button', { name: 'Vô hiệu hóa' });
      fireEvent.click(button);
      fireEvent.click(button);
      fireEvent.click(button);
      expect(onConfirm).toHaveBeenCalledTimes(1);
    });

    it('disables every action while the request is in flight', () => {
      renderModal({ submitting: true });
      expect(
        (screen.getByRole('button', { name: /Đang vô hiệu hóa/ }) as HTMLButtonElement).disabled,
      ).toBe(true);
      expect((screen.getByRole('button', { name: 'Hủy' }) as HTMLButtonElement).disabled).toBe(true);
      expect((screen.getByRole('button', { name: 'Đóng' }) as HTMLButtonElement).disabled).toBe(true);
    });

    it('does not submit while a request is already in flight', () => {
      const { onConfirm } = renderModal({ submitting: true });
      fireEvent.click(screen.getByRole('button', { name: /Đang vô hiệu hóa/ }));
      expect(onConfirm).not.toHaveBeenCalled();
    });

    // A refusal leaves the dialog open with its reason; the operator must be able to retry.
    it('shows the error and allows a retry once the failed request has settled', () => {
      const onConfirm = vi.fn();
      const props = {
        account,
        action: 'disable' as const,
        error: null as string | null,
        onCancel: vi.fn(),
        onConfirm,
      };
      const { rerender } = render(<AccountStatusConfirmModal {...props} submitting={false} />);

      fireEvent.click(screen.getByRole('button', { name: 'Vô hiệu hóa' }));
      expect(onConfirm).toHaveBeenCalledTimes(1);

      rerender(<AccountStatusConfirmModal {...props} submitting />);
      rerender(
        <AccountStatusConfirmModal
          {...props}
          submitting={false}
          error="Không thể cập nhật trạng thái tài khoản. Vui lòng thử lại."
        />,
      );

      expect(
        screen.getByText('Không thể cập nhật trạng thái tài khoản. Vui lòng thử lại.'),
      ).toBeTruthy();
      fireEvent.click(screen.getByRole('button', { name: 'Vô hiệu hóa' }));
      expect(onConfirm).toHaveBeenCalledTimes(2);
    });

    it('cancels without submitting', () => {
      const { onCancel, onConfirm } = renderModal();
      fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));
      expect(onCancel).toHaveBeenCalledTimes(1);
      expect(onConfirm).not.toHaveBeenCalled();
    });
  });
});
