import { describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useForm } from 'react-hook-form';
import { useContactLinkPrompt } from '../hooks/useContactLinkPrompt';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

/**
 * ID-01 — "Đầu mối này có phải chính thành viên đó không?"
 *
 * <p>The question used to live inside the create hook, so only the create form asked it. The edit
 * screen can type a contact for a campus being ADDED and asked nothing, which meant identical typing
 * linked the contact on one screen and left the request naming two people on the other. These pin
 * the shared rule both screens now run.</p>
 *
 * <p>The answers are asymmetric on purpose: "cùng một người" writes the member's key, "hai người
 * khác nhau" writes NOTHING — an unlinked contact in a payload that keys its members is exactly how
 * the backend is told they are two humans.</p>
 */

type Member = { clientMemberKey: string; fullName: string; jobTitle: string; organization: string };

const member = (key: string, fullName: string, jobTitle = 'Trưởng đoàn', organization = 'ĐH X'): Member =>
  ({ clientMemberKey: key, fullName, jobTitle, organization });

const campus = (over: Partial<Record<string, unknown>> = {}) => ({
  clientKey: 'ck-1',
  visitInstanceId: null,
  visitors: [member('k-a', 'Trần Thị B')],
  supportTeam: [],
  operationalContact: {
    fullName: 'Trần Thị B', jobTitle: 'Trưởng đoàn', organization: 'ĐH X',
    phone: '+84912345678', email: 'b@x.edu',
  },
  operationalContactClientMemberKey: null,
  ...over,
});

const values = (...campuses: unknown[]) =>
  ({ campusVisits: campuses } as unknown as VisitRequestV2Schema);

/** The hook plus a real form to write into, and a spy for the submit an answer resumes. */
const setup = () => {
  const resume = vi.fn();
  const { result } = renderHook(() => {
    const form = useForm<VisitRequestV2Schema>({ defaultValues: values(campus()) as never });
    const prompt = useContactLinkPrompt(form, resume);
    return { form, prompt };
  });
  return { result, resume };
};

describe('useContactLinkPrompt', () => {
  it('asks when the contact matches exactly one member and nothing links them', () => {
    const { result } = setup();

    let interrupted = false;
    act(() => { interrupted = result.current.prompt.interrupts(values(campus())); });

    expect(interrupted).toBe(true);
    expect(result.current.prompt.prompt?.memberName).toBe('Trần Thị B');
    expect(result.current.prompt.prompt?.memberKind).toBe('visitors');
  });

  it('"cùng một người" writes the member key and resumes the submit', () => {
    const { result, resume } = setup();
    act(() => { result.current.prompt.interrupts(values(campus())); });

    act(() => { result.current.prompt.confirmSame(); });

    expect(result.current.form.getValues('campusVisits.0.operationalContactClientMemberKey'))
      .toBe('k-a');
    expect(resume).toHaveBeenCalledTimes(1);
    expect(result.current.prompt.prompt).toBeNull();
  });

  it('"hai người khác nhau" writes NOTHING, resumes, and is not asked twice', () => {
    const { result, resume } = setup();
    act(() => { result.current.prompt.interrupts(values(campus())); });

    act(() => { result.current.prompt.confirmDifferent(); });

    // The absence of a key IS the answer — anything written here would undo it.
    expect(result.current.form.getValues('campusVisits.0.operationalContactClientMemberKey'))
      .toBeNull();
    expect(resume).toHaveBeenCalledTimes(1);

    let askedAgain = true;
    act(() => { askedAgain = result.current.prompt.interrupts(values(campus())); });
    expect(askedAgain).toBe(false);
  });

  it('says nothing when the contact is already linked', () => {
    const { result } = setup();

    let interrupted = true;
    act(() => {
      interrupted = result.current.prompt.interrupts(
        values(campus({ operationalContactClientMemberKey: 'k-a' })));
    });

    expect(interrupted).toBe(false);
  });

  it('says nothing when two members fit — the evidence names nobody', () => {
    const { result } = setup();

    let interrupted = true;
    act(() => {
      interrupted = result.current.prompt.interrupts(values(campus({
        visitors: [member('k-a', 'Trần Thị B'), member('k-b', 'Trần Thị B')],
      })));
    });

    expect(interrupted).toBe(false);
  });

  it('says nothing for a campus that already exists — its contact cannot be changed anyway', () => {
    const { result } = setup();

    let interrupted = true;
    act(() => {
      interrupted = result.current.prompt.interrupts(values(campus({ visitInstanceId: 42 })));
    });

    expect(interrupted).toBe(false);
  });

  // ── plan CanhIter3FixBug §24 — explicit MEMBER/EXTERNAL is never re-asked ─────────────────────
  it('says nothing once the campus has an explicit MEMBER source — the user already answered', () => {
    const { result } = setup();

    let interrupted = true;
    act(() => {
      interrupted = result.current.prompt.interrupts(
        values(campus({ operationalContactSource: 'MEMBER' })));
    });

    expect(interrupted).toBe(false);
  });

  it('says nothing once the campus has an explicit EXTERNAL source — the user already answered', () => {
    const { result } = setup();

    let interrupted = true;
    act(() => {
      interrupted = result.current.prompt.interrupts(
        values(campus({ operationalContactSource: 'EXTERNAL' })));
    });

    expect(interrupted).toBe(false);
  });

  it('still asks for a legacy/no-source campus — the fuzzy-match question stays alive for that path', () => {
    const { result } = setup();

    let interrupted = false;
    act(() => {
      interrupted = result.current.prompt.interrupts(
        values(campus({ operationalContactSource: null })));
    });

    expect(interrupted).toBe(true);
  });

  it('finds a match among the SUPPORT members too', () => {
    const { result } = setup();

    act(() => {
      result.current.prompt.interrupts(values(campus({
        visitors: [member('k-a', 'Nguyễn Văn A')],
        supportTeam: [member('k-c', 'Trần Thị B')],
      })));
    });

    expect(result.current.prompt.prompt?.memberKind).toBe('supportTeam');
    expect(result.current.prompt.prompt?.memberClientMemberKey).toBe('k-c');
  });
});
