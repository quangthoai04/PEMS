import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen, fireEvent, act, waitFor, within } from '@testing-library/react';
import { useState } from 'react';
import i18n from '../../../shared/i18n/config';
import { VisitDateTimeRangePicker } from '../components/shared/VisitDateTimeRangePicker';
import {
  addMinutes, durationMinutes, normalizeTimeInput, splitWallClock, wallClockToMinutes,
} from '../components/shared/visitDateTime';

/**
 * Plan §23. The old schedule was two <input type="datetime-local"> boxes: the user typed a date
 * twice, got no duration back, and learned that the end was before the start only after pressing
 * submit — because the form validates on submit.
 */

const Harness = ({
  start = '', end = '', minAdvanceHours = 72,
}: { start?: string; end?: string; minAdvanceHours?: number }) => {
  const [value, setValue] = useState({ start, end });
  return (
    <>
      <VisitDateTimeRangePicker
        idPrefix="t"
        minAdvanceHours={minAdvanceHours}
        startValue={value.start}
        endValue={value.end}
        onChange={setValue}
      />
      <output data-testid="start-out">{value.start}</output>
      <output data-testid="end-out">{value.end}</output>
    </>
  );
};

/** A date safely past any advance rule. */
const futureDate = (days = 20) => {
  const d = new Date();
  d.setDate(d.getDate() + days);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
};

/** Each keystroke gets its own act(): a real keyboard produces separate events, not one batch. */
const setTime = async (testId: string, value: string) => {
  const input = screen.getByTestId(testId);
  await act(async () => { fireEvent.focus(input); });
  await act(async () => { fireEvent.change(input, { target: { value } }); });
  await act(async () => { fireEvent.keyDown(input, { key: 'Enter' }); });
};

const press = async (input: HTMLElement, key: string) => {
  await act(async () => { fireEvent.keyDown(input, { key }); });
};

const startOut = () => screen.getByTestId('start-out').textContent;
const endOut = () => screen.getByTestId('end-out').textContent;

describe('wall-clock helpers', () => {
  it('computes a duration without the host timezone getting a vote', () => {
    // Pure string arithmetic: the same two wall-clock values always give the same answer,
    // whatever TZ the browser is in — that is the whole point of the carrier.
    expect(durationMinutes('2026-08-01T09:00', '2026-08-01T10:30')).toBe(90);
    expect(durationMinutes('2026-08-01T22:00', '2026-08-02T01:00')).toBe(180);
    expect(durationMinutes('2026-08-01T09:00', '2026-08-01T08:00')).toBe(-60);
  });

  it('rolls the date when adding across midnight', () => {
    expect(addMinutes('2026-08-01T23:30', 60)).toBe('2026-08-02T00:30');
    expect(addMinutes('2026-12-31T23:00', 120)).toBe('2027-01-01T01:00');
  });

  it('reads times the way people type them, and refuses what it cannot read', () => {
    expect(normalizeTimeInput('8:30')).toBe('08:30');
    expect(normalizeTimeInput('0830')).toBe('08:30');
    expect(normalizeTimeInput('8')).toBe('08:00');
    expect(normalizeTimeInput('8.45')).toBe('08:45');
    expect(normalizeTimeInput('25:00')).toBeNull();
    expect(normalizeTimeInput('08:70')).toBeNull();
    expect(normalizeTimeInput('nonsense')).toBeNull();
  });

  it('round-trips a wall-clock string through minutes unchanged', () => {
    const value = '2026-08-01T09:15';
    expect(splitWallClock(value)).toEqual({ date: '2026-08-01', time: '09:15' });
    expect(wallClockToMinutes(value)).toBe(wallClockToMinutes('2026-08-01T09:15'));
  });
});

describe('VisitDateTimeRangePicker', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  it('starts on the same day: one date, two times', () => {
    render(<Harness />);
    expect(screen.getByTestId('t-start-date')).toBeInTheDocument();
    expect(screen.getByTestId('t-start-time')).toBeInTheDocument();
    expect(screen.getByTestId('t-end-time')).toBeInTheDocument();
    // No second date until the user asks for one.
    expect(screen.queryByTestId('t-end-date')).toBeNull();
    expect(screen.getByTestId('t-multiday')).not.toBeChecked();
  });

  it('suggests an end one hour later while the end is untouched', async () => {
    const date = futureDate();
    render(<Harness />);

    await act(async () => {
      fireEvent.change(screen.getByTestId('t-start-date'), { target: { value: date } });
    });
    await setTime('t-start-time', '08:00');

    expect(startOut()).toBe(`${date}T08:00`);
    expect(endOut()).toBe(`${date}T09:00`);
  });

  it('never overwrites an end the user chose deliberately', async () => {
    const date = futureDate();
    render(<Harness />);

    await act(async () => {
      fireEvent.change(screen.getByTestId('t-start-date'), { target: { value: date } });
    });
    await setTime('t-start-time', '08:00');
    await setTime('t-end-time', '12:00');           // a deliberate 4-hour visit
    await setTime('t-start-time', '09:00');         // the start moves

    // Still ends at 12:00 — it was not quietly shortened back to one hour.
    expect(endOut()).toBe(`${date}T12:00`);
    expect(startOut()).toBe(`${date}T09:00`);
  });

  it('labels every end option with the visit length it produces', async () => {
    const date = futureDate();
    render(<Harness start={`${date}T08:00`} end={`${date}T09:00`} />);

    await act(async () => { fireEvent.focus(screen.getByTestId('t-end-time')); });

    const list = await screen.findByTestId('t-end-time-listbox');
    const options = within(list).getAllByRole('option').map(o => o.textContent ?? '');
    expect(options[0]).toContain('08:30');
    expect(options[0]).toMatch(/30/);      // 30 minutes
    expect(options.some(o => o.includes('09:00') && /1\s*hour/i.test(o))).toBe(true);
    expect(options.some(o => o.includes('10:00') && /2\s*hour/i.test(o))).toBe(true);
  });

  it('says immediately that an end before the start is wrong', async () => {
    const date = futureDate();
    render(<Harness start={`${date}T10:00`} end={`${date}T11:00`} />);

    await setTime('t-end-time', '09:00');

    // Live — no submit needed, which is exactly what was missing before.
    expect(await screen.findByTestId('t-end-error'))
      .toHaveTextContent(/end time must be after the start/i);
  });

  it('accepts 30 minutes and refuses 29', () => {
    const date = futureDate();
    const { unmount } = render(<Harness start={`${date}T09:00`} end={`${date}T09:30`} />);
    expect(screen.queryByTestId('t-end-error')).toBeNull();
    expect(screen.getByTestId('t-duration')).toHaveTextContent(/30/);
    unmount();

    // A fresh mount, not a rerender: the harness holds the value in its own state.
    render(<Harness start={`${date}T09:00`} end={`${date}T09:29`} />);
    expect(screen.getByTestId('t-end-error')).toHaveTextContent(/at least 30 minutes/i);
  });

  it('switches to a second date on request, and reports a multi-day duration', async () => {
    const d1 = futureDate(20);
    const d2 = futureDate(21);
    render(<Harness start={`${d1}T22:00`} end={`${d1}T23:00`} />);

    await act(async () => { fireEvent.click(screen.getByTestId('t-multiday')); });
    expect(screen.getByTestId('t-end-date')).toBeInTheDocument();

    await act(async () => {
      fireEvent.change(screen.getByTestId('t-end-date'), { target: { value: d2 } });
    });
    await setTime('t-end-time', '01:00');

    expect(endOut()).toBe(`${d2}T01:00`);
    expect(screen.getByTestId('t-duration')).toHaveTextContent(/3/); // 22:00 → 01:00 = 3 hours
  });

  it('opens already in multi-day mode for a saved overnight visit', () => {
    const d1 = futureDate(20);
    const d2 = futureDate(21);
    render(<Harness start={`${d1}T22:00`} end={`${d2}T01:00`} />);

    expect(screen.getByTestId('t-multiday')).toBeChecked();
    expect(screen.getByTestId('t-end-date')).toHaveValue(d2);
  });

  /**
   * The start/end inputs are wired through react-hook-form's `Controller` render prop, which never
   * registers a DOM ref for them — so RHF's own auto-focus-on-error has nothing to `.focus()`. The
   * `data-field-error` wrapper is what lets the shared `focusFirstInvalidField()` helper find and
   * scroll to this control instead (PEMS_PROMPT_FIX_VALIDATION_UX_PLURALIZATION §6 verification).
   */
  it('marks the field as erroring and wires aria-invalid when a submit-time error is passed', () => {
    const date = futureDate();
    render(
      <VisitDateTimeRangePicker
        idPrefix="t"
        minAdvanceHours={72}
        startValue={`${date}T08:00`}
        endValue={`${date}T09:00`}
        onChange={() => {}}
        startError="Không hợp lệ"
      />,
    );
    const startInput = screen.getByTestId('t-start-date');
    expect(startInput.closest('[data-field-error="true"]')).not.toBeNull();
    expect(startInput).toHaveAttribute('aria-invalid', 'true');
    expect(startInput).toHaveAttribute('aria-describedby', expect.stringContaining('start-error'));
    expect(screen.getByTestId('t-start-error')).toHaveAttribute('role', 'alert');
  });

  it('applies the advance rule it was given, not a hardcoded one', async () => {
    const soon = new Date(Date.now() + 30 * 60 * 1000);
    const pad = (n: number) => String(n).padStart(2, '0');
    const at = `${soon.getFullYear()}-${pad(soon.getMonth() + 1)}-${pad(soon.getDate())}T${pad(soon.getHours())}:${pad(soon.getMinutes())}`;

    const { unmount } = render(<Harness start={at} end={addMinutes(at, 60)} minAdvanceHours={72} />);
    expect(screen.getByTestId('t-start-error')).toHaveTextContent(/at least 72 hours/i);
    unmount();

    // The same instant, judged by the edit rule instead: still too soon, but the number differs.
    render(<Harness start={at} end={addMinutes(at, 60)} minAdvanceHours={24} />);
    expect(screen.getByTestId('t-start-error')).toHaveTextContent(/at least 24 hours/i);
  });

  // ── Short-notice floor (PEMS_INTERNAL_SELF_CREATE_SHORT_NOTICE_72H plan §8.3) ──
  // minAdvanceHours=0 is what internal self-registration resolves to: today must stay pickable and
  // the only live rule left is "must be in the future" — never a message claiming "at least 0 hours".

  it('minAdvanceHours=0: today is selectable, not pushed out to a future floor', () => {
    render(<Harness minAdvanceHours={0} />);
    expect(screen.getByTestId('t-start-date')).toHaveAttribute('min', futureDate(0));
  });

  it('minAdvanceHours=0: a past time today is refused without claiming "0 hours"', () => {
    const past = new Date(Date.now() - 30 * 60 * 1000);
    const pad = (n: number) => String(n).padStart(2, '0');
    const at = `${past.getFullYear()}-${pad(past.getMonth() + 1)}-${pad(past.getDate())}T${pad(past.getHours())}:${pad(past.getMinutes())}`;

    render(<Harness start={at} end={addMinutes(at, 60)} minAdvanceHours={0} />);
    const err = screen.getByTestId('t-start-error');
    expect(err).toHaveTextContent(/future/i);
    expect(err).not.toHaveTextContent(/0\s*hours/i);
  });

  it('minAdvanceHours=0: a future time today is accepted', () => {
    const soon = new Date(Date.now() + 30 * 60 * 1000);
    const pad = (n: number) => String(n).padStart(2, '0');
    const at = `${soon.getFullYear()}-${pad(soon.getMonth() + 1)}-${pad(soon.getDate())}T${pad(soon.getHours())}:${pad(soon.getMinutes())}`;

    render(<Harness start={at} end={addMinutes(at, 60)} minAdvanceHours={0} />);
    expect(screen.queryByTestId('t-start-error')).toBeNull();
  });

  it('minAdvanceHours=0: still keeps the 72h floor at every OTHER value (no global regression)', () => {
    const soon = new Date(Date.now() + 30 * 60 * 1000);
    const pad = (n: number) => String(n).padStart(2, '0');
    const at = `${soon.getFullYear()}-${pad(soon.getMonth() + 1)}-${pad(soon.getDate())}T${pad(soon.getHours())}:${pad(soon.getMinutes())}`;
    render(<Harness start={at} end={addMinutes(at, 60)} minAdvanceHours={72} />);
    expect(screen.getByTestId('t-start-error')).toHaveTextContent(/at least 72 hours/i);
  });

  it('can be driven entirely from the keyboard', async () => {
    const date = futureDate();
    render(<Harness start={`${date}T08:00`} end={`${date}T09:00`} />);
    const input = screen.getByTestId('t-start-time');

    await press(input, 'ArrowDown');                 // opens, highlighting the current 08:00
    expect(await screen.findByTestId('t-start-time-listbox')).toBeInTheDocument();

    await press(input, 'ArrowDown');                 // 08:15
    await press(input, 'Enter');

    expect(startOut()).toBe(`${date}T08:15`);
    await waitFor(() => expect(screen.queryByTestId('t-start-time-listbox')).toBeNull());
  });

  it('escapes out of the list without changing anything', async () => {
    const date = futureDate();
    render(<Harness start={`${date}T08:00`} end={`${date}T09:00`} />);
    const input = screen.getByTestId('t-start-time');

    await press(input, 'ArrowDown');
    await press(input, 'ArrowDown');
    await press(input, 'Escape');

    expect(screen.queryByTestId('t-start-time-listbox')).toBeNull();
    expect(startOut()).toBe(`${date}T08:00`);
  });

  it('renders the list outside the scrolling form so a modal cannot clip it', async () => {
    const date = futureDate();
    render(
      <div style={{ overflow: 'auto', height: 100 }} data-testid="scroller">
        <Harness start={`${date}T08:00`} end={`${date}T09:00`} />
      </div>,
    );

    await act(async () => { fireEvent.focus(screen.getByTestId('t-start-time')); });
    const list = await screen.findByTestId('t-start-time-listbox');

    // Portalled to <body>: the overflow container is NOT an ancestor, so it cannot cut it off.
    expect(screen.getByTestId('scroller').contains(list)).toBe(false);
    expect(document.body.contains(list)).toBe(true);
    expect(list).toHaveStyle({ position: 'fixed' });
  });

  it('keeps a typo out of the stored value', async () => {
    const date = futureDate();
    render(<Harness start={`${date}T08:00`} end={`${date}T09:00`} />);
    const input = screen.getByTestId('t-start-time') as HTMLInputElement;

    await act(async () => {
      fireEvent.focus(input);
      fireEvent.change(input, { target: { value: '99:99' } });
      fireEvent.blur(input);
    });

    // The previous good value stands, rather than storing something meaningless.
    expect(startOut()).toBe(`${date}T08:00`);
    expect(input.value).toBe('08:00');
  });
});
