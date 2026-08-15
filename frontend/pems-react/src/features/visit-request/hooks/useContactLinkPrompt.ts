import { useCallback, useRef, useState } from 'react';
import type { UseFormReturn } from 'react-hook-form';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
import { campusMemberRows, findContactMemberCandidates } from '../utils/memberDuplicates';

/**
 * A campus whose operational contact was typed out by hand and turns out to describe somebody who is
 * already in its delegation (ID-01) — one human, two records, and no link between them.
 */
export interface ContactLinkPrompt {
  /** Campus card + member, so answering "two different people" is remembered for THIS pair. */
  decisionKey: string;
  campusIndex: number;
  memberClientMemberKey: string;
  memberName: string;
  memberKind: 'visitors' | 'supportTeam';
}

/**
 * The "cùng một người, hay hai người khác nhau?" question, shared by every screen that can submit a
 * campus form.
 *
 * <p>It used to live inside the create hook alone, so only the create form asked it. The edit screen
 * lets the contact block be retyped too — and asked nothing — which meant identical typing produced a
 * link on one screen and no link on the other, with nothing on either to say why.</p>
 *
 * <p><b>What each answer sends.</b> "Cùng một người" writes the member's own stable key into the
 * form, which is what the backend links by. "Hai người khác nhau" deliberately writes NOTHING: a
 * payload that carries member keys but names no contact member is how the backend is told they are
 * two humans, and it links nobody rather than re-matching the fingerprint. Do not "fix" the decline
 * branch by making it write something — what it must send is nothing.</p>
 */
export function useContactLinkPrompt(
  form: UseFormReturn<VisitRequestV2Schema>,
  resumeSubmit: () => void,
) {
  const [prompt, setPrompt] = useState<ContactLinkPrompt | null>(null);
  /** Pairs the user has already called two people — asked once per pair, per session. */
  const declined = useRef<Set<string>>(new Set());

  /**
   * The first campus whose contact matches exactly ONE of its members and is not linked to them.
   *
   * <p>Several matches mean the evidence names nobody, so nothing is asked and nothing is linked:
   * picking the first would be the guess this whole mechanism exists to remove.</p>
   */
  const findUnlinkedMatch = useCallback(
    (values: VisitRequestV2Schema): ContactLinkPrompt | null => {
      const campuses = values.campusVisits ?? [];
      for (let campusIndex = 0; campusIndex < campuses.length; campusIndex++) {
        const cv = campuses[campusIndex];
        if (!cv || cv.operationalContactClientMemberKey) continue;
        // A campus that already exists has a contact snapshot the backend refuses to change, so
        // there is no action behind the question and asking it would only interrupt. On the create
        // form no campus has an id, so this skips nothing there; on the edit screen it narrows the
        // question to a campus being ADDED, which is the only one whose contact can still be typed.
        if (cv.visitInstanceId != null) continue;
        const candidates = findContactMemberCandidates(cv.operationalContact, campusMemberRows(cv));
        if (candidates.length !== 1) continue;
        const member = candidates[0];
        if (!member.clientMemberKey) continue;
        // Keyed on the campus CARD and the person, not on an index: adding a campus above this one
        // must not resurrect a question the user has already answered.
        const decisionKey = `${cv.clientKey}::${member.clientMemberKey}`;
        if (declined.current.has(decisionKey)) continue;
        return {
          decisionKey,
          campusIndex,
          memberClientMemberKey: member.clientMemberKey,
          memberName: member.fullName,
          memberKind: member.kind,
        };
      }
      return null;
    },
    [],
  );

  /**
   * "Cùng một người — liên kết": the contact IS this member, recorded by the member's own stable
   * key. The snapshot is left exactly as typed — the backend rewrites it from the member on the
   * create path, so the two cannot end up describing different people.
   */
  const confirmSame = useCallback(() => {
    if (!prompt) return;
    form.setValue(
      `campusVisits.${prompt.campusIndex}.operationalContactClientMemberKey`,
      prompt.memberClientMemberKey,
      { shouldDirty: true },
    );
    setPrompt(null);
    // Straight back into the submit the question interrupted. Any FURTHER unlinked match — a second
    // campus with the same problem — raises its own question on this pass.
    resumeSubmit();
  }, [prompt, form, resumeSubmit]);

  /** "Là người khác": two people. Nothing is written to the form, on purpose (see above). */
  const confirmDifferent = useCallback(() => {
    if (!prompt) return;
    declined.current.add(prompt.decisionKey);
    setPrompt(null);
    resumeSubmit();
  }, [prompt, resumeSubmit]);

  /** "Xem lại": nothing is decided and nothing is sent — the user goes back to the form. */
  const dismiss = useCallback(() => setPrompt(null), []);

  /**
   * Call at the top of a submit handler. Returns true when a question was raised, meaning the submit
   * must stop here and will be resumed by whichever button the user presses.
   */
  const interrupts = useCallback(
    (values: VisitRequestV2Schema): boolean => {
      const match = findUnlinkedMatch(values);
      if (!match) return false;
      setPrompt(match);
      return true;
    },
    [findUnlinkedMatch],
  );

  return { prompt, interrupts, confirmSame, confirmDifferent, dismiss };
}
