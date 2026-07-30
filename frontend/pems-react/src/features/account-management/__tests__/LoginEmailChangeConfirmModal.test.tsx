import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LoginEmailChangeConfirmModal } from '../components/LoginEmailChangeConfirmModal';

function renderModal(overrides: Partial<React.ComponentProps<typeof LoginEmailChangeConfirmModal>> = {}) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const utils = render(
    <LoginEmailChangeConfirmModal
      oldEmail="staff.leader.ct@fpt.edu.vn"
      newEmail="staff.leader.ct1@fpt.edu.vn"
      submitting={false}
      error={null}
      onCancel={onCancel}
      onConfirm={onConfirm}
      {...overrides}
    />,
  );
  return { ...utils, onConfirm, onCancel };
}

const confirmButton = () => screen.getByRole('button', { name: 'Xác nhận thay đổi' });

describe('LoginEmailChangeConfirmModal', () => {
  it('shows both addresses so the operator can check the change before it commits', () => {
    renderModal();
    expect(screen.getByText('staff.leader.ct@fpt.edu.vn')).toBeTruthy();
    expect(screen.getByText('staff.leader.ct1@fpt.edu.vn')).toBeTruthy();
    expect(screen.getByText('Email hiện tại')).toBeTruthy();
    expect(screen.getByText('Email mới')).toBeTruthy();
  });

  // The disruptive part, and the reason this confirmation exists at all.
  it('warns that sessions are revoked and SSO/FEID must be re-linked', () => {
    renderModal();
    expect(screen.getByText(/đăng xuất khỏi các phiên hiện tại/)).toBeTruthy();
    expect(screen.getByText(/liên kết lại SSO\/FEID/)).toBeTruthy();
  });

  it('falls back to a placeholder when no previous address is known', () => {
    renderModal({ oldEmail: '' });
    expect(screen.getByText('—')).toBeTruthy();
  });

  it('confirms once when clicked', () => {
    const { onConfirm } = renderModal();
    fireEvent.click(confirmButton());
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  // The parent's `submitting` flag has not been applied yet when a second click lands in the same
  // tick, so the disabled attribute alone would not stop a duplicate request.
  it('sends one request for a double-click', () => {
    const { onConfirm } = renderModal();
    const button = confirmButton();
    fireEvent.click(button);
    fireEvent.click(button);
    fireEvent.click(button);
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('disables every action and says it is working while the request is in flight', () => {
    renderModal({ submitting: true });
    expect((screen.getByRole('button', { name: /Đang lưu/ }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole('button', { name: 'Hủy' }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole('button', { name: 'Đóng' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('shows a refusal without closing, so the operator can correct and retry', () => {
    renderModal({ error: 'Email này đã tồn tại trong hệ thống. Vui lòng dùng email khác.' });
    expect(screen.getByText('Email này đã tồn tại trong hệ thống. Vui lòng dùng email khác.')).toBeTruthy();
    expect(confirmButton()).toBeTruthy();
  });

  it('cancels without submitting', () => {
    const { onCancel, onConfirm } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
