import React from 'react';
import { describe, expect, it, afterEach, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';
import {
  CHARACTER_COUNT_THRESHOLD,
  isOverCharacterLimit,
  shouldShowCharacterCount,
} from '../components/shared/characterCount';
import { countFieldErrors, focusFirstInvalidField } from '../utils/formErrorNavigation';
import { AutoGrowTextarea } from '../components/shared/AutoGrowTextarea';
import { AutoGrowTextField } from '../components/shared/AutoGrowTextField';
import { PhoneField } from '../components/shared/PhoneField';
import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';
import { buildVisitRequestV2Schema, V2_MIN_ADVANCE_HOURS_CREATE } from '../schema/visitRequestV2.schema';

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [{ campusCode: 'HN', campusName: 'Hòa Lạc', campusId: 1 }],
    loading: false,
  }),
}));
vi.mock('../../../shared/auth/AuthContext', () => ({ useAuthContext: () => ({ user: null }) }));
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {
    getCreateHostCandidates: vi.fn().mockResolvedValue([]),
    searchOrganizations: vi.fn().mockResolvedValue([]),
    initiate: vi.fn(), resendOtp: vi.fn(), recoverOtp: vi.fn(),
  },
}));
vi.mock('../api/visitRequestV2Api', () => ({
  createVisitRequestV2: vi.fn(),
  initiateVisitRequestV2: vi.fn(),
  verifyAndCreateVisitRequestV2: vi.fn(),
  getVisitSubmissionResult: vi.fn(),
}));

/**
 * Plan §22 — the three things that made a long form hard to fill in.
 *
 * A column of "0/2000" on a form nobody had started typing into; "Số điện thoại không hợp lệ" with
 * no hint of what a valid one looks like; and "please fill in all required fields" with no count and
 * no way to find the field, inside a modal whose campus cards can be collapsed over it.
 */

const t = (key: string, opts?: Record<string, unknown>) =>
  i18n.t(key, { ns: 'validation', ...opts }) as string;

describe('when a character counter is worth showing (plan §14)', () => {
  const base = { value: '', maxLength: 2000, focused: false };

  it('stays hidden on an untouched, unfocused field', () => {
    expect(shouldShowCharacterCount(base)).toBe(false);
  });

  it('appears as soon as the field has the caret', () => {
    expect(shouldShowCharacterCount({ ...base, focused: true })).toBe(true);
  });

  it('appears unfocused once the limit is in sight', () => {
    const near = 'x'.repeat(Math.ceil(2000 * CHARACTER_COUNT_THRESHOLD));
    expect(shouldShowCharacterCount({ ...base, value: near })).toBe(true);
    expect(shouldShowCharacterCount({ ...base, value: 'x'.repeat(1000) })).toBe(false);
  });

  it('never hides once the value is over the limit', () => {
    const over = 'x'.repeat(2001);
    expect(shouldShowCharacterCount({ ...base, value: over })).toBe(true);
    expect(isOverCharacterLimit(over, 2000)).toBe(true);
  });

  it('does not put "0/2000" next to a plain "this is required"', () => {
    // The message is not about a number; adding one would only make it look like a length problem.
    expect(shouldShowCharacterCount({ ...base, hasError: true })).toBe(false);
    expect(shouldShowCharacterCount({ ...base, value: 'abc', hasError: true })).toBe(true);
  });

  it('shows nothing at all when the field has no limit', () => {
    expect(shouldShowCharacterCount({ value: 'x'.repeat(9999), focused: true })).toBe(false);
  });
});

describe('the counter on screen', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  const Harness = ({ max = 2000, initial = '' }: { max?: number; initial?: string }) => {
    const [value, setValue] = React.useState(initial);
    return (
      <AutoGrowTextarea
        value={value}
        onChange={setValue}
        maxLength={max}
        counterTestId="counter"
        aria-label="Purpose"
      />
    );
  };

  it('is absent until the field is focused, and gone again on blur', async () => {
    render(<Harness />);
    expect(screen.queryByTestId('counter')).toBeNull();

    const box = screen.getByLabelText('Purpose');
    await act(async () => { fireEvent.focus(box); });
    expect(screen.getByTestId('counter')).toHaveTextContent('0/2000');

    await act(async () => { fireEvent.blur(box); });
    expect(screen.queryByTestId('counter')).toBeNull();
  });

  // Plan CanhIter3FixBug §10 — a real mouse click on a control right below this field sends
  // pointerdown/mousedown (blurring the field) before its own mouseup; if the counter's layout slot
  // disappeared on blur, whatever sat below it moved between mousedown and mouseup and the click
  // landed on empty space. The slot itself must never unmount, only its content.
  it('keeps the same layout slot mounted across focus and blur — only its content toggles', async () => {
    render(<Harness />);
    const box = screen.getByLabelText('Purpose');
    const slot = box.nextElementSibling;
    expect(slot).not.toBeNull();

    await act(async () => { fireEvent.focus(box); });
    expect(box.nextElementSibling).toBe(slot);

    await act(async () => { fireEvent.blur(box); });
    expect(box.nextElementSibling).toBe(slot); // same node — nothing below it could have shifted
    expect(screen.queryByTestId('counter')).toBeNull(); // only the counter's own content is gone
  });

  it('stays put after blur once the value is near the limit', async () => {
    render(<Harness initial={'x'.repeat(1900)} />);
    const box = screen.getByLabelText('Purpose');
    await act(async () => { fireEvent.focus(box); fireEvent.blur(box); });
    expect(screen.getByTestId('counter')).toHaveTextContent('1900/2000');
  });

  it('turns red past the limit, and does not truncate the value', async () => {
    render(<Harness />);
    const box = screen.getByLabelText('Purpose') as HTMLTextAreaElement;
    const pasted = 'x'.repeat(2014);
    await act(async () => { fireEvent.change(box, { target: { value: pasted } }); });

    expect(box.value).toHaveLength(2014);          // nothing was silently cut to fit
    expect(box).not.toHaveAttribute('maxlength');   // …and the browser cannot cut it either
    const counter = screen.getByTestId('counter');
    expect(counter).toHaveTextContent('2014/2000');
    expect(counter.className).toContain('text-red-600');
  });

  it('applies the same rule to the one-line auto-grow field', async () => {
    const Field = () => {
      const [value, setValue] = React.useState('');
      return (
        <AutoGrowTextField value={value} onChange={setValue} maxLength={200} testId="name" ariaLabel="Name" />
      );
    };
    render(<Field />);
    expect(screen.queryByTestId('name-counter')).toBeNull();
    await act(async () => { fireEvent.focus(screen.getByTestId('name')); });
    expect(screen.getByTestId('name-counter')).toHaveTextContent('0/200');
  });
});

describe('over-limit messages name the field (plan §15)', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  const messageFor = (mutate: (v: Record<string, unknown>) => void, path: string) => {
    const values = {
      registerInfo: {
        fullName: 'A', organization: 'B', jobTitle: 'C',
        phone: '+84912345678', email: 'a@b.com', nationality: 'VN',
      },
      partnerSelectionMode: 'NEW_ORGANIZATION',
      partnerId: null,
      campusVisits: [] as unknown[],
    };
    mutate(values as unknown as Record<string, unknown>);
    const schema = buildVisitRequestV2Schema(V2_MIN_ADVANCE_HOURS_CREATE, t);
    const result = schema.safeParse(values);
    if (result.success) return undefined;
    return result.error.issues.find(i => i.path.join('.') === path)?.message;
  };

  it('says which box is too full, and by how much', () => {
    const message = messageFor(
      v => { (v.registerInfo as Record<string, string>).fullName = 'x'.repeat(151); },
      'registerInfo.fullName',
    );
    expect(message).toContain('Registrant full name');
    expect(message).toContain('150');
    expect(message).toContain('151');
  });

  it('groups the digits, so 2000 does not read as a year', () => {
    const message = messageFor(
      v => {
        // The over-limit field is a CAMPUS contact now — the request-level one it used to be is gone.
        (v.campusVisits as unknown[]).push({
          operationalContact: { fullName: 'A', organization: 'x'.repeat(201), jobTitle: 'Trưởng phòng Hợp tác', phone: '+84912345678', email: 'a@b.com' },
        });
      },
      'campusVisits.0.operationalContact.organization',
    );
    expect(message).toContain('200');
    expect(message).not.toMatch(/^Must not exceed/);
  });

  it('says it in Vietnamese too', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    const message = messageFor(
      v => { (v.registerInfo as Record<string, string>).fullName = 'x'.repeat(151); },
      'registerInfo.fullName',
    );
    expect(message).toContain('Họ tên người đăng ký');
    expect(message).toContain('ký tự');
    await act(async () => { await i18n.changeLanguage('en'); });
  });
});

describe('phone guidance (plan §17, §18)', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  const phoneMessage = (value: string, path: string) => {
    const values = {
      registerInfo: {
        fullName: 'A', organization: 'B', jobTitle: 'C',
        phone: value, email: 'a@b.com', nationality: 'VN',
      },
      partnerSelectionMode: 'NEW_ORGANIZATION',
      partnerId: null,
      campusVisits: [] as unknown[],
    };
    const result = buildVisitRequestV2Schema(V2_MIN_ADVANCE_HOURS_CREATE, t).safeParse(values);
    if (result.success) return undefined;
    return result.error.issues.find(i => i.path.join('.') === path)?.message;
  };

  it('names the phone that is wrong and shows both accepted shapes', () => {
    const message = phoneMessage('090abc', 'registerInfo.phone');
    expect(message).toContain('Registrant phone number');
    expect(message).toContain('0912345678');
    expect(message).toContain('+84912345678');
    expect(message).toMatch(/extension/i);
  });

  it('accepts both of the shapes it advertises', () => {
    expect(phoneMessage('0912345678', 'registerInfo.phone')).toBeUndefined();
    expect(phoneMessage('+84912345678', 'registerInfo.phone')).toBeUndefined();
  });

  it('shows the example while the field is being typed into', async () => {
    render(<PhoneField field={{ name: 'phone', 'aria-label': 'Phone' }} testId="p" />);
    expect(screen.queryByTestId('p-hint')).toBeNull();

    await act(async () => { fireEvent.focus(screen.getByTestId('p')); });
    expect(screen.getByTestId('p-hint')).toHaveTextContent('0912345678');
    expect(screen.getByTestId('p-hint')).toHaveTextContent('+84912345678');
  });

  it('gives way to the real message once there is one, rather than stacking two paragraphs', async () => {
    render(<PhoneField field={{ name: 'phone' }} testId="p" hasError error="Registrant phone number is not valid." />);
    await act(async () => { fireEvent.focus(screen.getByTestId('p')); });
    expect(screen.queryByTestId('p-hint')).toBeNull();
  });

  it('says it in Vietnamese', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    const message = phoneMessage('090abc', 'registerInfo.phone');
    expect(message).toContain('Số điện thoại người đăng ký');
    expect(message).toContain('máy lẻ');
    await act(async () => { await i18n.changeLanguage('en'); });
  });
});

describe('finding the first bad field (plan §19)', () => {
  // These build their own DOM by hand; Testing Library's cleanup only removes what IT rendered, so
  // without this the leftovers would be visible to the whole-form tests further down.
  afterEach(() => { document.body.innerHTML = ''; });

  it('counts leaf errors, not branches', () => {
    expect(countFieldErrors({
      registerInfo: { phone: { message: 'bad' }, email: { message: 'bad' } },
      campusVisits: [{ purpose: { message: 'bad' } }],
    })).toBe(3);
    expect(countFieldErrors(undefined)).toBe(0);
  });

  it('focuses the control inside the first field marked invalid', () => {
    document.body.innerHTML = `
      <form>
        <div class="flex flex-col gap-2"><input id="ok" /></div>
        <div class="flex flex-col gap-2" data-field-error="true"><input id="bad-1" /></div>
        <div class="flex flex-col gap-2" data-field-error="true"><input id="bad-2" /></div>
      </form>`;
    const focused = focusFirstInvalidField(document);
    expect(focused?.id).toBe('bad-1');
    expect(document.activeElement?.id).toBe('bad-1');
  });

  it('skips a field hidden inside a collapsed campus card', () => {
    // A collapsed card hides its body with `hidden`; focusing something in there would move the
    // caret to a box the user cannot see, which is worse than not moving it at all.
    document.body.innerHTML = `
      <form>
        <div class="hidden">
          <div data-field-error="true"><input id="collapsed" /></div>
        </div>
        <div data-field-error="true"><input id="visible" /></div>
      </form>`;
    expect(focusFirstInvalidField(document)?.id).toBe('visible');
  });

  it('returns null rather than throwing when nothing can take focus', () => {
    document.body.innerHTML = '<form><div data-field-error="true"><p>no control</p></div></form>';
    expect(focusFirstInvalidField(document)).toBeNull();
  });
});

describe('the submit banner counts what is left', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  it('states a number instead of "fill in all required fields"', () => {
    expect(t('fixErrorsCount', { count: 3 })).toBe('3 fields still need attention.');
    expect(t('fixErrorsCount', { count: 1 })).toBe('1 field still needs attention.');
  });

  it('states it in Vietnamese as well', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    expect(t('fixErrorsCount', { count: 3 })).toBe('Còn 3 trường cần kiểm tra.');
    await act(async () => { await i18n.changeLanguage('en'); });
  });
});

describe('a failed submit on the real form', () => {
  beforeEach(async () => { localStorage.clear(); await i18n.changeLanguage('en'); });

  const submitEmpty = async () => {
    render(<VisitRequestFormV2 mode="public" draftNamespace="focus-test" onSuccess={() => {}} />);
    await act(async () => { fireEvent.click(screen.getByTestId('v2-submit')); });
    // The focus is deferred one tick so a campus card that has just been expanded is really open.
    await act(async () => { await new Promise(r => setTimeout(r, 120)); });
  };

  it('says how many fields are left rather than repeating a generic sentence', async () => {
    await submitEmpty();
    // Every invalid field's own inline message is ALSO `role="alert"` now (P1 accessibility fix,
    // PEMS_PROMPT_FIX_VALIDATION_UX_PLURALIZATION §6) — the summary banner is targeted by its own
    // stable testid rather than by role, which now matches many elements on a failed submit.
    const banner = screen.getByTestId('v2-error-summary');
    expect(banner.textContent).toMatch(/\d+ fields? still needs? attention\./);
  });

  it('puts the caret on the first invalid field instead of leaving the user to scroll', async () => {
    await submitEmpty();

    const active = document.activeElement as HTMLElement;
    expect(active.tagName).toMatch(/INPUT|TEXTAREA|SELECT/);
    const owner = active.closest('[data-field-error="true"]');
    expect(owner).not.toBeNull();
    // …and it is the FIRST one in the form, not just any of them.
    const form = document.getElementById('visit-request-v2-form')!;
    const allInvalid = Array.from(form.querySelectorAll('[data-field-error="true"]'));
    expect(allInvalid.indexOf(owner as Element)).toBe(0);
  });

  it('expands the campus card holding the error, so the field it focuses is on screen', async () => {
    await submitEmpty();
    // The card body is `hidden` when collapsed; an expanded one is what makes its errors reachable.
    const cardBody = document.querySelector('[id^="campus-card-body-"]');
    expect(cardBody?.className).not.toContain('hidden');
  });
});
