import { describe, it, expect } from 'vitest';
import { accountRoleUpdateFeedback } from '../adapters/accountRoleUpdateFeedback';

/**
 * What the operator is told after an edit that also moved the account's email.
 *
 * The property every case below defends: the account change is committed regardless, so no message
 * may cast doubt on it — but only `SENT` means a message actually reached anybody, and a toast that
 * says "đã gửi" over a skipped or failed delivery sends the operator away waiting for a mail that
 * cannot arrive. For a pending account that mail is the ACTIVATION link, so its failure leaves the
 * account unusable and the wording has to name the way out.
 */
describe('accountRoleUpdateFeedback', () => {
  const newEmail = 'new.holder@fpt.edu.vn';

  describe('no email change', () => {
    it('reports a plain success without mentioning any mail', () => {
      const feedback = accountRoleUpdateFeedback({
        emailChanged: false,
        requiresEmailConfirmation: false,
        emailNotificationStatus: 'NOT_REQUIRED',
      });
      expect(feedback.kind).toBe('success');
      expect(feedback.message).toBe('Cập nhật tài khoản thành công.');
      expect(feedback.message).not.toMatch(/email/i);
    });

    it('says nothing about mail even if the backend reported a status', () => {
      const feedback = accountRoleUpdateFeedback({
        emailChanged: false,
        emailNotificationStatus: 'SENT',
      });
      expect(feedback.message).toBe('Cập nhật tài khoản thành công.');
    });
  });

  describe('pending account — the mail is an activation link', () => {
    const pending = { emailChanged: true, requiresEmailConfirmation: true };

    it('names the address and says the account activates only after confirmation', () => {
      const feedback = accountRoleUpdateFeedback(
        { ...pending, emailNotificationStatus: 'SENT' }, newEmail);
      expect(feedback.kind).toBe('success');
      expect(feedback.message).toContain(newEmail);
      expect(feedback.message).toContain('kích hoạt sau khi người nhận hoàn tất xác nhận');
    });

    it.each([
      ['SKIPPED', 'warning'],
      ['FAILED', 'error'],
    ] as const)('does not claim a send for %s, and points at the resend action', (status, kind) => {
      const feedback = accountRoleUpdateFeedback(
        { ...pending, emailNotificationStatus: status }, newEmail);
      expect(feedback.kind).toBe(kind);
      expect(feedback.message).toContain('Gửi lại email xác nhận');
      expect(feedback.message).not.toContain(`gửi liên kết xác nhận đến ${newEmail}`);
      // The address change itself is committed and must not be reported as lost.
      expect(feedback.message).toContain('Đã cập nhật tài khoản');
    });

    it('says the account is still awaiting confirmation when the link failed to send', () => {
      const feedback = accountRoleUpdateFeedback(
        { ...pending, emailNotificationStatus: 'FAILED' }, newEmail);
      expect(feedback.message).toContain('vẫn ở trạng thái chờ xác nhận email');
    });

    it('stays non-committal on an unrecognised status', () => {
      const feedback = accountRoleUpdateFeedback(
        { ...pending, emailNotificationStatus: 'SOMETHING_NEW' }, newEmail);
      expect(feedback.kind).toBe('warning');
      expect(feedback.message).toContain('chưa xác định được');
    });

    it('still reads correctly when the address is unavailable', () => {
      const feedback = accountRoleUpdateFeedback({ ...pending, emailNotificationStatus: 'SENT' });
      expect(feedback.kind).toBe('success');
      expect(feedback.message).toContain('địa chỉ email mới');
      expect(feedback.message).not.toContain('undefined');
    });
  });

  describe('confirmed account — the mails are notices', () => {
    const active = { emailChanged: true, requiresEmailConfirmation: false };

    it('reports both notices going out as a success', () => {
      const feedback = accountRoleUpdateFeedback(
        { ...active, emailNotificationStatus: 'SENT' }, newEmail);
      expect(feedback.kind).toBe('success');
      expect(feedback.message).toBe('Đã cập nhật tài khoản và gửi email thông báo thay đổi.');
    });

    // Two messages go out here, so "some of them landed" is a real outcome and must be said plainly
    // rather than rounded up to success or down to failure.
    it('reports a partial delivery as exactly that', () => {
      const feedback = accountRoleUpdateFeedback(
        { ...active, emailNotificationStatus: 'PARTIAL' }, newEmail);
      expect(feedback.kind).toBe('warning');
      expect(feedback.message).toContain('một số email thông báo chưa gửi được');
    });

    it.each([
      ['SKIPPED', 'warning'],
      ['FAILED', 'error'],
    ] as const)('does not claim a send for %s', (status, kind) => {
      const feedback = accountRoleUpdateFeedback(
        { ...active, emailNotificationStatus: status }, newEmail);
      expect(feedback.kind).toBe(kind);
      expect(feedback.message).not.toContain('đã gửi email thông báo thay đổi');
      expect(feedback.message).toContain('Đã cập nhật tài khoản');
    });

    // A confirmed account is re-verifying, not activating — the resend action does not apply to it
    // and offering it would send the operator to a button that is not there.
    it('never suggests the resend action', () => {
      for (const status of ['SENT', 'PARTIAL', 'SKIPPED', 'FAILED', 'WAT']) {
        const feedback = accountRoleUpdateFeedback(
          { ...active, emailNotificationStatus: status }, newEmail);
        expect(feedback.message).not.toContain('Gửi lại email xác nhận');
      }
    });
  });

  // ── The role notice, which now goes out for a pending account too ──────────
  //
  // The property throughout: re-roling an account that has not confirmed its email changes the
  // permissions it will wake up with, so the holder is mailed about it — and the operator is told
  // whether that mail landed AND that the account is still waiting on confirmation. A toast that says
  // only "đã cập nhật" invites the assumption that the account is now usable.

  describe('role changed, address unchanged', () => {
    const roleOnly = { emailChanged: false, roleChanged: true };

    it('reports the notice going out to a pending account, and that it is still pending', () => {
      const feedback = accountRoleUpdateFeedback({
        ...roleOnly,
        requiresEmailConfirmation: true,
        emailNotificationStatus: 'NOT_REQUIRED',
        confirmationEmailNotificationStatus: 'NOT_REQUIRED',
        roleChangeEmailNotificationStatus: 'SENT',
      });
      expect(feedback.kind).toBe('success');
      expect(feedback.message).toContain('Đã cập nhật vai trò và gửi email thông báo');
      expect(feedback.message).toContain('vẫn đang chờ xác nhận email');
      // Nothing here activates an account, so nothing may say so.
      expect(feedback.message).not.toMatch(/kích hoạt/i);
    });

    it.each([
      ['SKIPPED', 'warning'],
      ['FAILED', 'error'],
    ] as const)('does not claim a send for %s, and still says the account is pending', (status, kind) => {
      const feedback = accountRoleUpdateFeedback({
        ...roleOnly,
        requiresEmailConfirmation: true,
        roleChangeEmailNotificationStatus: status,
      });
      expect(feedback.kind).toBe(kind);
      expect(feedback.message).not.toContain('và gửi email thông báo tới người dùng');
      expect(feedback.message).toContain('Đã cập nhật vai trò');
      expect(feedback.message).toContain('vẫn đang chờ xác nhận email');
    });

    it('leaves the pending sentence out for an account that is not pending', () => {
      const feedback = accountRoleUpdateFeedback({
        ...roleOnly,
        requiresEmailConfirmation: false,
        roleChangeEmailNotificationStatus: 'SENT',
      });
      expect(feedback.kind).toBe('success');
      expect(feedback.message).not.toContain('chờ xác nhận email');
    });

    // The address never moved, so there is no activation link to resend — pointing at that button
    // would send the operator to an action that changes nothing about the failed mail.
    it('never suggests the resend action', () => {
      for (const status of ['SENT', 'SKIPPED', 'FAILED', 'WAT']) {
        const feedback = accountRoleUpdateFeedback({
          ...roleOnly,
          requiresEmailConfirmation: true,
          roleChangeEmailNotificationStatus: status,
        });
        expect(feedback.message).not.toContain('Gửi lại email xác nhận');
      }
    });

    it('says nothing about mail when the backend reports no role mail was due', () => {
      const feedback = accountRoleUpdateFeedback({
        ...roleOnly,
        requiresEmailConfirmation: false,
        roleChangeEmailNotificationStatus: 'NOT_REQUIRED',
      });
      expect(feedback.message).toBe('Cập nhật tài khoản thành công.');
    });
  });

  describe('role and address changed on a pending account', () => {
    const both = { emailChanged: true, roleChanged: true, requiresEmailConfirmation: true };

    it('names the new address once and says activation waits on confirmation', () => {
      const feedback = accountRoleUpdateFeedback({
        ...both,
        confirmationEmailNotificationStatus: 'SENT',
        roleChangeEmailNotificationStatus: 'SENT',
      }, newEmail);
      expect(feedback.kind).toBe('success');
      expect(feedback.message).toContain(newEmail);
      expect(feedback.message).toContain('email xác nhận và thông báo thay đổi vai trò');
      expect(feedback.message).toContain('kích hoạt sau khi người nhận hoàn tất xác nhận');
    });

    // The activation link is what makes the account usable; the role notice only informs. So a lost
    // role notice is a warning, while a lost link is an error that names the way out.
    it('reports a sent link with a lost role notice as a partial outcome', () => {
      const feedback = accountRoleUpdateFeedback({
        ...both,
        confirmationEmailNotificationStatus: 'SENT',
        roleChangeEmailNotificationStatus: 'FAILED',
      }, newEmail);
      expect(feedback.kind).toBe('warning');
      expect(feedback.message).toContain('gửi email xác nhận');
      expect(feedback.message).toContain('Không thể gửi email thông báo thay đổi vai trò');
    });

    it('points at the resend action when the link failed but the role notice went out', () => {
      const feedback = accountRoleUpdateFeedback({
        ...both,
        confirmationEmailNotificationStatus: 'FAILED',
        roleChangeEmailNotificationStatus: 'SENT',
      }, newEmail);
      expect(feedback.kind).toBe('error');
      expect(feedback.message).toContain('gửi thông báo thay đổi vai trò');
      expect(feedback.message).toContain('Không thể gửi email xác nhận');
      expect(feedback.message).toContain('Gửi lại email xác nhận');
    });

    it('reports both failing without casting doubt on the committed change', () => {
      const feedback = accountRoleUpdateFeedback({
        ...both,
        confirmationEmailNotificationStatus: 'FAILED',
        roleChangeEmailNotificationStatus: 'FAILED',
      }, newEmail);
      expect(feedback.kind).toBe('error');
      expect(feedback.message).toContain('Đã cập nhật tài khoản');
      expect(feedback.message).toContain('Gửi lại email xác nhận');
      expect(feedback.message).not.toMatch(/đã gửi/i);
    });

    // Mail switched off in this environment is not an incident to escalate — but it is not a send either.
    it('shows a skipped link as a warning that still does not claim a send', () => {
      const feedback = accountRoleUpdateFeedback({
        ...both,
        confirmationEmailNotificationStatus: 'SKIPPED',
        roleChangeEmailNotificationStatus: 'SKIPPED',
      }, newEmail);
      expect(feedback.kind).toBe('warning');
      expect(feedback.message).not.toMatch(/đã gửi/i);
      expect(feedback.message).toContain('Gửi lại email xác nhận');
    });
  });

  describe('role and address changed on a confirmed account', () => {
    const both = { emailChanged: true, roleChanged: true, requiresEmailConfirmation: false };

    it('reports both message groups going out', () => {
      const feedback = accountRoleUpdateFeedback({
        ...both,
        emailNotificationStatus: 'SENT',
        roleChangeEmailNotificationStatus: 'SENT',
      }, newEmail);
      expect(feedback.kind).toBe('success');
      expect(feedback.message).toContain('thay đổi địa chỉ đăng nhập');
      expect(feedback.message).toContain('thông báo thay đổi vai trò');
    });

    it('reports a partial address delivery next to a sent role notice', () => {
      const feedback = accountRoleUpdateFeedback({
        ...both,
        emailNotificationStatus: 'PARTIAL',
        roleChangeEmailNotificationStatus: 'SENT',
      }, newEmail);
      // PARTIAL means some of them landed — a warning, not an incident.
      expect(feedback.kind).toBe('warning');
      expect(feedback.message).toContain('Một số email thông báo thay đổi địa chỉ đăng nhập chưa gửi được');
    });

    // A confirmed account is re-verifying, not activating — the resend button is not on its screen.
    it('never suggests the resend action', () => {
      for (const status of ['SENT', 'SKIPPED', 'FAILED', 'PARTIAL', 'WAT']) {
        const feedback = accountRoleUpdateFeedback({
          ...both,
          emailNotificationStatus: status,
          roleChangeEmailNotificationStatus: status,
        }, newEmail);
        expect(feedback.message).not.toContain('Gửi lại email xác nhận');
      }
    });
  });

  // An older server that has not learned the new fields must not make a pending account's activation
  // link look unreported — the single status it does send still describes that mail.
  it('falls back to the legacy single status when the split fields are absent', () => {
    const feedback = accountRoleUpdateFeedback({
      emailChanged: true,
      requiresEmailConfirmation: true,
      emailNotificationStatus: 'FAILED',
    }, newEmail);
    expect(feedback.kind).toBe('error');
    expect(feedback.message).toContain('không thể gửi email xác nhận');
  });

  it('normalizes casing and whitespace from the server', () => {
    expect(accountRoleUpdateFeedback(
      { emailChanged: true, requiresEmailConfirmation: true, emailNotificationStatus: ' sent ' },
      newEmail,
    ).kind).toBe('success');
  });
});
