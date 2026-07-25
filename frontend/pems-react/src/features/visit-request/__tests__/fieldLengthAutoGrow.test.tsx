import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { useState } from 'react';
import i18n from '../../../shared/i18n/config';
import { buildVisitRequestV2Schema, V2_MIN_ADVANCE_HOURS_CREATE } from '../schema/visitRequestV2.schema';
import { AutoGrowTextField } from '../components/shared/AutoGrowTextField';
import { EXCEL_COLUMN_MAX_LENGTH } from '../components/ExcelUpload/excelValidator';

/**
 * Plan §24. Four `.max()` rules shipped with NO message (delegationName, visitTypeOther,
 * mediaConsentNote, notes), so a Vietnamese user who pasted too much got Zod's English default
 * — and the Excel importer checked no lengths at all, so a sheet could import cleanly and then
 * be refused by the server.
 */

const pad2 = (n: number) => String(n).padStart(2, '0');
const futureAt = (offsetMs = 0) => {
  const d = new Date(Date.now() + 30 * 24 * 3600 * 1000 + offsetMs);
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}T${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
};

const person = () => ({
  fullName: 'Khách A', jobTitle: 'GV', organization: 'ĐH Đối Tác', nationality: 'Việt Nam',
});

const validValues = () => ({
  registerInfo: {
    fullName: 'Người Đăng Ký', organization: 'Công ty X', jobTitle: 'TP',
    phone: '+84912345678', email: 'a@example.com', nationality: 'Việt Nam',
  },
  contactPoint: {
    fullName: 'Đầu Mối', organization: 'Công ty X',
    phone: '+84912345678', email: 'b@example.com',
  },
  partnerSelectionMode: 'NEW_ORGANIZATION' as const,
  partnerId: null,
  campusVisits: [{
    clientKey: 'k1',
    campus: 'HN',
    startDatetime: futureAt(),
    endDatetime: futureAt(3 * 3600 * 1000),
    delegationName: 'Đoàn A',
    visitType: 'MEETING' as const,
    visitTypeOther: '',
    purpose: 'Mục đích',
    workingContent: 'Nội dung',
    visitors: [person()],
    supportTeam: [],
    operationalContact: {
      fullName: 'ĐM', organization: 'Org', phone: '+84912345678', email: 'c@example.com',
    },
    workingLanguage: 'VI' as const,
    transportationNote: '',
    mediaConsentStatus: 'DECLINED' as const,
    mediaConsentNote: '',
    notes: '',
  }],
});

/** The message Zod produced for `path`, or undefined when the value was accepted. */
const messageFor = (values: ReturnType<typeof validValues>, path: (string | number)[]) => {
  const schema = buildVisitRequestV2Schema(
    V2_MIN_ADVANCE_HOURS_CREATE,
    (key, opts) => i18n.t(key, { ns: 'validation', ...opts }) as string,
  );
  const result = schema.safeParse(values);
  if (result.success) return undefined;
  return result.error.issues.find(i => i.path.join('.') === path.join('.'))?.message;
};

describe('length rules carry a real message (plan §18)', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  const cases: Array<[string, number, (v: ReturnType<typeof validValues>, s: string) => void]> = [
    ['campusVisits.0.delegationName', 200, (v, s) => { v.campusVisits[0].delegationName = s; }],
    ['campusVisits.0.purpose', 2000, (v, s) => { v.campusVisits[0].purpose = s; }],
    ['campusVisits.0.workingContent', 4000, (v, s) => { v.campusVisits[0].workingContent = s; }],
    ['campusVisits.0.transportationNote', 2000, (v, s) => { v.campusVisits[0].transportationNote = s; }],
    ['campusVisits.0.mediaConsentNote', 2000, (v, s) => { v.campusVisits[0].mediaConsentNote = s; }],
    ['campusVisits.0.notes', 2000, (v, s) => { v.campusVisits[0].notes = s; }],
    ['registerInfo.fullName', 150, (v, s) => { v.registerInfo.fullName = s; }],
    ['registerInfo.organization', 200, (v, s) => { v.registerInfo.organization = s; }],
    ['registerInfo.nationality', 100, (v, s) => { v.registerInfo.nationality = s; }],
    ['campusVisits.0.visitors.0.fullName', 150, (v, s) => { v.campusVisits[0].visitors[0].fullName = s; }],
    ['campusVisits.0.operationalContact.organization', 200, (v, s) => { v.campusVisits[0].operationalContact.organization = s; }],
  ];

  it.each(cases)('%s over %i characters is refused with a translated message', (path, max, apply) => {
    const values = validValues();
    apply(values, 'x'.repeat(max + 1));

    const message = messageFor(values, path.split('.'));
    expect(message, `${path} produced no message`).toBeTruthy();
    // Zod's untranslated default is the thing we are ruling out.
    expect(message).not.toMatch(/String must contain at most/i);
    expect(message).toContain(String(max));
  });

  it.each(cases)('%s at exactly %i characters is accepted', (_path, max, apply) => {
    const values = validValues();
    apply(values, 'x'.repeat(max));
    expect(messageFor(values, _path.split('.'))).toBeUndefined();
  });

  it('says it in Vietnamese when the user is browsing in Vietnamese', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    const values = validValues();
    values.campusVisits[0].delegationName = 'x'.repeat(201);

    expect(messageFor(values, ['campusVisits', 0, 'delegationName'])).toMatch(/ký tự/);
    await act(async () => { await i18n.changeLanguage('en'); });
  });

  it('checks the SAME limits in the Excel importer as in the form', () => {
    // Drift here is what let a sheet import cleanly and then be rejected on submit.
    expect(EXCEL_COLUMN_MAX_LENGTH.fullName).toBe(150);
    expect(EXCEL_COLUMN_MAX_LENGTH.jobTitle).toBe(150);
    expect(EXCEL_COLUMN_MAX_LENGTH.organization).toBe(200);
    expect(EXCEL_COLUMN_MAX_LENGTH.nationality).toBe(100);

    for (const [column, max] of Object.entries(EXCEL_COLUMN_MAX_LENGTH)) {
      const values = validValues();
      (values.campusVisits[0].visitors[0] as Record<string, string>)[column] = 'x'.repeat(max);
      expect(messageFor(values, ['campusVisits', 0, 'visitors', 0, column])).toBeUndefined();

      const over = validValues();
      (over.campusVisits[0].visitors[0] as Record<string, string>)[column] = 'x'.repeat(max + 1);
      expect(messageFor(over, ['campusVisits', 0, 'visitors', 0, column])).toBeTruthy();
    }
  });
});

const Field = ({ max = 150, initial = '' }: { max?: number; initial?: string }) => {
  const [value, setValue] = useState(initial);
  return (
    <>
      <AutoGrowTextField value={value} onChange={setValue} maxLength={max} testId="f" ariaLabel="Field" />
      <output data-testid="out">{value}</output>
    </>
  );
};

describe('AutoGrowTextField (plan §21.2)', () => {
  it('is a textarea, so a long value wraps instead of scrolling sideways', () => {
    render(<Field initial="Trường Đại học Khoa học Tự nhiên — Đại học Quốc gia TP Hồ Chí Minh" />);
    expect(screen.getByTestId('f').tagName).toBe('TEXTAREA');
  });

  it('still behaves like one line: Enter does not break it', async () => {
    render(<Field initial="Tên đoàn" />);
    const el = screen.getByTestId('f');

    await act(async () => { fireEvent.keyDown(el, { key: 'Enter' }); });
    expect(screen.getByTestId('out').textContent).toBe('Tên đoàn');
  });

  it('flattens a multi-line paste rather than storing line breaks', async () => {
    render(<Field />);
    const el = screen.getByTestId('f');

    await act(async () => {
      fireEvent.change(el, { target: { value: 'Dòng một\nDòng hai\r\nDòng ba' } });
    });

    expect(screen.getByTestId('out').textContent).toBe('Dòng một Dòng hai Dòng ba');
  });

  it('shows the counter as the limit comes into view, and turns it red past it', async () => {
    render(<Field max={10} />);
    const el = screen.getByTestId('f');

    // Well under the limit: no counter, no noise.
    await act(async () => { fireEvent.change(el, { target: { value: 'abc' } }); });
    expect(screen.queryByText('3/10')).toBeNull();

    await act(async () => { fireEvent.change(el, { target: { value: 'abcdefgh' } }); });
    const near = screen.getByText('8/10');
    expect(near.className).not.toContain('text-red-600');

    // Over: the value is KEPT (never truncated) and the counter says so.
    await act(async () => { fireEvent.change(el, { target: { value: 'abcdefghijkl' } }); });
    expect(screen.getByTestId('out').textContent).toBe('abcdefghijkl');
    expect(screen.getByText('12/10').className).toContain('text-red-600');
  });

  it('does not put maxLength on the DOM node, so the browser cannot truncate silently', () => {
    render(<Field max={10} initial="abc" />);
    expect(screen.getByTestId('f')).not.toHaveAttribute('maxlength');
  });
});
