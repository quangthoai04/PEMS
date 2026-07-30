import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PendingEmailEditConfirmModal } from '../components/PendingEmailEditConfirmModal';

function renderModal(overrides: Partial<React.ComponentProps<typeof PendingEmailEditConfirmModal>> = {}) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const utils = render(
    <PendingEmailEditConfirmModal
      oldEmail="old.owner@fpt.edu.vn"
      newEmail="new.owner@fpt.edu.vn"
      submitting={false}
      error={null}
      onCancel={onCancel}
      onConfirm={onConfirm}
      {...overrides}
    />,
  );
  return { ...utils, onConfirm, onCancel };
}

const confirmButton = () => screen.getByRole('button', { name: /Cập nhật và gửi email xác nhận/ });

describe('PendingEmailEditConfirmModal', () => {
  it('shows both addresses so the operator can check the correction before it commits', () => {
    renderModal();
    expect(screen.getByText('old.owner@fpt.edu.vn')).toBeTruthy();
    expect(screen.getByText('new.owner@fpt.edu.vn')).toBeTruthy();
  });

  // The consequence an operator would not otherwise expect: anything already sent to the old address
  // stops working the moment this is confirmed.
  it('states that the link sent to the old address stops working', () => {
    renderModal();
    expect(screen.getByText('Liên kết xác nhận đã gửi tới email cũ sẽ không còn hiệu lực.')).toBeTruthy();
  });

  it('states that a new link is issued and that activation still needs the recipient', () => {
    renderModal();
    expect(screen.getByText('Hệ thống sẽ phát hành một liên kết xác nhận mới và gửi tới email mới.')).toBeTruthy();
    expect(screen.getByText('Tài khoản chỉ được kích hoạt sau khi người nhận hoàn tất xác nhận email.')).toBeTruthy();
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

  it('disables both actions and says it is working while the request is in flight', () => {
    renderModal({ submitting: true });
    const button = screen.getByRole('button', { name: /Đang cập nhật/ });
    expect((button as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole('button', { name: 'Hủy' }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole('button', { name: 'Đóng' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('does not submit while a request is already in flight', () => {
    const { onConfirm } = renderModal({ submitting: true });
    fireEvent.click(screen.getByRole('button', { name: /Đang cập nhật/ }));
    expect(onConfirm).not.toHaveBeenCalled();
  });

  // A refusal leaves the modal open with its reason; the operator must be able to correct and retry.
  it('allows a retry once a failed request has settled', () => {
    const { onConfirm, rerender } = renderModal();
    fireEvent.click(confirmButton());
    expect(onConfirm).toHaveBeenCalledTimes(1);

    rerender(
      <PendingEmailEditConfirmModal
        oldEmail="old.owner@fpt.edu.vn"
        newEmail="new.owner@fpt.edu.vn"
        submitting
        error={null}
        onCancel={vi.fn()}
        onConfirm={onConfirm}
      />,
    );
    rerender(
      <PendingEmailEditConfirmModal
        oldEmail="old.owner@fpt.edu.vn"
        newEmail="new.owner@fpt.edu.vn"
        submitting={false}
        error="Email này đã tồn tại trong hệ thống. Vui lòng dùng email khác."
        onCancel={vi.fn()}
        onConfirm={onConfirm}
      />,
    );

    expect(screen.getByText('Email này đã tồn tại trong hệ thống. Vui lòng dùng email khác.')).toBeTruthy();
    fireEvent.click(confirmButton());
    expect(onConfirm).toHaveBeenCalledTimes(2);
  });

  it('cancels without submitting', () => {
    const { onCancel, onConfirm } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
