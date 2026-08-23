import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';
import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';
import { focusFirstInvalidField } from '../utils/formErrorNavigation';

/**
 * P0-02 regression — see docs/CanhIter3FixBug/GopYCQuyen/PEMS_PATCH_PLAN_STATUS_FILTER_VALIDATION_UX.md.
 *
 * Root cause: RHF's `useFieldArray` puts an array-level `.min(1)` error at
 * `errors.campusVisits[i].visitors.root.message`, not `.message`. `countFieldErrors` (used for the
 * top summary) already recurses through `.root`, so the summary correctly said "1 field needs
 * fixing" — but `CampusVisitCard`'s own `fieldError()` helper only ever checked `.message` at the
 * exact leaf, so the "Danh sách khách" section itself showed nothing, and there was no
 * `data-field-error`/focus target for `focusFirstInvalidField` to land the user on either.
 *
 * A fresh campus card starts with exactly ONE empty visitor row (`createEmptyMember()`), so the bug
 * needs that row explicitly removed first — a blank submit alone lands on per-field errors instead
 * (a different, already-working code path).
 */

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

const t = (key: string, opts?: Record<string, unknown>) =>
  i18n.t(key, { ns: 'visitRequestV2', ...opts }) as string;

describe('empty "Danh sách khách" (visitors) after removing the default row', () => {
  beforeEach(async () => { localStorage.clear(); await i18n.changeLanguage('en'); });

  /** Renders the form, removes the campus's one default visitor row, then submits. */
  const removeVisitorThenSubmit = async () => {
    render(<VisitRequestFormV2 mode="public" draftNamespace="visitor-list-test" onSuccess={() => {}} />);

    // Desktop table + mobile card both render the same row's remove control — either works, take
    // the first (desktop, earlier in DOM order, matching this test suite's existing convention).
    const removeButtons = screen.getAllByLabelText(t('person.removeGuest'));
    await act(async () => { fireEvent.click(removeButtons[0]); });

    await act(async () => { fireEvent.click(screen.getByTestId('v2-submit')); });
    // Focus is deferred one tick so a just-expanded campus card is really open (same as the sibling
    // "a failed submit on the real form" suite in counterPhoneErrorFocus.test.tsx).
    await act(async () => { await new Promise((r) => setTimeout(r, 120)); });
  };

  it('shows the array-level message in the section itself, not just the top summary', async () => {
    await removeVisitorThenSubmit();

    // The summary already worked before this fix (countFieldErrors walks .root) — the regression is
    // specifically that the section itself showed nothing.
    const banner = screen.getByTestId('v2-error-summary');
    expect(banner.textContent).toMatch(/\d+ fields? still needs? attention\./);

    expect(screen.getAllByText('Please add at least 1 visitor.').length).toBeGreaterThan(0);
  });

  it('marks the visitors section with data-field-error so it participates in focus/scroll like any other field', async () => {
    await removeVisitorThenSubmit();

    const message = screen.getAllByText('Please add at least 1 visitor.')[0];
    const section = message.closest('[data-field-error="true"]');
    expect(section).not.toBeNull();
    expect(section?.tagName).toBe('FIELDSET');
  });

  it('does not duplicate a per-row child field error as the list-level message', async () => {
    // Add a row back (now count=1, satisfying .min(1)) but leave its fields empty — this must
    // produce PER-FIELD errors on that row, never route back through the list-level message above.
    render(<VisitRequestFormV2 mode="public" draftNamespace="visitor-list-test-2" onSuccess={() => {}} />);
    await act(async () => { fireEvent.click(screen.getByTestId('v2-submit')); });
    await act(async () => { await new Promise((r) => setTimeout(r, 120)); });

    // The array-level "at least 1" message must NOT appear — the row exists, just empty.
    expect(screen.queryByText('Please add at least 1 visitor.')).toBeNull();
  });
});

describe('focusFirstInvalidField falls back to the opt-in add-guest button when a container has no row input', () => {
  // Hand-built DOM, same technique as counterPhoneErrorFocus.test.tsx's "finding the first bad
  // field" suite — isolates the fallback logic from which field happens to be first in a real form,
  // which is what made the full-form version of this assertion flaky (an earlier, unrelated invalid
  // field would legitimately win instead).
  afterEach(() => { document.body.innerHTML = ''; });

  it('focuses the [data-error-focus-target] button when the container has no input/textarea/select', () => {
    document.body.innerHTML = `
      <form>
        <fieldset data-field-error="true">
          <p>Please add at least 1 visitor.</p>
          <button type="button" data-error-focus-target="true" id="add-guest">Add guest</button>
        </fieldset>
      </form>`;
    const focused = focusFirstInvalidField(document);
    expect(focused?.id).toBe('add-guest');
    expect(document.activeElement?.id).toBe('add-guest');
  });

  it('still prefers a real input over the fallback button when both exist in the same container', () => {
    document.body.innerHTML = `
      <form>
        <div data-field-error="true">
          <input id="real-input" />
          <button type="button" data-error-focus-target="true" id="fallback-button">Add</button>
        </div>
      </form>`;
    expect(focusFirstInvalidField(document)?.id).toBe('real-input');
  });

  it('never focuses an unrelated button that did not opt in (e.g. Excel import/download)', () => {
    document.body.innerHTML = `
      <form>
        <fieldset data-field-error="true">
          <button type="button" id="excel-import">Import Excel</button>
          <button type="button" id="excel-template">Download template</button>
        </fieldset>
      </form>`;
    // No FOCUSABLE control and no opt-in target inside this container — must return null rather
    // than grabbing the first random button, exactly like the pre-existing "no control" test.
    expect(focusFirstInvalidField(document)).toBeNull();
  });

  it('skips a disabled opt-in target', () => {
    document.body.innerHTML = `
      <form>
        <fieldset data-field-error="true">
          <button type="button" data-error-focus-target="true" disabled id="disabled-add">Add</button>
        </fieldset>
      </form>`;
    expect(focusFirstInvalidField(document)).toBeNull();
  });
});
