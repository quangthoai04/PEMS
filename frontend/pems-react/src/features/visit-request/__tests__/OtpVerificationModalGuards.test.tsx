import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';
import { OtpVerificationModal } from '../components/OtpVerificationModal';

/**
 * Plan §16 items 1–3 and 6–7, plus §5.
 *
 * The modal is PORTALLED, and a React portal keeps the component tree even though it leaves the DOM
 * tree — so its submit event bubbles to the <form> that renders it, which is the visit-request form
 * itself. That is how confirming an OTP used to re-submit the entire registration and mint a second
 * challenge: the code the user was holding stopped working, and the initiate that came back wiped
 * the "wrong code" message they were reading.
 */

const baseProps = {
  maskedEmail: 're***@example.com',
  otpError: null as string | null,
  isVerifying: false,
  isResending: false,
  remainingAttempts: null,
  retryAfterSeconds: null,
  retryAt: null,
  resendAfterSeconds: 0,
  humanVerificationRequired: false,
  isRecovering: false,
  onVerify: vi.fn(),
  onResend: vi.fn(),
  onRecover: vi.fn(),
  onCancel: vi.fn(),
};

/** Renders the modal inside a host <form>, exactly as the real page does. */
const renderInForm = (over: Partial<typeof baseProps> & { onReviewForm?: () => void } = {}) => {
  const onHostSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
  const props = { ...baseProps, ...over };
  render(
    <form onSubmit={onHostSubmit} data-testid="host-form">
      <input name="host-field" />
      <OtpVerificationModal {...props} />
    </form>,
  );
  return { onHostSubmit, props };
};

const typeCode = (code: string) => {
  fireEvent.change(screen.getByPlaceholderText('______'), { target: { value: code } });
};

describe('OTP modal guards', () => {
  beforeEach(async () => { vi.clearAllMocks(); await i18n.changeLanguage('en'); });

  it('confirming verifies WITHOUT submitting the form it is rendered inside', () => {
    const { onHostSubmit, props } = renderInForm();
    typeCode('123456');

    fireEvent.click(screen.getByTestId('otp-confirm'));

    expect(props.onVerify).toHaveBeenCalledTimes(1);
    expect(props.onVerify).toHaveBeenCalledWith('123456');
    // The registration form was NOT re-submitted, so no second challenge is minted.
    expect(onHostSubmit).not.toHaveBeenCalled();
  });

  it('Enter inside the code box does not reach the host form either', () => {
    const { onHostSubmit, props } = renderInForm();
    typeCode('123456');

    // Submitting the modal's own form is what Enter in its input does.
    fireEvent.submit(screen.getByPlaceholderText('______').closest('form')!);

    expect(props.onVerify).toHaveBeenCalledTimes(1);
    expect(onHostSubmit).not.toHaveBeenCalled();
  });

  it('every non-confirm control is type="button", so none of them can submit anything', () => {
    renderInForm({ onReviewForm: vi.fn() });
    const buttons = screen.getAllByRole('button');
    const submitters = buttons.filter(b => (b as HTMLButtonElement).type === 'submit');
    expect(submitters).toHaveLength(1);
    expect(submitters[0]).toHaveAttribute('data-testid', 'otp-confirm');
  });

  it('will not verify an incomplete code', () => {
    const { props } = renderInForm();
    typeCode('123');
    fireEvent.click(screen.getByTestId('otp-confirm'));
    expect(props.onVerify).not.toHaveBeenCalled();
  });

  // ── While the create may be committing (plan §5) ──────────────────────────

  it('locks close, back and resend while verifying', () => {
    const { props } = renderInForm({ isVerifying: true });

    fireEvent.click(screen.getByLabelText(/cancel|hủy|đóng/i));
    fireEvent.click(screen.getByRole('button', { name: /back|quay lại/i }));
    expect(props.onCancel).not.toHaveBeenCalled();

    const resend = screen.getByRole('button', { name: /resend|gửi lại|sending/i });
    fireEvent.click(resend);
    expect(props.onResend).not.toHaveBeenCalled();
  });

  it('says it is creating the request, not merely checking a code', () => {
    renderInForm({ isVerifying: true });
    expect(screen.getByTestId('otp-confirm')).toHaveTextContent(/creating the request/i);
  });

  it('closes normally when nothing is in flight', () => {
    const { props } = renderInForm();
    fireEvent.click(screen.getByRole('button', { name: /back|quay lại/i }));
    expect(props.onCancel).toHaveBeenCalledTimes(1);
  });

  // ── Reviewing the form (plan §12) ─────────────────────────────────────────

  it('offers "review the request" only when there is a form to go back to', () => {
    const onReviewForm = vi.fn();
    const { onHostSubmit } = renderInForm({ onReviewForm });

    fireEvent.click(screen.getByTestId('otp-review-form'));
    expect(onReviewForm).toHaveBeenCalledTimes(1);
    // Stepping out must not submit anything either.
    expect(onHostSubmit).not.toHaveBeenCalled();
  });

  it('hides the review action when no handler is supplied', () => {
    renderInForm();
    expect(screen.queryByTestId('otp-review-form')).toBeNull();
  });

  it('keeps the wrong-code message visible', () => {
    renderInForm({ otpError: 'Mã xác minh không chính xác.' });
    expect(screen.getByRole('alert')).toHaveTextContent('Mã xác minh không chính xác.');
    // And the input is still there to try again in.
    expect(screen.getByPlaceholderText('______')).toBeInTheDocument();
  });
});
