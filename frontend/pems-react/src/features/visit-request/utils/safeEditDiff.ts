import type { ResolvedVisitForm, SafeEditPayload } from '../api/visitRequestV2Api';

/** The registrant fields the safe-edit form manages. */
export interface SafeEditRegistrantDraft {
  fullName: string;
  nationality: string;
  organization: string;
  jobTitle: string;
  phone: string;
  /** Partner profile the organization was picked from, or null for free text. */
  partnerId: number | null;
}

/**
 * One campus row in the safe-edit form.
 *
 * The operational-contact profile is deliberately ABSENT: it has exactly one door now — "Manage the
 * contact role" — so this form never carries it (plan PEMS_CONTACT_ONE_DOOR).
 */
export interface SafeEditInstanceDraft {
  visitInstanceId: number;
  expectedRowVersion: number;
  campusName: string;
  transportationNote: string;
  mediaConsentStatus: string;
  /** "Ghi chú gửi FPTU" — one general remark per campus, independent of media consent. */
  notes: string;
}

/** Trimmed text, with empty treated the same as absent — "  " and null mean the same thing here. */
const norm = (value: string | null | undefined): string => (value ?? '').trim();

/** True when the user actually changed this field. */
const changed = (before: string | null | undefined, after: string): boolean => norm(before) !== norm(after);

/**
 * Builds a SPARSE safe-edit payload: only the fields the user touched, and only the campuses that
 * have at least one touched field.
 *
 * The modal previously sent everything it had loaded. Two things went wrong with that. A user
 * correcting one campus's note also re-submitted the media-consent decision and notes of every other
 * campus — so a campus that had moved inside its window was dragged into the request and the backend
 * refused the whole edit, for a campus the user never touched. And because the form held whatever was
 * loaded when it opened, any value changed server-side in the meantime was quietly written back to
 * its old state.
 *
 * Returns null when nothing changed at all, so the caller can say so instead of sending an empty
 * request and surfacing the backend's "không có thay đổi nào" as if it were a failure.
 */
export function buildChangedOnlyPayload(
  form: ResolvedVisitForm,
  registrant: SafeEditRegistrantDraft,
  instances: SafeEditInstanceDraft[],
): SafeEditPayload | null {
  const payload: SafeEditPayload = {
    expectedRequestRowVersion: form.rowVersion,
    registrant: null,
    instances: [],
  };

  // ── Registrant. Full name is required by the backend, so once ANY registrant field changed the
  //    block carries the name too — otherwise the patch would look like a request to blank it.
  //    partnerId travels ATOMICALLY with organization — never sent independently — so the text and
  //    the id it points at can never be applied as two separate patches. ──
  const registrantChanged =
    changed(form.registrant.fullName, registrant.fullName)
    || changed(form.registrant.nationality, registrant.nationality)
    || changed(form.registrant.organization, registrant.organization)
    || changed(form.registrant.jobTitle, registrant.jobTitle)
    || changed(form.registrant.phone, registrant.phone)
    || (form.partnerId ?? null) !== (registrant.partnerId ?? null);
  if (registrantChanged) {
    payload.registrant = {
      fullName: norm(registrant.fullName),
      nationality: norm(registrant.nationality),
      organization: norm(registrant.organization) || null,
      jobTitle: norm(registrant.jobTitle) || null,
      phone: norm(registrant.phone) || null,
      partnerId: registrant.partnerId,
    };
  }

  // ── Campuses. Each field is sent ONLY if it changed; a campus with no changes is left out. ──
  for (const draft of instances) {
    const current = form.campusVisits.find(c => c.visitInstanceId === draft.visitInstanceId);
    if (!current) continue;

    const patch: NonNullable<SafeEditPayload['instances']>[number] = {
      visitInstanceId: draft.visitInstanceId,
      expectedRowVersion: draft.expectedRowVersion,
    };
    let touched = false;

    if (changed(current.transportationNote, draft.transportationNote)) {
      // "" rather than null: null means "not part of this edit", so clearing a field has to be an
      // explicit empty string.
      patch.transportationNote = norm(draft.transportationNote);
      touched = true;
    }
    if (changed(current.notes, draft.notes)) {
      patch.notes = norm(draft.notes);
      touched = true;
    }
    if (norm(current.mediaConsentStatus) !== norm(draft.mediaConsentStatus)) {
      patch.mediaConsentStatus = norm(draft.mediaConsentStatus);
      touched = true;
    }

    if (touched) payload.instances!.push(patch);
  }

  const nothingChanged = payload.registrant === null && payload.instances!.length === 0;
  return nothingChanged ? null : payload;
}
